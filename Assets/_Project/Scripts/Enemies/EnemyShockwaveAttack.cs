using System.Collections;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    /// <summary>
    /// Ground slam: telegraphed windup, damage around the enemy, then an expanding ring along the ground that hits
    /// grounded players once. Jumping over the ring is the counterplay. The ring keeps travelling while the enemy acts.
    /// </summary>
    public class EnemyShockwaveAttack : EnemyAttack
    {
        [Header("Slam")]
        [SerializeField] private float slamDamage = 30f;
        [SerializeField, Min(0f)] private float slamRadius = 4f;
        [SerializeField, Min(0f)] private float windup = 1f;
        [SerializeField, Min(0f)] private float recovery = 0.8f;

        [Header("Wave")]
        [SerializeField] private float waveDamage = 20f;
        [SerializeField, Min(1f)] private float waveMaxRadius = 25f;
        [SerializeField, Min(0.1f), Tooltip("Must match the ExpandingRing VFX speed.")] private float waveSpeed = 14f;
        [SerializeField, Min(0.1f)] private float waveThickness = 1.5f;
        [SerializeField, Min(0f), Tooltip("Players higher than this above the ground are jumping over the wave.")]
        private float jumpClearance = 0.5f;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;
        [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;

        [Header("VFX (VfxNetwork)")]
        [SerializeField] private GameObject telegraphVfx;
        [SerializeField] private GameObject slamVfx;
        [SerializeField, Tooltip("ExpandingRing prefab, scaled by the max radius.")] private GameObject waveVfx;

        private static readonly RaycastHit[] groundHits = new RaycastHit[8];
        private readonly HashSet<Health> slamHit = new();

        protected override IEnumerator Perform(PlayerAvatar target)
        {
            VfxNetwork.Spawn(telegraphVfx, transform.position, Quaternion.identity, slamRadius);
            yield return new WaitForSeconds(windup);
            if (!Actor.Health.IsAlive)
                yield break;

            Vector3 center = transform.position;
            VfxNetwork.Spawn(slamVfx, center, Quaternion.identity, slamRadius);
            slamHit.Clear();
            var info = DamageInfo.Create(slamDamage * Actor.DamageMultiplier, damageType, NetworkObject, Actor.Health.Team, center, Vector3.up);
            AreaDamage.Apply(center + Vector3.up, slamRadius, info, hitMask, slamHit);

            VfxNetwork.Spawn(waveVfx, center, Quaternion.identity, waveMaxRadius);
            StartCoroutine(Wave(center, Actor.DamageMultiplier, Actor.Health.Team));

            yield return new WaitForSeconds(recovery);
        }

        private IEnumerator Wave(Vector3 center, float damageMultiplier, RiskOfMagic.Core.Team team)
        {
            var hit = new HashSet<PlayerAvatar>();
            for (float radius = 0f; radius < waveMaxRadius;)
            {
                float previous = radius;
                radius += waveSpeed * Time.deltaTime;
                foreach (var player in PlayerAvatar.All)
                {
                    if (player == null || !player.IsStanding || hit.Contains(player))
                        continue;
                    Vector3 offset = player.transform.position - center;
                    if (Mathf.Abs(offset.y) > maxHeightDifference)
                        continue;
                    offset.y = 0f;
                    float distance = offset.magnitude;
                    if (distance < previous - waveThickness || distance > radius || IsAirborne(player))
                        continue;

                    hit.Add(player);
                    Vector3 direction = distance > 0.01f ? offset / distance : Vector3.forward;
                    var info = DamageInfo.Create(waveDamage * damageMultiplier, damageType, NetworkObject, team,
                        player.transform.position + Vector3.up, direction);
                    player.Health.ApplyDamage(info);
                }
                yield return null;
            }
        }

        private bool IsAirborne(PlayerAvatar player)
        {
            Vector3 origin = player.transform.position + Vector3.up * 0.1f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 0.1f + jumpClearance, groundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!groundHits[i].collider.transform.IsChildOf(player.transform))
                    return false;
            return true;
        }
    }
}
