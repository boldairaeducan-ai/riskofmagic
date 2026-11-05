using System.Collections;
using System.Collections.Generic;
using RiskOfMagic.Player;
using RiskOfMagic.Run;
using RiskOfMagic.Vfx;
using UnityEngine;
using UnityEngine.AI;

namespace RiskOfMagic.Enemies
{
    /// <summary>Spawns minions around the enemy (same level), already hunting the target. Capped by alive minions.</summary>
    public class EnemySummonAttack : EnemyAttack
    {
        [SerializeField] private SpawnCard minion;
        [SerializeField, Min(1)] private int countPerCast = 4;
        [SerializeField, Min(1)] private int maxAlive = 8;
        [SerializeField] private Vector2 spawnDistance = new(2f, 5f);
        [SerializeField, Min(0f)] private float windup = 1f;
        [SerializeField, Min(0f)] private float recovery = 0.5f;
        [SerializeField, Tooltip("Spawned at each minion (VfxNetwork).")] private GameObject spawnVfx;

        private readonly List<EnemyActor> minions = new();

        public override bool IsInRange(Transform target)
        {
            minions.RemoveAll(m => m == null || !m.IsSpawned || !m.Health.IsAlive);
            return minions.Count < maxAlive && base.IsInRange(target);
        }

        protected override IEnumerator Perform(PlayerAvatar target)
        {
            var director = CombatDirector.Instance;
            if (director == null || minion == null || minion.prefab == null)
                yield break;
            yield return new WaitForSeconds(windup);

            int count = Mathf.Min(countPerCast, maxAlive - minions.Count);
            for (int i = 0; i < count && Actor.Health.IsAlive; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(spawnDistance.x, spawnDistance.y);
                if (!NavMesh.SamplePosition(transform.position + new Vector3(dir.x, 0f, dir.y), out var hit, 3f, NavMesh.AllAreas))
                    continue;
                var spawned = director.Spawn(minion, hit.position, transform.rotation, Actor.Level);
                minions.Add(spawned);
                VfxNetwork.Spawn(spawnVfx, hit.position, Quaternion.identity);
                if (target != null && spawned.TryGetComponent(out EnemyBrain brain))
                    brain.Alert(target, false);
            }
            yield return new WaitForSeconds(recovery);
        }
    }
}
