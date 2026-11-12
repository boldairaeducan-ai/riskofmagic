using System.Collections;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    /// <summary>
    /// A cursed zone that slowly chases its target for a while: players inside take damage and are cursed.
    /// The boss keeps fighting while it roams.
    /// </summary>
    public class EnemyHauntAttack : EnemyAttack
    {
        private HauntAttackData D => data as HauntAttackData ?? (HauntAttackData)(data = ScriptableObject.CreateInstance<HauntAttackData>());

        private float radius => D.radius;
        private float speed => D.speed;
        private float duration => D.duration;
        private float tick => D.tick;
        private float damagePerTick => D.damagePerTick;
        private DamageType damageType => D.damageType;
        private float curseDuration => D.curseDuration;
        private float windup => D.windup;
        private float recovery => D.recovery;
        private GameObject zoneVfx => D.zoneVfx;
        private LayerMask groundMask => D.groundMask;

        protected override IEnumerator Perform(PlayerAvatar target)
        {
            yield return new WaitForSeconds(windup);
            if (!Actor.Health.IsAlive || target == null)
                yield break;
            StartCoroutine(Roam(target));
            yield return new WaitForSeconds(recovery);
        }

        private IEnumerator Roam(PlayerAvatar target)
        {
            Vector3 position = EnemyBossUtility.Ground(transform.position + transform.forward * 2f, groundMask);
            for (float t = 0f; t < duration; t += tick)
            {
                if (Actor == null || !Actor.Health.IsAlive)
                    yield break;
                if (target != null && target.IsStanding)
                {
                    Vector3 to = target.transform.position - position;
                    to.y = 0f;
                    position += Vector3.ClampMagnitude(to, speed * tick);
                    position = EnemyBossUtility.Ground(position, groundMask);
                }
                VfxNetwork.Spawn(zoneVfx, position, Quaternion.identity, radius);
                EnemyBossUtility.HitArea(Actor, position, radius, damagePerTick, damageType, 3f, true, StatusType.Curse, curseDuration);
                yield return new WaitForSeconds(tick);
            }
        }
    }
}
