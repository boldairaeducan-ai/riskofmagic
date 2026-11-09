using System.Collections;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    public enum EruptionPattern : byte
    {
        /// <summary>Under every player in range (+ extra impacts around them).</summary>
        UnderPlayers,
        /// <summary>A line from the boss towards the target, impacts rolling outwards one after another.</summary>
        Line,
        /// <summary>A ring around the boss.</summary>
        Ring
    }

    /// <summary>
    /// Telegraphed ground impacts (thorns, avalanche, hammer slams): ground markers, then area damage after a delay,
    /// with an optional status and an optional lingering hazard (lava pool).
    /// </summary>
    public class EnemyEruptionAttack : EnemyAttack
    {
        [SerializeField] private EruptionPattern pattern = EruptionPattern.UnderPlayers;
        [SerializeField] private float damage = 25f;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField, Min(0.3f)] private float radius = 2.5f;
        [SerializeField, Min(0f)] private float windup = 0.6f;
        [SerializeField, Min(0.2f), Tooltip("Marker → impact. Keep it under the telegraph VFX lifetime.")] private float delay = 1.2f;
        [SerializeField, Min(0f), Tooltip("Line: delay between two consecutive impacts.")] private float stepDelay = 0.12f;
        [SerializeField, Min(1), Tooltip("Line/Ring: impacts. UnderPlayers: extra impacts around each player.")] private int count = 6;
        [SerializeField, Min(0f), Tooltip("Line: distance between impacts. Ring: ring radius. UnderPlayers: extra impact offset.")]
        private float spacing = 3f;
        [SerializeField, Min(0f)] private float recovery = 0.8f;
        [SerializeField] private bool applyStatus;
        [SerializeField] private StatusType status = StatusType.Chilled;
        [SerializeField, Min(0.1f)] private float statusDuration = 1.5f;
        [Header("Lingering hazard (0 = none)")]
        [SerializeField, Min(0f)] private float hazardDuration;
        [SerializeField] private float hazardDamagePerTick = 5f;
        [SerializeField, Min(0.1f)] private float hazardTick = 0.5f;
        [SerializeField, Tooltip("Respawned every vfx lifetime while the hazard lasts.")] private GameObject hazardVfx;
        [SerializeField, Min(0.1f)] private float hazardVfxLifetime = 3f;
        [Header("VFX")]
        [SerializeField] private GameObject telegraphVfx;
        [SerializeField] private GameObject impactVfx;
        [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;

        private readonly List<PlayerAvatar> players = new();
        private readonly List<Vector3> points = new();

        protected override IEnumerator Perform(PlayerAvatar target)
        {
            yield return new WaitForSeconds(windup);
            if (!Actor.Health.IsAlive)
                yield break;

            BuildPoints(target);
            for (int i = 0; i < points.Count; i++)
            {
                VfxNetwork.Spawn(telegraphVfx, points[i], Quaternion.identity, radius);
                if (pattern == EruptionPattern.Line)
                    StartCoroutine(Impact(points[i], delay + i * stepDelay));
            }
            if (pattern != EruptionPattern.Line)
                foreach (var point in points)
                    StartCoroutine(Impact(point, delay));
            yield return new WaitForSeconds(delay + (pattern == EruptionPattern.Line ? points.Count * stepDelay : 0f) + recovery);
        }

        private void BuildPoints(PlayerAvatar target)
        {
            points.Clear();
            switch (pattern)
            {
                case EruptionPattern.Line:
                {
                    Vector3 direction = target != null ? target.transform.position - transform.position : transform.forward;
                    direction.y = 0f;
                    direction = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
                    for (int i = 1; i <= count; i++)
                        points.Add(EnemyBossUtility.Ground(transform.position + direction * (spacing * i), groundMask));
                    break;
                }
                case EruptionPattern.Ring:
                    for (int i = 0; i < count; i++)
                    {
                        float angle = i * Mathf.PI * 2f / count;
                        points.Add(EnemyBossUtility.Ground(transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spacing, groundMask));
                    }
                    break;
                default:
                    EnemyBossUtility.PlayersInRange(transform.position, range, players);
                    foreach (var player in players)
                    {
                        points.Add(EnemyBossUtility.Ground(player.transform.position, groundMask));
                        for (int i = 0; i < count - 1; i++)
                        {
                            Vector2 offset = Random.insideUnitCircle.normalized * spacing;
                            points.Add(EnemyBossUtility.Ground(player.transform.position + new Vector3(offset.x, 0f, offset.y), groundMask));
                        }
                    }
                    break;
            }
        }

        private IEnumerator Impact(Vector3 point, float wait)
        {
            yield return new WaitForSeconds(wait);
            if (Actor == null || !Actor.Health.IsAlive)
                yield break;
            VfxNetwork.Spawn(impactVfx, point, Quaternion.identity, radius);
            EnemyBossUtility.HitArea(Actor, point, radius, damage, damageType, 3f, applyStatus, status, statusDuration);
            if (hazardDuration <= 0f)
                yield break;

            float nextVfx = 0f;
            for (float t = 0f; t < hazardDuration; t += hazardTick)
            {
                if (t >= nextVfx)
                {
                    VfxNetwork.Spawn(hazardVfx, point, Quaternion.identity, radius);
                    nextVfx += hazardVfxLifetime;
                }
                yield return new WaitForSeconds(hazardTick);
                if (Actor == null)
                    yield break;
                EnemyBossUtility.HitArea(Actor, point, radius, hazardDamagePerTick, damageType, 2f);
            }
        }
    }
}
