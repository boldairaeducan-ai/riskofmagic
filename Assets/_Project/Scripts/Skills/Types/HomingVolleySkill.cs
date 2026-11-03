using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>
    /// Charged volley of homing projectiles: the charge sets the number of missiles (1 → maxTargets).
    /// Targets are the enemies closest to the crosshair, assigned round-robin (one enemy can take several).
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Homing Volley", fileName = "Skill_HomingVolley")]
    public class HomingVolleySkill : SkillDefinition
    {
        [Header("Projectiles")]
        public ProjectileDefinition projectile;
        public DamageType damageType = DamageType.Arcane;
        [Min(0f), Tooltip("Per missile.")] public float damageCoefficient = 1.1f;
        [Min(1)] public int maxTargets = 6;
        [Min(0f), Tooltip("Launch fan (degrees) before homing kicks in.")] public float launchSpread = 30f;
        public Vector3 muzzleOffset = new(0.25f, -0.2f, 0.5f);

        [Header("Targeting")]
        [Min(0f)] public float targetRange = 45f;
        [Range(1f, 90f), Tooltip("Half-angle of the targeting cone around the crosshair.")] public float targetConeAngle = 25f;
        public LayerMask sightMask = Physics.DefaultRaycastLayers;

        private static readonly List<Health> candidates = new();

        public int GetMissileCount(float charge) => 1 + Mathf.RoundToInt(Mathf.Clamp01(charge) * (maxTargets - 1));

        public override void PrepareActivation(SkillContext context, SkillSlotState state, ref SkillActivation activation)
        {
            activation.Origin = context.Look.CameraPivot.TransformPoint(muzzleOffset);
        }

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            int count = GetMissileCount(activation.Charge);
            FindTargets(context, context.Look.CameraPivot.position, activation.Direction);

            for (int i = 0; i < count; i++)
            {
                var target = candidates.Count > 0 ? candidates[i % candidates.Count] : null;
                Vector3 direction = LaunchDirection(activation.Direction, i, count);
                ProjectileNetwork.FireServer(projectile, activation.Origin, direction, 1f, context.CreateDamage(damageCoefficient, damageType),
                    context.NetworkObject, ProjectileNetwork.NoClient, -1f, target != null ? target.NetworkObject : null);
            }
        }

        private void FindTargets(SkillContext context, Vector3 eye, Vector3 aim)
        {
            TargetFinder.FindHostiles(eye, targetRange, context.Team, candidates, context.HitMask);
            candidates.RemoveAll(health =>
            {
                Vector3 toTarget = TargetFinder.Center(health) - eye;
                if (Vector3.Angle(aim, toTarget) > targetConeAngle)
                    return true;
                return Physics.Linecast(eye, TargetFinder.Center(health), out var hit, sightMask, QueryTriggerInteraction.Ignore)
                       && !hit.collider.transform.IsChildOf(health.transform) && !context.IsSelf(hit.collider);
            });
            candidates.Sort((a, b) => Vector3.Angle(aim, TargetFinder.Center(a) - eye).CompareTo(Vector3.Angle(aim, TargetFinder.Center(b) - eye)));
        }

        private Vector3 LaunchDirection(Vector3 aim, int index, int count)
        {
            if (count <= 1)
                return aim;
            Vector3 right = Vector3.Cross(Vector3.up, aim).normalized;
            float yaw = Mathf.Lerp(-launchSpread, launchSpread, index / (float)(count - 1));
            return Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(-12f, right) * aim;
        }
    }
}
