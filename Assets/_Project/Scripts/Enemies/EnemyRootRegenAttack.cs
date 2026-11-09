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
        [SerializeField] private SpawnCard rootCard;
        [SerializeField, Min(1)] private int rootCount = 3;
        [SerializeField] private Vector2 rootDistance = new(4f, 7f);
        [SerializeField, Range(0f, 0.2f), Tooltip("Max health healed per second while at least one root lives.")] private float healPerSecond = 0.03f;
        [SerializeField, Min(1f)] private float channelDuration = 8f;
        [SerializeField, Range(0.1f, 1f), Tooltip("Only below this health fraction.")] private float onlyBelowHealth = 0.75f;
        [SerializeField, Min(0f)] private float windup = 0.8f;
        [SerializeField, Min(0f)] private float recovery = 0.6f;
        [SerializeField] private GameObject spawnVfx;
        [SerializeField, Tooltip("Pulsed on the boss while it regenerates.")] private GameObject healVfx;

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
