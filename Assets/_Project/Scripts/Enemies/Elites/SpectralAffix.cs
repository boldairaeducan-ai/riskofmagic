using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;
using UnityEngine.AI;

namespace RiskOfMagic.Enemies.Elites
{
    /// <summary>Éthéré: becomes invulnerable briefly after being hit (with a cooldown), blinks next to a distant target.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Elites/Spectral", fileName = "Elite_Spectral")]
    public class SpectralAffix : EliteAffix
    {
        [Header("Phase")]
        [Min(0.1f)] public float phaseDuration = 1f;
        [Min(0f), Tooltip("Time between two phases (from the start of the previous one).")] public float phaseCooldown = 4f;

        [Header("Blink")]
        [Min(1f)] public float blinkInterval = 6f;
        [Min(0f)] public float blinkMinDistance = 12f;
        public Vector2 blinkDistanceFromTarget = new(3f, 5f);
        public GameObject blinkVfx;

        public override void OnServerDamaged(EliteRuntime elite, in DamageInfo info)
        {
            if (Time.time < elite.Cooldown)
                return;
            elite.Cooldown = Time.time + phaseCooldown;
            elite.Actor.Phase(phaseDuration);
        }

        public override void ServerTick(EliteRuntime elite)
        {
            if (Time.time < elite.Timer)
                return;
            elite.Timer = Time.time + blinkInterval;
            var target = elite.Brain != null ? elite.Brain.Target : null;
            var agent = elite.Agent;
            if (target == null || agent == null || !agent.enabled || !agent.isOnNavMesh)
                return;
            Vector3 targetPosition = target.transform.position;
            if ((targetPosition - elite.transform.position).sqrMagnitude < blinkMinDistance * blinkMinDistance)
                return;

            for (int i = 0; i < 6; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(blinkDistanceFromTarget.x, blinkDistanceFromTarget.y);
                if (!NavMesh.SamplePosition(targetPosition + new Vector3(dir.x, 0f, dir.y), out var hit, 2f, NavMesh.AllAreas))
                    continue;
                VfxNetwork.Spawn(blinkVfx, elite.transform.position, Quaternion.identity);
                agent.Warp(hit.position);
                VfxNetwork.Spawn(blinkVfx, hit.position, Quaternion.identity);
                return;
            }
        }
    }
}
