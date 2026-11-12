using System.Collections;
using System.Collections.Generic;
using RiskOfMagic.Player;
using RiskOfMagic.Run;
using RiskOfMagic.Vfx;
using UnityEngine;
using UnityEngine.AI;

namespace RiskOfMagic.Enemies
{
    /// <summary>
    /// The boss roots itself and regenerates while its roots (minions) live: destroy them all to stop it early.
    /// Remaining roots wither at the end of the channel.
    /// </summary>
    public class EnemyRootRegenAttack : EnemyAttack
    {
        private RootRegenAttackData D => data as RootRegenAttackData ?? (RootRegenAttackData)(data = ScriptableObject.CreateInstance<RootRegenAttackData>());

        private SpawnCard rootCard => D.rootCard;
        private int rootCount => D.rootCount;
        private Vector2 rootDistance => D.rootDistance;
        private float healPerSecond => D.healPerSecond;
        private float channelDuration => D.channelDuration;
        private float onlyBelowHealth => D.onlyBelowHealth;
        private float windup => D.windup;
        private float recovery => D.recovery;
        private GameObject spawnVfx => D.spawnVfx;
        private GameObject healVfx => D.healVfx;

        private readonly List<EnemyActor> roots = new();

        public override bool IsInRange(Transform target) => Actor.Health.Normalized <= onlyBelowHealth && base.IsInRange(target);

        protected override IEnumerator Perform(PlayerAvatar target)
        {
            var director = CombatDirector.Instance;
            if (director == null || rootCard == null || rootCard.prefab == null)
                yield break;
            yield return new WaitForSeconds(windup);

            roots.Clear();
            for (int i = 0; i < rootCount && Actor.Health.IsAlive; i++)
            {
                float angle = (i + Random.value * 0.5f) * Mathf.PI * 2f / rootCount;
                Vector3 around = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(rootDistance.x, rootDistance.y);
                if (!NavMesh.SamplePosition(around, out var hit, 3f, NavMesh.AllAreas))
                    continue;
                roots.Add(director.Spawn(rootCard, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Actor.Level));
                VfxNetwork.Spawn(spawnVfx, hit.position, Quaternion.identity);
            }

            const float tick = 0.5f;
            for (float t = 0f; t < channelDuration && Actor.Health.IsAlive; t += tick)
            {
                roots.RemoveAll(r => r == null || !r.IsSpawned || !r.Health.IsAlive);
                if (roots.Count == 0)
                    break;
                Actor.Health.Heal(Actor.Health.Max * healPerSecond * tick);
                VfxNetwork.Spawn(healVfx, transform.position, Quaternion.identity, 2f);
                yield return new WaitForSeconds(tick);
            }

            foreach (var root in roots) // wither
                if (root != null && root.IsSpawned && root.Health.IsAlive)
                    root.NetworkObject.Despawn();
            roots.Clear();
            yield return new WaitForSeconds(recovery);
        }
    }
}
