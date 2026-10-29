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
        Dead
    }

    /// <summary>
    /// Server-side NavMesh AI: pick the closest living player, chase, attack when in range.
    /// Clients only receive the transform through NetworkTransform.
    /// </summary>
    [RequireComponent(typeof(EnemyActor), typeof(NavMeshAgent))]
    public class EnemyBrain : NetworkBehaviour
    {
        [SerializeField] private EnemyAttack attack;
        [SerializeField, Min(0.05f)] private float retargetInterval = 0.5f;
        [SerializeField, Min(0.05f)] private float repathInterval = 0.2f;
        [SerializeField, Min(0f)] private float turnSpeed = 540f;
        [SerializeField, Min(0f)] private float navMeshSnapDistance = 3f;

        private EnemyActor actor;
        private NavMeshAgent agent;
        private float nextRetarget;
        private float nextRepath;

        public PlayerAvatar Target { get; private set; }
        public EnemyState State { get; private set; }

        private void Awake()
        {
            actor = GetComponent<EnemyActor>();
            agent = GetComponent<NavMeshAgent>();
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

            ApplyDefinition();
            agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out var hit, navMeshSnapDistance, NavMesh.AllAreas))
                agent.Warp(hit.position);
            else
                Debug.LogWarning($"[EnemyBrain] {name} spawned off the NavMesh.", this);

            actor.Health.Died += OnDied;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
                actor.Health.Died -= OnDied;
        }

        private void ApplyDefinition()
        {
            var def = actor.Definition;
            if (def == null)
                return;
            agent.speed = def.moveSpeed;
            agent.acceleration = def.acceleration;
            agent.angularSpeed = def.angularSpeed;
            agent.stoppingDistance = attack != null ? attack.Range * 0.8f : 1f;
        }

        private void Update()
        {
            if (!IsSpawned || State == EnemyState.Dead || !agent.isOnNavMesh)
                return;

            if (Time.time >= nextRetarget || (Target != null && !Target.IsAlive))
            {
                nextRetarget = Time.time + retargetInterval;
                float range = actor.Definition != null ? actor.Definition.detectionRange : 50f;
                Target = PlayerAvatar.FindClosestAlive(transform.position, range);
            }

            if (Target == null)
            {
                State = EnemyState.Idle;
                if (agent.hasPath)
                    agent.ResetPath();
                return;
            }

            if (attack != null && (attack.IsAttacking || attack.IsInRange(Target.transform)))
            {
                State = EnemyState.Attack;
                agent.isStopped = true;
                FaceTarget();
                if (attack.IsReady)
                    attack.TryBegin(Target);
                return;
            }

            State = EnemyState.Chase;
            agent.isStopped = false;
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + repathInterval;
                agent.SetDestination(Target.transform.position);
            }
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
            if (attack != null)
                attack.Cancel();
            if (agent.isOnNavMesh)
                agent.ResetPath();
            agent.enabled = false;
        }
    }
}
