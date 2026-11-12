using System.Collections;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    /// <summary>Rotating beams around the boss (a cross by default): thin preview lines, then damaging sweeps.</summary>
    public class EnemyBeamAttack : EnemyAttack
    {
        private BeamAttackData D => data as BeamAttackData ?? (BeamAttackData)(data = ScriptableObject.CreateInstance<BeamAttackData>());

        private int beams => D.beams;
        private float length => D.length;
        private float halfWidth => D.halfWidth;
        private float rotationSpeed => D.rotationSpeed;
        private float duration => D.duration;
        private float tick => D.tick;
        private float damagePerTick => D.damagePerTick;
        private DamageType damageType => D.damageType;
        private float height => D.height;
        private float windup => D.windup;
        private float recovery => D.recovery;
        private GameObject previewVfx => D.previewVfx;
        private GameObject beamVfx => D.beamVfx;

        private readonly List<PlayerAvatar> players = new();
        private readonly HashSet<Health> hitThisTick = new();

        protected override IEnumerator Perform(PlayerAvatar target)
        {
            float angle = transform.eulerAngles.y + Random.Range(0f, 360f / beams);
            for (float t = 0f; t < windup; t += tick)
            {
                SpawnLines(previewVfx, angle);
                yield return new WaitForSeconds(tick);
            }
            float direction = Random.value < 0.5f ? 1f : -1f;
            for (float t = 0f; t < duration; t += tick)
            {
                if (!Actor.Health.IsAlive)
                    yield break;
                angle += rotationSpeed * direction * tick;
                SpawnLines(beamVfx, angle);
                Damage(angle);
                yield return new WaitForSeconds(tick);
            }
            yield return new WaitForSeconds(recovery);
        }

        private Vector3 Origin => transform.position + Vector3.up * height;

        private Vector3 Direction(float angle, int index) => Quaternion.Euler(0f, angle + index * 360f / beams, 0f) * Vector3.forward;

        private void SpawnLines(GameObject vfx, float angle)
        {
            for (int i = 0; i < beams; i++)
                VfxNetwork.SpawnLine(vfx, new[] { Origin, Origin + Direction(angle, i) * length });
        }

        private void Damage(float angle)
        {
            hitThisTick.Clear();
            EnemyBossUtility.PlayersInRange(transform.position, length + halfWidth, players);
            foreach (var player in players)
            {
                Vector3 offset = player.transform.position - transform.position;
                if (Mathf.Abs(offset.y) > 3f)
                    continue;
                offset.y = 0f;
                for (int i = 0; i < beams; i++)
                {
                    Vector3 axis = Direction(angle, i);
                    float along = Vector3.Dot(offset, axis);
                    if (along < 0f || along > length || (offset - axis * along).magnitude > halfWidth || !hitThisTick.Add(player.Health))
                        continue;
                    Vector3 point = TargetFinder.Center(player.Health);
                    player.Health.ApplyDamage(DamageInfo.Create(damagePerTick * Actor.DamageMultiplier, damageType, NetworkObject,
                        Actor.Health.Team, point, axis));
                }
            }
        }
    }
}
