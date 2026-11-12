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
        private SummonAttackData D => data as SummonAttackData ?? (SummonAttackData)(data = ScriptableObject.CreateInstance<SummonAttackData>());

        private SpawnCard minion => D.minion;
        private SpawnCard[] minionPool => D.minionPool;
        private int countPerCast => D.countPerCast;
        private int maxAlive => D.maxAlive;
        private Vector2 spawnDistance => D.spawnDistance;
        private float windup => D.windup;
        private float recovery => D.recovery;
        private GameObject spawnVfx => D.spawnVfx;
        private float onlyBelowHealth => D.onlyBelowHealth;
        private int maxCasts => D.maxCasts;

        private int casts;

        private readonly List<EnemyActor> minions = new();

        public override bool IsInRange(Transform target)
        {
            minions.RemoveAll(m => m == null || !m.IsSpawned || !m.Health.IsAlive);
            if (maxCasts > 0 && casts >= maxCasts)
                return false;
            if (onlyBelowHealth < 1f && Actor.Health.Normalized > onlyBelowHealth)
                return false;
            return minions.Count < maxAlive && base.IsInRange(target);
        }

        protected override IEnumerator Perform(PlayerAvatar target)
        {
            var director = CombatDirector.Instance;
            if (director == null || ((minion == null || minion.prefab == null) && minionPool.Length == 0))
                yield break;
            casts++;
            yield return new WaitForSeconds(windup);

            int count = Mathf.Min(countPerCast, maxAlive - minions.Count);
            for (int i = 0; i < count && Actor.Health.IsAlive; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(spawnDistance.x, spawnDistance.y);
                if (!NavMesh.SamplePosition(transform.position + new Vector3(dir.x, 0f, dir.y), out var hit, 3f, NavMesh.AllAreas))
                    continue;
                var card = minionPool.Length > 0 ? minionPool[Random.Range(0, minionPool.Length)] : minion;
                if (card == null || card.prefab == null)
                    continue;
                var spawned = director.Spawn(card, hit.position, transform.rotation, Actor.Level);
                minions.Add(spawned);
                VfxNetwork.Spawn(spawnVfx, hit.position, Quaternion.identity);
                if (target != null && spawned.TryGetComponent(out EnemyBrain brain))
                    brain.Alert(target, false);
            }
            yield return new WaitForSeconds(recovery);
        }
    }
}
