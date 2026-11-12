using System.Collections;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    /// <summary>Rifts near the players that pull them in and hurt them at the core.</summary>
    public class EnemyVortexAttack : EnemyAttack
    {
        private VortexAttackData D => data as VortexAttackData ?? (VortexAttackData)(data = ScriptableObject.CreateInstance<VortexAttackData>());

        private int riftsPerPlayer => D.riftsPerPlayer;
        private Vector2 offsetFromPlayer => D.offsetFromPlayer;
        private float pullRadius => D.pullRadius;
        private float coreRadius => D.coreRadius;
        private float pullStrength => D.pullStrength;
        private float coreDamagePerTick => D.coreDamagePerTick;
        private DamageType damageType => D.damageType;
        private float duration => D.duration;
        private float tick => D.tick;
        private float windup => D.windup;
        private float recovery => D.recovery;
        private GameObject telegraphVfx => D.telegraphVfx;
        private GameObject riftVfx => D.riftVfx;
        private LayerMask groundMask => D.groundMask;

        private readonly List<PlayerAvatar> players = new();
        private readonly List<Vector3> rifts = new();

        protected override IEnumerator Perform(PlayerAvatar target)
        {
            rifts.Clear();
            EnemyBossUtility.PlayersInRange(transform.position, range, players);
            foreach (var player in players)
                for (int i = 0; i < riftsPerPlayer; i++)
                {
                    Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(offsetFromPlayer.x, offsetFromPlayer.y);
                    var point = EnemyBossUtility.Ground(player.transform.position + new Vector3(offset.x, 0f, offset.y), groundMask);
                    rifts.Add(point);
                    VfxNetwork.Spawn(telegraphVfx, point, Quaternion.identity, coreRadius);
                }
            yield return new WaitForSeconds(windup);
            if (rifts.Count > 0 && Actor.Health.IsAlive)
                StartCoroutine(Pull(new List<Vector3>(rifts)));
            yield return new WaitForSeconds(recovery);
        }

        private IEnumerator Pull(List<Vector3> points)
        {
            float nextVfx = 0f;
            for (float t = 0f; t < duration; t += tick)
            {
                if (Actor == null || !Actor.Health.IsAlive)
                    yield break;
                bool pulse = t >= nextVfx;
                if (pulse)
                    nextVfx += 0.4f;
                foreach (var point in points)
                {
                    if (pulse)
                        VfxNetwork.Spawn(riftVfx, point, Quaternion.identity, coreRadius);
                    foreach (var player in PlayerAvatar.All)
                    {
                        if (player == null || !player.IsStanding)
                            continue;
                        Vector3 to = point - player.transform.position;
                        to.y = 0f;
                        float distance = to.magnitude;
                        if (distance > pullRadius || distance < 0.3f)
                            continue;
                        float strength = pullStrength * Mathf.Lerp(1.6f, 0.6f, distance / pullRadius);
                        player.PushRpc(to / distance * (strength * tick));
                    }
                    EnemyBossUtility.HitArea(Actor, point, coreRadius, coreDamagePerTick, damageType, 3f);
                }
                yield return new WaitForSeconds(tick);
            }
        }
    }
}
