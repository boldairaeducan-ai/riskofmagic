using System.Collections;
using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>Arrows, bolts, orbs. Supports fan volleys and charged shots.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Projectile", fileName = "Skill_Projectile")]
    public class ProjectileSkill : SkillDefinition
    {
        [Header("Projectile")]
        public ProjectileDefinition projectile;
        public DamageType damageType = DamageType.Physical;
        [Min(0f)] public float damageCoefficient = 1.5f;
        [Min(1)] public int count = 1;
        [Min(0f), Tooltip("Horizontal fan angle for count > 1.")] public float spreadAngle;
        [Tooltip("Relative to the camera pivot.")] public Vector3 muzzleOffset = new(0.25f, -0.2f, 0.5f);
        [Min(1), Tooltip("Shots fired in sequence (each one fires `count` projectiles).")] public int burstCount = 1;
        [Min(0.01f)] public float burstInterval = 0.08f;

        [Header("Charge (Charge mode)")]
        [Min(0f)] public float minChargeCoefficient = 2f;
        [Min(0f)] public float maxChargeCoefficient = 6f;
        [Min(0.1f)] public float minChargeScale = 1f;
        [Min(0.1f)] public float maxChargeScale = 1f;

        public override float MaxRange => projectile != null ? projectile.maxRange : 0f;

        public override void PrepareActivation(SkillContext context, SkillSlotState state, ref SkillActivation activation)
        {
            Vector3 muzzle = context.Look.CameraPivot.TransformPoint(muzzleOffset);
            Vector3 toTarget = activation.Target - muzzle;
            activation.Origin = muzzle;
            if (toTarget.sqrMagnitude > 1f)
                activation.Direction = toTarget.normalized;
        }

        public override void OnOwnerExecute(SkillContext context, in SkillActivation activation)
        {
            base.OnOwnerExecute(context, activation);
            if (context.IsServer)
                return;
            float scale = GetScale(activation);
            for (int i = 0; i < count; i++)
                context.PredictProjectile(projectile, activation.Origin, GetDirection(activation.Direction, i), scale);
            if (burstCount > 1)
                context.Run(PredictBurst(context, scale));
        }

        // Follow-up shots of a burst use the current aim (owner prediction / server).
        private IEnumerator PredictBurst(SkillContext context, float scale)
        {
            for (int shot = 1; shot < burstCount; shot++)
            {
                yield return new WaitForSeconds(burstInterval);
                Vector3 muzzle = context.Look.CameraPivot.TransformPoint(muzzleOffset);
                Vector3 forward = context.Look.CameraPivot.forward;
                for (int i = 0; i < count; i++)
                    context.PredictProjectile(projectile, muzzle, GetDirection(forward, i), scale);
            }
        }

        private IEnumerator FireBurst(SkillContext context, Vector3 direction, float coefficient, float scale, float yawAtCast)
        {
            for (int shot = 1; shot < burstCount; shot++)
            {
                yield return new WaitForSeconds(burstInterval);
                if (!context.Avatar.IsAlive)
                    yield break;
                Vector3 muzzle = context.Look.CameraPivot.TransformPoint(muzzleOffset);
                // Yaw follows the replicated body rotation; pitch stays the one of the activation.
                Vector3 aim = Quaternion.Euler(0f, context.Transform.eulerAngles.y - yawAtCast, 0f) * direction;
                for (int i = 0; i < count; i++)
                    context.FireProjectile(projectile, muzzle, GetDirection(aim, i), coefficient, damageType, scale);
            }
        }


        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            float coefficient = activationMode == SkillActivationMode.Charge
                ? Mathf.Lerp(minChargeCoefficient, maxChargeCoefficient, activation.Charge)
                : damageCoefficient;
            float scale = GetScale(activation);
            for (int i = 0; i < count; i++)
                context.FireProjectile(projectile, activation.Origin, GetDirection(activation.Direction, i), coefficient, damageType, scale);
            if (burstCount > 1)
                context.Run(FireBurst(context, activation.Direction, coefficient, scale, context.Transform.eulerAngles.y));
        }

        private float GetScale(in SkillActivation activation)
            => activationMode == SkillActivationMode.Charge ? Mathf.Lerp(minChargeScale, maxChargeScale, activation.Charge) : 1f;

        private Vector3 GetDirection(Vector3 forward, int index)
        {
            if (count <= 1 || spreadAngle <= 0f)
                return forward;
            float angle = Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, index / (float)(count - 1));
            return Quaternion.AngleAxis(angle, Vector3.up) * forward;
        }
    }
}
