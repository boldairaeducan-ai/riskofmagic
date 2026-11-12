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
        private ShockwaveAttackData D => data as ShockwaveAttackData ?? (ShockwaveAttackData)(data = ScriptableObject.CreateInstance<ShockwaveAttackData>());

        private float slamDamage => D.slamDamage;
        private float slamRadius => D.slamRadius;
        private float windup => D.windup;
        private float recovery => D.recovery;

        private float waveDamage => D.waveDamage;
        private float waveMaxRadius => D.waveMaxRadius;
        private float waveSpeed => D.waveSpeed;
        private float waveThickness => D.waveThickness;
        private float jumpClearance => D.jumpClearance;
        private DamageType damageType => D.damageType;
        private LayerMask hitMask => D.hitMask;
        private LayerMask groundMask => D.groundMask;

        private GameObject telegraphVfx => D.telegraphVfx;
        private GameObject slamVfx => D.slamVfx;
        private GameObject waveVfx => D.waveVfx;

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
