using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Run;
using RiskOfMagic.Vfx;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace RiskOfMagic.Enemies
{
    /// <summary>
    /// Final boss phases (server). Phase 1: bolts + meteors. At each health threshold it splits: illusions appear
    /// around it and it blinks among them (identical, fragile copies dealing less damage), then the next phase
    /// unlocks attacks and speeds up the meteors. Illusions vanish when the real one dies or after a while.
    /// </summary>
    [RequireComponent(typeof(EnemyActor))]
    public class ChaosAvatar : NetworkBehaviour
    {
        [System.Serializable]
        public class Phase
        {
            [Range(0f, 1f), Tooltip("Starts when health drops below this fraction.")] public float healthThreshold = 0.66f;
            [Min(0)] public int illusions = 3;
            [Tooltip("Attacks enabled from this phase on.")] public EnemyAttack[] unlockAttacks = new EnemyAttack[0];
            [Min(0.1f)] public float meteorCooldownMultiplier = 1f;
            [Min(0)] public int bonusMeteors;
        }

        [SerializeField, Tooltip("Card of this boss, used to spawn the illusions (set by the builder).")] private SpawnCard illusionCard;
        [SerializeField] private EnemyMeteorAttack meteors;
        [SerializeField, Tooltip("Attacks disabled until a phase unlocks them.")] private EnemyAttack[] lockedAtStart = new EnemyAttack[0];
        [SerializeField] private List<Phase> phases = new();

        [Header("Illusions")]
        [SerializeField, Range(0.005f, 0.2f), Tooltip("Illusion health as a fraction of the real max health.")] private float illusionHealthFraction = 0.04f;
        [SerializeField] private Vector2 splitDistance = new(8f, 14f);
        [SerializeField, Min(1f)] private float illusionLifetime = 25f;
        [SerializeField] private GameObject splitVfx;

        private readonly List<EnemyActor> illusions = new();
        private EnemyActor actor;
        private int nextPhase;
        private float illusionsExpireAt;

        private void Awake() => actor = GetComponent<EnemyActor>();

        public override void OnNetworkSpawn()
        {
            if (!IsServer || actor.IsIllusion)
            {
                if (actor.IsIllusion)
                    foreach (var attack in lockedAtStart)
                        if (attack != null)
                            attack.enabled = true; // copies use everything, with reduced damage
                enabled = actor.IsIllusion ? false : enabled;
                return;
            }
            foreach (var attack in lockedAtStart)
                if (attack != null)
                    attack.enabled = false;
            phases.Sort((a, b) => b.healthThreshold.CompareTo(a.healthThreshold));
            actor.Health.Died += OnDied;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && !actor.IsIllusion)
            {
                actor.Health.Died -= OnDied;
                ClearIllusions();
            }
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || actor.IsIllusion || !actor.Health.IsAlive)
                return;

            if (illusions.Count > 0 && Time.time >= illusionsExpireAt)
                ClearIllusions();
            else
                illusions.RemoveAll(i => i == null || !i.IsSpawned || !i.Health.IsAlive);

            if (nextPhase < phases.Count && actor.Health.Normalized <= phases[nextPhase].healthThreshold)
                EnterPhase(phases[nextPhase++]);
        }

        private void EnterPhase(Phase phase)
        {
            foreach (var attack in phase.unlockAttacks)
                if (attack != null)
                    attack.enabled = true;
            if (meteors != null)
            {
                meteors.CooldownMultiplier = phase.meteorCooldownMultiplier;
                meteors.BonusMeteors = phase.bonusMeteors;
            }
            if (phase.illusions > 0)
                Split(phase.illusions);
        }

        private void Split(int count)
        {
            if (illusionCard == null || illusionCard.prefab == null)
                return;
            ClearIllusions();

            var points = new List<Vector3>();
            for (int attempt = 0; attempt < count * 6 && points.Count < count + 1; attempt++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(splitDistance.x, splitDistance.y);
                if (NavMesh.SamplePosition(transform.position + new Vector3(dir.x, 0f, dir.y), out var hit, 3f, NavMesh.AllAreas))
                    points.Add(hit.position);
            }
            if (points.Count == 0)
                return;

            // The real one takes one of the spots: players lose track of it.
            VfxNetwork.Spawn(splitVfx, transform.position, Quaternion.identity);
            int realSpot = Random.Range(0, points.Count);
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.Warp(points[realSpot]);

            float illusionHealth = actor.Health.Max * illusionHealthFraction;
            var brain = GetComponent<EnemyBrain>();
            for (int i = 0; i < points.Count; i++)
            {
                VfxNetwork.Spawn(splitVfx, points[i], Quaternion.identity);
                if (i == realSpot)
                    continue;
                var copy = Instantiate(illusionCard.prefab, points[i], transform.rotation);
                copy.Initialize(actor.Level);
                copy.MakeIllusion(illusionHealth);
                copy.NetworkObject.Spawn(true);
                illusions.Add(copy);
                if (brain != null && brain.Target != null && copy.TryGetComponent(out EnemyBrain copyBrain))
                    copyBrain.Alert(brain.Target, false);
            }
            illusionsExpireAt = Time.time + illusionLifetime;
        }

        private void ClearIllusions()
        {
            foreach (var copy in illusions)
                if (copy != null && copy.IsSpawned)
                {
                    VfxNetwork.Spawn(splitVfx, copy.transform.position, Quaternion.identity);
                    copy.NetworkObject.Despawn();
                }
            illusions.Clear();
        }

        private void OnDied(DamageInfo info) => ClearIllusions();
    }
}
