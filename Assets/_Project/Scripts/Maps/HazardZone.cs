using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Maps
{
    /// <summary>
    /// Damaging ground area (lava...): server ticks damage on standing players inside. Enemies are not affected.
    /// Pure volume test (no collider): keeps the NavMesh and raycasts untouched.
    /// </summary>
    public class HazardZone : MonoBehaviour
    {
        public enum ZoneShape : byte { Box, Cylinder }

        [SerializeField] private ZoneShape shape = ZoneShape.Cylinder;
        [SerializeField, Tooltip("Box: local size. Cylinder: x = diameter, y = height.")] private Vector3 size = new(10f, 3f, 10f);
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of max health per second.")] private float damagePercentPerSecond = 0.08f;
        [SerializeField, Min(0f)] private float flatDamagePerSecond = 4f;
        [SerializeField, Min(0.1f)] private float tickInterval = 0.5f;
        [SerializeField] private DamageType damageType = DamageType.Fire;

        private float nextTick;

        public bool Contains(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            if (local.y < -0.5f || local.y > size.y)
                return false;
            return shape == ZoneShape.Box
                ? Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.z * 0.5f
                : new Vector2(local.x, local.z).sqrMagnitude <= size.x * size.x * 0.25f;
        }

        private void Update()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || Time.time < nextTick)
                return;
            nextTick = Time.time + tickInterval;
            foreach (var player in PlayerAvatar.All)
            {
                if (player == null || !player.IsStanding || !Contains(player.transform.position + Vector3.up * 0.1f))
                    continue;
                float amount = (player.Health.Max * damagePercentPerSecond + flatDamagePerSecond) * tickInterval;
                var info = DamageInfo.Create(amount, damageType, null, Team.Neutral, player.transform.position, Vector3.up);
                info.ProcCoefficient = 0f;
                player.Health.ApplyDamage(info);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.05f, 0.5f);
            Gizmos.matrix = transform.localToWorldMatrix;
            if (shape == ZoneShape.Box)
                Gizmos.DrawWireCube(new Vector3(0f, size.y * 0.5f, 0f), size);
            else
                Gizmos.DrawWireCube(new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x, size.y, size.x));
        }
    }
}
