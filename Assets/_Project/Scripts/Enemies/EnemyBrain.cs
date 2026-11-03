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
        Leap,
        Dead
    }

    /// <summary>
    /// Server-side AI.
    /// Perception: players entering the detection radius (optionally in sight), hitting the enemy, or reported by
    /// an ally become known for good; the closest known living player is chased.
    /// Navigation: NavMesh path when complete; otherwise walk as close as possible and leap (EnemyLeap) onto the
    /// NavMesh area under the target (platforms, gaps).
    /// </summary>
    [RequireComponent(typeof(EnemyActor), typeof(NavMeshAgent))]
    public class EnemyBrain : NetworkBehaviour
    {
        [SerializeField] private EnemyAttack attack;

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

        private readonly HashSet<PlayerAvatar> knownPlayers = new();
        private EnemyActor actor;
        private NavMeshAgent agent;
        private EnemyLeap leap;
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
            if (attack == null)
                attack = GetComponent<EnemyAttack>();
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

            actor.Health.Died += OnDied;
            actor.Health.Damaged += OnDamaged;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
                return;
            actor.Health.Died -= OnDied;
            actor.Health.Damaged -= OnDamaged;
        }

        /// <summary>Server only. Makes this enemy (and optionally its allies) chase the player from now on.</summary>
        public void Alert(PlayerAvatar player, bool propagate = true)
        {
            if (!IsServer || player == null || !player.IsAlive || State == EnemyState.Dead || !knownPlayers.Add(player))
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
            if (leap != null && leap.IsLeaping)
            {
                State = EnemyState.Leap;
                return;
            }
            if (!agent.enabled || !agent.isOnNavMesh)
                return;

            if (Time.time >= nextPerception || (Target != null && !Target.IsAlive))
            {
                nextPerception = Time.time + perceptionInterval;
                Perceive();
                Target = PickTarget();
            }

            if (Target == null)
                Wander();
            else if (attack != null && (attack.IsAttacking || attack.IsInRange(Target.transform)))
                Attack();
            else
                Chase();
        }

        // ---------------------------------------------------------------- Perception

        private void Perceive()
        {
            knownPlayers.RemoveWhere(p => p == null || !p.IsSpawned);
            float range = Definition != null ? Definition.detectionRange : 30f;
            float rangeSqr = range * range;

            foreach (var player in PlayerAvatar.All)
            {
                if (!player.IsAlive || knownPlayers.Contains(player))
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
                if (!player.IsAlive)
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
            if (info.TryGetSource(out var source) && source.TryGetComponent(out PlayerAvatar player))
                Alert(player);
        }

        // ---------------------------------------------------------------- Behaviours

        private void Wander()
        {
            State = EnemyState.Idle;
            agent.isStopped = false;
            agent.stoppingDistance = 0.3f;
            agent.speed = Definition != null ? Definition.moveSpeed * Definition.wanderSpeedMultiplier : 1.5f;
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

        private void Attack()
        {
            State = EnemyState.Attack;
            agent.isStopped = true;
            FaceTarget();
            if (attack.IsReady)
                attack.TryBegin(Target);
        }

        private void Chase()
        {
            State = EnemyState.Chase;
            agent.isStopped = false;
            agent.speed = Definition != null ? Definition.moveSpeed : 4.5f;
            agent.stoppingDistance = attack != null ? attack.Range * 0.8f : 1f;

            if (Time.time < nextRepath)
                return;
            nextRepath = Time.time + repathInterval;
            UpdatePath();

            // Target unreachable by walking: leap onto its NavMesh area once in range.
            if (!pathComplete && targetOnNavMesh && leap != null && leap.TryLeap(targetNavPoint))
                State = EnemyState.Leap;
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

        private void FaceTarget()
        {
            Vector3 direction = Target.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
        }

        private void OnDied(DamageInfo info)
        {
            State = EnemyState.Dead;
            Target = null;
            knownPlayers.Clear();
            if (attack != null)
                attack.Cancel();
            if (leap != null)
                leap.Cancel();
            if (agent.enabled && agent.isOnNavMesh)
                agent.ResetPath();
            agent.enabled = false;
        }
    }
}
