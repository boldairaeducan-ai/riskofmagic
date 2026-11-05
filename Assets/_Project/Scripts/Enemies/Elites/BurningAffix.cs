using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Enemies.Elites
{
    /// <summary>Ardent: hits burn, leaves a fire trail on the ground.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Elites/Burning", fileName = "Elite_Burning")]
    public class BurningAffix : EliteAffix
    {
        public DotEffect burn;
        [Header("Trail")]
        public GameObject patchVfx;
        [Min(0.2f)] public float dropSpacing = 1.5f;
        [Min(0.5f), Tooltip("Keep equal to the patch VFX lifetime.")] public float trailDuration = 3f;
        [Min(0.2f)] public float trailRadius = 1.3f;
        public float trailDamage = 3f;
        [Min(0.1f)] public float tickInterval = 0.5f;

        public override void ServerTick(EliteRuntime elite)
        {
            float now = Time.time;
            Vector3 position = elite.transform.position;
            if ((position - elite.LastDrop).sqrMagnitude >= dropSpacing * dropSpacing)
            {
                elite.LastDrop = position;
                elite.Points.Add(new Vector4(position.x, position.y, position.z, now + trailDuration));
                VfxNetwork.Spawn(patchVfx, position, Quaternion.identity, trailRadius);
            }
            elite.Points.RemoveAll(p => p.w <= now);

            if (now < elite.Timer || elite.Points.Count == 0)
                return;
            elite.Timer = now + tickInterval;
            float sqr = trailRadius * trailRadius;
            foreach (var player in PlayerAvatar.All)
            {
                if (player == null || !player.IsStanding)
                    continue;
                Vector3 feet = player.transform.position;
                foreach (var point in elite.Points)
                {
                    Vector3 offset = feet - (Vector3)point;
                    if (Mathf.Abs(offset.y) > 1.5f)
                        continue;
                    offset.y = 0f;
                    if (offset.sqrMagnitude > sqr)
                        continue;
                    elite.HitPlayer(player, trailDamage, DamageType.Fire, Vector3.up);
                    break;
                }
            }
        }

        public override void OnServerDealtDamage(EliteRuntime elite, Health victim, in DamageInfo info)
        {
            if (burn != null)
                DotSystem.Apply(victim, burn, info);
        }
    }
}
