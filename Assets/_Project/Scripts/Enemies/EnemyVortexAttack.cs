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
        [SerializeField, Min(1)] private int riftsPerPlayer = 1;
        [SerializeField] private Vector2 offsetFromPlayer = new(3f, 6f);
        [SerializeField, Min(1f)] private float pullRadius = 9f;
        [SerializeField, Min(0.5f)] private float coreRadius = 2f;
        [SerializeField, Tooltip("Pull speed (m/s) added per second at the edge, stronger near the core.")] private float pullStrength = 9f;
        [SerializeField] private float coreDamagePerTick = 6f;
        [SerializeField] private DamageType damageType = DamageType.Arcane;
        [SerializeField, Min(0.5f)] private float duration = 4f;
        [SerializeField, Min(0.1f)] private float tick = 0.25f;
        [SerializeField, Min(0f)] private float windup = 0.9f;
        [SerializeField, Min(0f)] private float recovery = 0.5f;
        [SerializeField] private GameObject telegraphVfx;
        [SerializeField, Tooltip("Pulsed at each rift, scaled by the core radius.")] private GameObject riftVfx;
        [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;

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
