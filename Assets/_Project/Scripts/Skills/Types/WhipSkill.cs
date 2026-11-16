using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>
    /// Instant mid-range lash (Fouet de liane): hits the first enemy along the aim within range (level geometry stops
    /// it), optionally applies a damage over time. A line effect goes from the caster's hand to the end point.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Whip", fileName = "Skill_Whip")]
    public class WhipSkill : SkillDefinition
    {
        [Min(1f)] public float range = 14f;
        [Min(0.05f)] public float radius = 0.45f;
        [Min(0f)] public float damageCoefficient = 1.2f;
        public DamageType damageType = DamageType.Physical;
        [Tooltip("Optional damage over time applied to the enemy hit.")] public DotEffect dot;
        [Tooltip("Level geometry stopping the lash.")] public LayerMask obstacleMask = Physics.DefaultRaycastLayers;

        [Header("Feedback")]
        [Tooltip("Line effect (VfxNetwork, in the GameCatalog effects) from the hand to the end point.")] public GameObject lashVfx;
        public GameObject hitVfx;

        private static readonly RaycastHit[] hits = new RaycastHit[32];
        private static readonly List<RaycastHit> sorted = new();

        public override float MaxRange => range;

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            Vector3 origin = context.Look.CameraPivot.position;
            Vector3 direction = activation.Direction.sqrMagnitude > 0.001f ? activation.Direction.normalized : context.Transform.forward;
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, hits, range, obstacleMask | context.HitMask, QueryTriggerInteraction.Ignore);
            sorted.Clear();
            for (int i = 0; i < count; i++)
                sorted.Add(hits[i]);
            sorted.Sort((a, b) => a.distance.CompareTo(b.distance));

            Vector3 end = origin + direction * range;
            foreach (var hit in sorted)
            {
                if (context.IsSelf(hit.collider))
                    continue;
                if (CombatUtility.TryGetDamageable(hit.collider, out var target))
                {
                    if (target.Team == context.Team || !target.IsAlive)
                        continue;
                    Vector3 point = hit.distance > 0f ? hit.point : TargetFinder.Center(target.Health);
                    var info = context.CreateDamage(damageCoefficient, damageType, point, direction);
                    target.ApplyDamage(info);
                    if (dot != null)
                        DotSystem.Apply(target.Health, dot, info);
                    VfxNetwork.Spawn(hitVfx, point, Quaternion.LookRotation(-direction), 1f);
                    end = point;
                    break;
                }
                if (hit.collider.isTrigger)
                    continue;
                end = hit.distance > 0f ? hit.point : origin; // wall
                break;
            }

            Vector3 hand = origin + context.Transform.right * 0.3f - Vector3.up * 0.3f + direction * 0.4f;
            if (lashVfx != null)
                VfxNetwork.SpawnLine(lashVfx, new[] { hand, Vector3.Lerp(hand, end, 0.5f) + Vector3.up * 0.25f, end });
        }
    }
}
