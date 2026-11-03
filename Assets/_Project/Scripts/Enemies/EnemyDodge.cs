using System;
using System.Collections;
using RiskOfMagic.Player;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RiskOfMagic.Enemies
{
    /// <summary>
    /// Server-side sidestep, triggered by EnemyBrain when hit or when the target aims at this enemy.
    /// Movement goes through NavMeshAgent.Move so it never leaves the NavMesh.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyDodge : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float chanceOnHit = 0.25f;
        [SerializeField, Range(0f, 1f), Tooltip("Chance per check while the target looks at this enemy.")]
        private float chanceWhenAimedAt = 0.2f;
        [SerializeField, Min(0.05f)] private float aimCheckInterval = 0.5f;
        [SerializeField, Range(1f, 45f)] private float aimConeAngle = 8f;
        [SerializeField, Min(0f)] private float aimMaxDistance = 25f;

        [Header("Sidestep")]
        [SerializeField, Min(0.5f)] private float distance = 4.5f;
        [SerializeField, Min(0.05f)] private float duration = 0.25f;
        [SerializeField, Min(0f)] private float cooldown = 2.5f;
        [SerializeField, Range(0f, 1f), Tooltip("How much the dodge also moves away from the threat.")] private float backwardBias = 0.3f;

        private NavMeshAgent agent;
        private Coroutine routine;
        private float readyTime;
        private float nextAimCheck;

        /// <summary>Server only: (dodge direction).</summary>
        public event Action<Vector3> DodgeStarted;

        public bool IsDodging { get; private set; }

        private void Awake() => agent = GetComponent<NavMeshAgent>();

        public void OnHit(Vector3 threatPosition)
        {
            if (Random.value < chanceOnHit)
                TryDodge(threatPosition);
        }

        /// <summary>Returns true if a dodge started because the player is aiming at this enemy.</summary>
        public bool CheckAimedAt(PlayerAvatar player)
        {
            if (player == null || Time.time < nextAimCheck)
                return false;
            nextAimCheck = Time.time + aimCheckInterval;

            Vector3 toMe = transform.position - player.transform.position;
            toMe.y = 0f;
            if (toMe.sqrMagnitude > aimMaxDistance * aimMaxDistance)
                return false;
            Vector3 facing = player.transform.forward;
            facing.y = 0f;
            if (Vector3.Angle(facing, toMe) > aimConeAngle)
                return false;
            return Random.value < chanceWhenAimedAt && TryDodge(player.transform.position);
        }

        public bool TryDodge(Vector3 threatPosition)
        {
            if (IsDodging || Time.time < readyTime || !agent.enabled || !agent.isOnNavMesh)
                return false;

            Vector3 position = transform.position;
            Vector3 away = position - threatPosition;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, away);
            float sign = Random.value < 0.5f ? 1f : -1f;

            for (int attempt = 0; attempt < 2; attempt++, sign = -sign)
            {
                Vector3 direction = (side * sign + away * backwardBias).normalized;
                Vector3 target = position + direction * distance;
                if (NavMesh.Raycast(position, target, out var hit, NavMesh.AllAreas))
                    target = hit.position;
                Vector3 offset = target - position;
                if (offset.magnitude < distance * 0.5f)
                    continue;

                routine = StartCoroutine(DodgeRoutine(offset));
                DodgeStarted?.Invoke(direction);
                return true;
            }
            return false;
        }

        public void Cancel()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
            IsDodging = false;
        }

        private IEnumerator DodgeRoutine(Vector3 offset)
        {
            IsDodging = true;
            readyTime = Time.time + cooldown;
            if (agent.isOnNavMesh)
                agent.isStopped = true;

            Vector3 moved = Vector3.zero;
            for (float t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / duration);
                Vector3 step = offset * t - moved;
                if (agent.enabled && agent.isOnNavMesh)
                    agent.Move(step);
                moved += step;
                yield return null;
            }

            IsDodging = false;
            routine = null;
        }
    }
}
