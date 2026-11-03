using System;
using System.Collections;
using RiskOfMagic.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace RiskOfMagic.Enemies
{
    /// <summary>
    /// Server-side ballistic jump between disconnected NavMesh areas (platforms, gaps).
    /// The NavMeshAgent is disabled during the arc and warped onto the landing point.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyLeap : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float minDistance = 1f;
        [SerializeField, Min(0f)] private float maxDistance = 12f;
        [SerializeField, Min(0f)] private float maxUpHeight = 6f;
        [SerializeField, Min(0f)] private float maxDownHeight = 25f;
        [SerializeField, Min(0f), Tooltip("Height of the arc above the highest end point.")] private float arcHeight = 2f;
        [SerializeField, Min(0.1f)] private float horizontalSpeed = 10f;
        [SerializeField, Min(0f)] private float cooldown = 1.5f;

        [Header("Arc clearance")]
        [SerializeField] private LayerMask obstacleMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(2)] private int clearanceSamples = 10;
        [SerializeField, Min(0f)] private float bodyCenterHeight = 0.9f;
        [SerializeField, Min(0f)] private float clearanceRadius = 0.35f;

        private NavMeshAgent agent;
        private Coroutine routine;
        private float readyTime;

        /// <summary>Server only: (landing point).</summary>
        public event Action<Vector3> LeapStarted;

        public bool IsLeaping { get; private set; }
        public bool IsReady => !IsLeaping && Time.time >= readyTime;
        public float MaxDistance => maxDistance;

        private void Awake() => agent = GetComponent<NavMeshAgent>();

        public bool CanLeapTo(Vector3 destination)
        {
            if (!IsReady)
                return false;

            Vector3 from = transform.position;
            Vector3 flat = destination - from;
            float dy = flat.y;
            flat.y = 0f;
            float distance = flat.magnitude;
            if (distance < minDistance && Mathf.Abs(dy) < 0.5f)
                return false;
            if (distance > maxDistance || dy > maxUpHeight || -dy > maxDownHeight)
                return false;
            return IsArcClear(from, destination);
        }

        public bool TryLeap(Vector3 destination)
        {
            if (!CanLeapTo(destination))
                return false;
            routine = StartCoroutine(LeapRoutine(destination));
            LeapStarted?.Invoke(destination);
            return true;
        }

        public void Cancel()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
            if (!IsLeaping)
                return;
            IsLeaping = false;
            if (NavMesh.SamplePosition(transform.position, out var hit, 5f, NavMesh.AllAreas))
                Land(hit.position);
        }

        private IEnumerator LeapRoutine(Vector3 destination)
        {
            IsLeaping = true;
            if (agent.isOnNavMesh)
                agent.ResetPath();
            agent.enabled = false;

            Vector3 start = transform.position;
            Vector3 flat = destination - start;
            flat.y = 0f;
            float duration = Mathf.Clamp(flat.magnitude / horizontalSpeed, 0.35f, 1.5f);
            if (flat.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(flat);

            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                transform.position = Evaluate(start, destination, t);
                yield return null;
            }

            Land(destination);
            IsLeaping = false;
            routine = null;
        }

        private void Land(Vector3 position)
        {
            transform.position = position;
            agent.enabled = true;
            agent.Warp(position);
            readyTime = Time.time + cooldown;
        }

        private Vector3 Evaluate(Vector3 start, Vector3 end, float t)
        {
            float apex = Mathf.Max(start.y, end.y) + arcHeight;
            float boost = apex - (start.y + end.y) * 0.5f;
            Vector3 position = Vector3.Lerp(start, end, t);
            position.y += boost * 4f * t * (1f - t);
            return position;
        }

        private bool IsArcClear(Vector3 start, Vector3 end)
        {
            Vector3 offset = Vector3.up * bodyCenterHeight;
            Vector3 previous = Evaluate(start, end, 0f) + offset;
            for (int i = 1; i <= clearanceSamples; i++)
            {
                Vector3 next = Evaluate(start, end, i / (float)clearanceSamples) + offset;
                Vector3 segment = next - previous;
                float length = segment.magnitude;
                if (length > 0.001f && Physics.SphereCast(previous, clearanceRadius, segment / length, out var hit, length, obstacleMask, QueryTriggerInteraction.Ignore)
                    && !hit.collider.transform.IsChildOf(transform)
                    && !CombatUtility.TryGetDamageable(hit.collider, out _))
                    return false; // world geometry in the way (characters don't block)
                previous = next;
            }
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, maxDistance);
        }
    }
}
