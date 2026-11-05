using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace RiskOfMagic.Enemies
{
    public enum EnemyState : byte
    {
        Idle,
        Chase,
        Attack,
        Hold,
        Retreat,
        Dodge,
        Stunned,
        Frozen,
        Leap,
        Dead
    }

    /// <summary>
    /// Server-side AI.
    /// Perception: players entering the detection radius (optionally in sight), hitting the enemy, or reported by
    /// an ally become known for good; the closest known living player is chased.
    /// Navigation: NavMesh path when complete; otherwise walk as close as possible and leap (EnemyLeap) onto the
    /// NavMesh area under the target (platforms, gaps).
    /// Movement style (EnemyDefinition): Direct, Flank (approach from the target's sides/back) or Kite (keep a
    /// distance band, back off when approached, stand still to attack). Optional EnemyDodge sidesteps.
    /// </summary>
    [RequireComponent(typeof(EnemyActor), typeof(NavMeshAgent))]
    public class EnemyBrain : NetworkBehaviour
    {
        [SerializeField, Tooltip("Primary attack: its range drives the approach. Every EnemyAttack on the object can be used.")]
        private EnemyAttack attack;

        [Header("Perception")]
        [SerializeField, Min(0.05f)] private float perceptionInterval = 0.25f;
        [SerializeField] private LayerMask sightMask = Physics.DefaultRaycastLayers;
        [SerializeField] private float eyeHeight = 1.5f;

        [Header("Navigation")]
        [SerializeField, Min(0.05f)] private float repathInterval = 0.25f;
        [SerializeField, Min(0f), Tooltip("How far around the target to look for NavMesh (players jump, stand on edges).")]
        private float targetSampleRadius = 4f;
        [SerializeField, Min(0f)] private float turnSpeed = 540f;
        [SerializeField, Min(0f)] private float navMeshSnapDistance = 3f;

        [Header("Idle")]
        [SerializeField] private Vector2 wanderPause = new(2f, 5f);

        [Header("Crowd (no single file)")]
        [SerializeField, Tooltip("Direct chasers of a same player spread their approach angles around them.")]
        private bool surround = true;
        [SerializeField, Range(10f, 120f), Tooltip("Max angle between two neighbours on the ring (smaller crowds spread less).")]
        private float maxSpacingAngle = 40f;
        [SerializeField, Min(0.5f), Tooltip("Minimum radius of the approach ring around the target.")] private float minRingRadius = 2.2f;
        [SerializeField, Min(0f), Tooltip("Closer than ring radius + this: go straight for the target.")] private float commitDistance = 2.5f;
        [SerializeField, Tooltip("Random NavMesh avoidance priority so agents sidestep each other instead of queueing.")]
        private Vector2Int avoidancePriorityRange = new(30, 70);

        private readonly HashSet<PlayerAvatar> knownPlayers = new();

        // Crowd coordination (server): approach angles around each target, refreshed a few times per second.
        private const float CrowdRefreshInterval = 0.4f;
        private static readonly List<EnemyBrain> brains = new();
        private static readonly Dictionary<PlayerAvatar, float> crowdRefreshAt = new();
        private static readonly List<EnemyBrain> crowdBuffer = new();
        private float approachAngle = float.NaN; // world yaw (degrees) around the target, NaN = none

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCrowd()
        {
            brains.Clear();
            crowdRefreshAt.Clear();
        }
        private EnemyActor actor;
        private NavMeshAgent agent;
        private EnemyLeap leap;
        private EnemyDodge dodge;
        private EnemyAttack[] attacks;
        private EnemyAttack currentAttack;
        private float flankAngle;
        private Coroutine pushRoutine;
        private NavMeshPath path;
        private Vector3 home;
        private Vector3 targetNavPoint;
        private bool targetOnNavMesh;
        private bool pathComplete;
        private float nextPerception;
        private float nextRepath;
        private float nextWander = -1f;

        public PlayerAvatar Target { get; private set; }
        public EnemyState State { get; private set; }

        private EnemyDefinition Definition => actor.Definition;

        private void Awake()
        {
            actor = GetComponent<EnemyActor>();
            agent = GetComponent<NavMeshAgent>();
            leap = GetComponent<EnemyLeap>();
            dodge = GetComponent<EnemyDodge>();
            if (attack == null)
                attack = GetComponent<EnemyAttack>();
            attacks = GetComponents<EnemyAttack>();
            agent.enabled = false; // enabled on the server once spawned
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                enabled = false;
                return;
            }

            path = new NavMeshPath();
            agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out var hit, navMeshSnapDistance, NavMesh.AllAreas))
                agent.Warp(hit.position);
            else
                Debug.LogWarning($"[EnemyBrain] {name} spawned off the NavMesh.", this);
            home = transform.position;
            if (Definition != null)
                flankAngle = Random.Range(Definition.flankAngle.x, Definition.flankAngle.y) * (Random.value < 0.5f ? -1f : 1f);
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.avoidancePriority = Random.Range(avoidancePriorityRange.x, avoidancePriorityRange.y + 1);
            brains.Add(this);

            actor.Health.Died += OnDied;
            actor.Health.Damaged += OnDamaged;
        }

        public override void OnNetworkDespawn()
        {
            brains.Remove(this);
            if (!IsServer)
                return;
            actor.Health.Died -= OnDied;
            actor.Health.Damaged -= OnDamaged;
        }

        /// <summary>Server only. Knockback along the NavMesh, interrupting the current attack.</summary>
        public void Push(Vector3 displacement, float duration = 0.25f)
        {
            if (!IsServer || State == EnemyState.Dead || (leap != null && leap.IsLeaping) || !agent.enabled || !agent.isOnNavMesh)
                return;
            float resistance = Definition != null ? Definition.controlResistance : 0f;
            displacement.y = 0f;
            displacement *= 1f - resistance;
            if (displacement.sqrMagnitude < 0.01f)
                return;

            CancelAttacks();
            if (dodge != null)
                dodge.Cancel();
            if (pushRoutine != null)
                StopCoroutine(pushRoutine);
            pushRoutine = StartCoroutine(PushRoutine(displacement, duration));
        }

        private System.Collections.IEnumerator PushRoutine(Vector3 displacement, float duration)
        {
            agent.isStopped = true;
            Vector3 moved = Vector3.zero;
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / duration);
                Vector3 step = displacement * (1f - (1f - t) * (1f - t)) - moved; // ease out
                if (agent.enabled && agent.isOnNavMesh)
                    agent.Move(step);
                moved += step;
                yield return null;
            }
            pushRoutine = null;
        }

        private void HoldStill(EnemyState state)
        {
            State = state;
            if (IsAttacking)
                CancelAttacks();
            if (dodge != null)
                dodge.Cancel();
            if (leap != null && leap.IsLeaping)
                leap.Cancel();
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }

        /// <summary>Server only. Makes this enemy (and optionally its allies) chase the player from now on.</summary>
        public void Alert(PlayerAvatar player, bool propagate = true)
        {
            if (!IsServer || player == null || !player.IsStanding || State == EnemyState.Dead || !knownPlayers.Add(player))
                return;
            if (!propagate || Definition == null || Definition.alertAlliesRadius <= 0f)
                return;

            float radiusSqr = Definition.alertAlliesRadius * Definition.alertAlliesRadius;
            foreach (var other in EnemyActor.Active)
            {
                if (other == actor || (other.transform.position - transform.position).sqrMagnitude > radiusSqr)
                    continue;
                if (other.TryGetComponent(out EnemyBrain brain))
                    brain.Alert(player, false);
            }
        }

        private void Update()
        {
            if (!IsSpawned || State == EnemyState.Dead)
                return;
            if (actor.IsFrozen)
            {
                HoldStill(EnemyState.Frozen);
                return;
            }
            if (pushRoutine != null)
            {
                State = EnemyState.Stunned;
                return;
            }
            if (leap != null && leap.IsLeaping)
            {
                State = EnemyState.Leap;
                return;
            }
            if (dodge != null && dodge.IsDodging)
            {
                State = EnemyState.Dodge;
                return;
            }
            if (!agent.enabled || !agent.isOnNavMesh)
                return;

            if (Time.time >= nextPerception || (Target != null && !Target.IsStanding))
            {
                nextPerception = Time.time + perceptionInterval;
                Perceive();
                Target = PickTarget();
            }

            if (Target == null)
            {
                Wander();
                return;
            }

            if (IsAttacking)
            {
                State = EnemyState.Attack;
                agent.isStopped = true;
                FaceTarget(currentAttack != null ? currentAttack.TrackingFactor : 1f);
                return;
            }
            if (dodge != null && dodge.CheckAimedAt(Target))
            {
                State = EnemyState.Dodge;
                return;
            }

            var style = Definition != null ? Definition.movementStyle : EnemyMovementStyle.Direct;
            if (style == EnemyMovementStyle.Kite)
                Kite();
            else if (!TryAttack())
                Chase(style == EnemyMovementStyle.Flank);
        }

        // ---------------------------------------------------------------- Perception

        private void Perceive()
        {
            knownPlayers.RemoveWhere(p => p == null || !p.IsSpawned);
            float range = (Definition != null ? Definition.detectionRange : 30f) * RiskOfMagic.Run.RunDifficulty.CurrentDetectionMultiplier;
            float rangeSqr = range * range;

            foreach (var player in PlayerAvatar.All)
            {
                if (!player.IsStanding || knownPlayers.Contains(player))
                    continue;
                if ((player.transform.position - transform.position).sqrMagnitude > rangeSqr)
                    continue;
                if (Definition != null && Definition.requireLineOfSight && !HasLineOfSight(player))
                    continue;
                Alert(player);
            }
        }

        private PlayerAvatar PickTarget()
        {
            PlayerAvatar best = null;
            float bestSqr = float.MaxValue;
            foreach (var player in knownPlayers)
            {
                if (!player.IsStanding)
                    continue;
                float sqr = (player.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = player;
                }
            }
            return best;
        }

        private bool HasLineOfSight(PlayerAvatar player)
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 target = player.transform.position + Vector3.up * 1.2f;
            if (!Physics.Linecast(eye, target, out var hit, sightMask, QueryTriggerInteraction.Ignore))
                return true;
            return hit.collider.transform.IsChildOf(player.transform) || hit.collider.transform.IsChildOf(transform);
        }

        private void OnDamaged(DamageInfo info)
        {
            if (!info.TryGetSource(out var source) || !source.TryGetComponent(out PlayerAvatar player))
                return;
            Alert(player);
            bool busy = IsAttacking || (leap != null && leap.IsLeaping);
            if (dodge != null && !busy && actor.Health.IsAlive)
                dodge.OnHit(player.transform.position);
        }

        // ---------------------------------------------------------------- Behaviours

        private void Wander()
        {
            State = EnemyState.Idle;
            agent.isStopped = false;
            agent.stoppingDistance = 0.3f;
            agent.speed = Definition != null ? Definition.moveSpeed * Definition.wanderSpeedMultiplier * actor.MoveSpeedMultiplier : 1.5f;
            if (Definition == null || Definition.wanderRadius <= 0f || agent.pathPending)
                return;

            bool arrived = !agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.2f;
            if (!arrived)
                return;
            if (nextWander < 0f)
            {
                nextWander = Time.time + Random.Range(wanderPause.x, wanderPause.y);
                return;
            }
            if (Time.time < nextWander)
                return;

            nextWander = -1f;
            Vector2 offset = Random.insideUnitCircle * Definition.wanderRadius;
            if (NavMesh.SamplePosition(home + new Vector3(offset.x, 0f, offset.y), out var hit, 2f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        private bool IsAttacking => currentAttack != null && currentAttack.IsAttacking;

        private void CancelAttacks()
        {
            foreach (var a in attacks)
                if (a != null)
                    a.Cancel();
            currentAttack = null;
        }

        /// <summary>
        /// Starts a random ready attack in range (weighted). With none ready, holds position if the primary attack
        /// is in range (waiting for its cooldown). False = nothing to do here, keep moving.
        /// </summary>
        private bool TryAttack()
        {
            var picked = PickAttack();
            if (picked == null && (attack == null || !attack.IsInRange(Target.transform)))
                return false;
            State = EnemyState.Attack;
            agent.isStopped = true;
            FaceTarget();
            if (picked != null && picked.TryBegin(Target))
                currentAttack = picked;
            return true;
        }

        private EnemyAttack PickAttack()
        {
            float total = 0f;
            foreach (var a in attacks)
                if (a != null && a.IsAvailable && a.IsReady && a.IsInRange(Target.transform))
                    total += a.SelectionWeight;
            if (total <= 0f)
                return null;
            float roll = Random.value * total;
            EnemyAttack last = null;
            foreach (var a in attacks)
            {
                if (a == null || !a.IsAvailable || !a.IsReady || !a.IsInRange(Target.transform))
                    continue;
                last = a;
                roll -= a.SelectionWeight;
                if (roll <= 0f)
                    return a;
            }
            return last;
        }

        private void Kite()
        {
            Vector2 band = Definition.kiteRange;
            Vector3 offset = Target.transform.position - transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;

            if (distance < band.x && TryRetreat())
                return;
            if (TryAttack())
                return;
            if (distance > band.y || attacks.Length == 0)
            {
                Chase(false, band.y * 0.85f);
                return;
            }

            // In the band but the attack is not possible (cooldown, too close for it): hold and watch.
            State = EnemyState.Hold;
            agent.isStopped = true;
            FaceTarget();
        }

        private bool TryRetreat()
        {
            State = EnemyState.Retreat;
            agent.isStopped = false;
            agent.speed = Definition.moveSpeed * 1.15f * actor.MoveSpeedMultiplier;
            agent.stoppingDistance = 0.3f;
            if (Time.time < nextRepath && agent.hasPath)
                return true;
            nextRepath = Time.time + repathInterval;

            Vector3 away = transform.position - Target.transform.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
            foreach (float angle in RetreatAngles)
            {
                Vector3 candidate = transform.position + Quaternion.Euler(0f, angle, 0f) * away * Definition.retreatDistance;
                if (!NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas) || !agent.CalculatePath(hit.position, path)
                    || path.status != NavMeshPathStatus.PathComplete)
                    continue;
                agent.SetPath(path);
                return true;
            }
            return false; // cornered: fight
        }

        private static readonly float[] RetreatAngles = { 0f, 35f, -35f, 70f, -70f, 110f, -110f };

        private void Chase(bool flank, float stoppingDistance = -1f)
        {
            State = EnemyState.Chase;
            agent.isStopped = false;
            agent.speed = (Definition != null ? Definition.moveSpeed : 4.5f) * actor.MoveSpeedMultiplier;
            agent.stoppingDistance = stoppingDistance >= 0f ? stoppingDistance : attack != null ? attack.Range * 0.8f : 1f;

            if (Time.time < nextRepath)
                return;
            nextRepath = Time.time + repathInterval;
            if (flank && TryFlankPath())
                return;
            if (!flank && surround && TrySurroundPath())
                return;
            UpdatePath();

            // Target unreachable by walking: leap onto its NavMesh area once in range.
            if (!pathComplete && targetOnNavMesh && leap != null && leap.TryLeap(targetNavPoint))
                State = EnemyState.Leap;
        }

        /// <summary>
        /// Paths to this enemy's slot on a ring around the target (angles spread by the crowd coordinator) while still
        /// far enough; false = go straight for the target.
        /// </summary>
        private bool TrySurroundPath()
        {
            RefreshCrowd(Target);
            if (float.IsNaN(approachAngle))
                return false;
            float ring = Mathf.Max(minRingRadius, attack != null ? attack.Range * 0.8f : minRingRadius);
            Vector3 toTarget = Target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.magnitude < ring + commitDistance)
                return false;

            Vector3 offset = Quaternion.Euler(0f, approachAngle, 0f) * Vector3.forward * ring;
            if (!NavMesh.SamplePosition(Target.transform.position + offset, out var hit, 2f, NavMesh.AllAreas)
                || !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                return false;
            agent.SetPath(path);
            pathComplete = true;
            targetOnNavMesh = true;
            targetNavPoint = hit.position;
            return true;
        }

        /// <summary>
        /// Gives every enemy chasing <paramref name="target"/> an approach angle: sorted by their current bearing around
        /// the target, then pushed apart to at least the spacing angle (keeping the group's mean bearing), so a line of
        /// followers fans out into an arc and then a circle instead of a single file.
        /// </summary>
        private static void RefreshCrowd(PlayerAvatar target)
        {
            if (target == null || (crowdRefreshAt.TryGetValue(target, out float next) && Time.time < next))
                return;
            crowdRefreshAt[target] = Time.time + CrowdRefreshInterval;

            crowdBuffer.Clear();
            foreach (var brain in brains)
                if (brain != null && brain.Target == target && brain.surround && brain.State != EnemyState.Dead
                    && (brain.Definition == null || brain.Definition.movementStyle == EnemyMovementStyle.Direct))
                {
                    brain.approachAngle = float.NaN;
                    crowdBuffer.Add(brain);
                }
            int count = crowdBuffer.Count;
            if (count < 2)
                return; // alone: straight line is fine

            Vector3 center = target.transform.position;
            float Bearing(EnemyBrain b)
            {
                Vector3 d = b.transform.position - center;
                return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }
            float spacing = Mathf.Min(crowdBuffer[0].maxSpacingAngle, 360f / count);
            // Unwrap bearings around the circular mean so a group around ±180° stays contiguous.
            float sx = 0f, sy = 0f;
            foreach (var b in crowdBuffer)
            {
                float r = Bearing(b) * Mathf.Deg2Rad;
                sx += Mathf.Sin(r);
                sy += Mathf.Cos(r);
            }
            float mean = Mathf.Atan2(sx, sy) * Mathf.Rad2Deg;
            var angles = new float[count];
            var ordered = new List<EnemyBrain>(crowdBuffer);
            ordered.Sort((a, b) => Mathf.DeltaAngle(mean, Bearing(a)).CompareTo(Mathf.DeltaAngle(mean, Bearing(b))));
            for (int i = 0; i < count; i++)
                angles[i] = mean + Mathf.DeltaAngle(mean, Bearing(ordered[i]));

            // Push apart (forward pass), then recenter on the original mean.
            float originalMean = 0f;
            foreach (float a in angles)
                originalMean += a;
            originalMean /= count;
            for (int i = 1; i < count; i++)
                angles[i] = Mathf.Max(angles[i], angles[i - 1] + spacing);
            float newMean = 0f;
            foreach (float a in angles)
                newMean += a;
            float shift = originalMean - newMean / count;
            for (int i = 0; i < count; i++)
                ordered[i].approachAngle = angles[i] + shift;
        }

        /// <summary>Paths to a point on the target's side/back while still far enough; false = go direct.</summary>
        private bool TryFlankPath()
        {
            float radius = Definition.flankRadius;
            Vector3 toTarget = Target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.magnitude < radius + 1.5f)
                return false; // close enough: commit

            Vector3 facing = Target.transform.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.01f)
                return false;
            Vector3 point = Target.transform.position + Quaternion.Euler(0f, flankAngle, 0f) * facing.normalized * radius;
            if (!NavMesh.SamplePosition(point, out var hit, 2f, NavMesh.AllAreas) || !agent.CalculatePath(hit.position, path)
                || path.status != NavMeshPathStatus.PathComplete)
                return false;

            // Already at the flank point: commit to the attack.
            if ((hit.position - transform.position).sqrMagnitude < 2f * 2f)
                return false;
            agent.stoppingDistance = 0.5f;
            agent.SetPath(path);
            pathComplete = true;
            return true;
        }

        private void UpdatePath()
        {
            Vector3 targetPosition = Target.transform.position;
            targetOnNavMesh = NavMesh.SamplePosition(targetPosition, out var hit, targetSampleRadius, NavMesh.AllAreas);
            targetNavPoint = targetOnNavMesh ? hit.position : targetPosition;

            if (targetOnNavMesh && agent.CalculatePath(targetNavPoint, path))
            {
                pathComplete = path.status == NavMeshPathStatus.PathComplete;
                if (path.status != NavMeshPathStatus.PathInvalid)
                {
                    // Partial paths end at the reachable point closest to the target.
                    agent.SetPath(path);
                    return;
                }
            }

            pathComplete = false;
            agent.SetDestination(targetPosition); // falls back to the closest reachable point
        }

        private void FaceTarget(float speedFactor = 1f)
        {
            Vector3 direction = Target.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * speedFactor * Time.deltaTime);
        }

        private void OnDied(DamageInfo info)
        {
            State = EnemyState.Dead;
            Target = null;
            knownPlayers.Clear();
            CancelAttacks();
            if (leap != null)
                leap.Cancel();
            if (dodge != null)
                dodge.Cancel();
            if (pushRoutine != null)
                StopCoroutine(pushRoutine);
            pushRoutine = null;
            if (agent.enabled && agent.isOnNavMesh)
                agent.ResetPath();
            agent.enabled = false;
        }
    }
}
