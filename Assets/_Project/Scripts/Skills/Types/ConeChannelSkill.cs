using System.Collections;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>
    /// Channelled cone (Souffle enflammé): damage ticks in a cone in front of the caster for a duration, following the
    /// caster's yaw; each tick can add a stack of damage over time. The caster is slowed (Channeling status).
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Cone Channel", fileName = "Skill_ConeChannel")]
    public class ConeChannelSkill : SkillDefinition
    {
        [Min(0.1f)] public float duration = 2.5f;
        [Min(1)] public int ticks = 10;
        [Min(1f)] public float range = 10f;
        [Range(1f, 90f), Tooltip("Half-angle of the cone.")] public float coneAngle = 30f;
        [Min(0f)] public float damageCoefficientPerTick = 0.45f;
        public DamageType damageType = DamageType.Fire;
        [Range(0f, 1f), Tooltip("Item proc chance multiplier of each tick.")] public float procCoefficient = 0.5f;
        [Tooltip("Applied on every tick (one stack each).")] public DotEffect dot;

        [Header("Feedback")]
        [Tooltip("Spawned along the cone on each tick (VfxNetwork).")] public GameObject tickVfx;
        [Min(0.05f)] public float tickVfxScale = 0.35f;
        [Tooltip("Continuous effect (jet) following the caster for the whole channel, local on every peer. +Z = jet direction.")]
        public GameObject channelVfx;
        [Tooltip("Jet start in the first-person camera space (owner).")] public Vector3 firstPersonOffset = new(0.15f, -0.35f, 0.6f);
        [Tooltip("Jet start relative to the head pivot, along the body, for other players.")] public Vector3 thirdPersonOffset = new(0f, -0.15f, 0.45f);

        private static readonly List<Health> found = new();

        public override float MaxRange => range;

        public override void OnOwnerExecute(SkillContext context, in SkillActivation activation)
        {
            base.OnOwnerExecute(context, activation);
            StartChannelVfx(context, activation, firstPerson: true);
        }

        public override void OnRemoteExecute(SkillContext context, in SkillActivation activation)
        {
            base.OnRemoteExecute(context, activation);
            StartChannelVfx(context, activation, firstPerson: false);
        }

        /// <summary>
        /// Owner: follows the camera (aim pitch included). Others: the head pivot with the body yaw and the pitch of
        /// the cast (the camera pitch isn't replicated).
        /// </summary>
        private void StartChannelVfx(SkillContext context, in SkillActivation activation, bool firstPerson)
        {
            if (channelVfx == null || context.Look == null || context.Look.CameraPivot == null)
                return;
            var avatar = context.Avatar;
            var pivot = context.Look.CameraPivot;
            var effect = VfxUtility.Spawn(channelVfx, pivot.position, pivot.rotation);
            if (firstPerson)
            {
                ChannelEffect.Begin(effect, pivot, firstPersonOffset, null, () => avatar != null && avatar.IsStanding, duration);
                return;
            }
            Vector3 aim = activation.Direction.sqrMagnitude > 0.001f ? activation.Direction.normalized : avatar.transform.forward;
            float pitch = -Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f)) * Mathf.Rad2Deg;
            var body = avatar.transform;
            Vector3 offset = body.InverseTransformPoint(pivot.position) + thirdPersonOffset; // pivot height, body space
            ChannelEffect.Begin(effect, body, offset, () => body != null ? body.rotation * Quaternion.Euler(pitch, 0f, 0f) : Quaternion.identity,
                () => avatar != null && avatar.IsStanding, duration);
        }

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            if (context.Avatar.Status != null)
                context.Avatar.Status.Apply(StatusType.Channeling, duration);
            context.Run(Channel(context, activation.Direction, context.Transform.eulerAngles.y));
        }

        private IEnumerator Channel(SkillContext context, Vector3 direction, float yawAtCast)
        {
            float interval = duration / ticks;
            var wait = new WaitForSeconds(interval);
            for (int i = 0; i < ticks; i++)
            {
                if (!context.Avatar.IsStanding)
                    yield break;
                Vector3 aim = Quaternion.Euler(0f, context.Transform.eulerAngles.y - yawAtCast, 0f) * direction;
                Tick(context, aim);
                yield return wait;
            }
        }

        private void Tick(SkillContext context, Vector3 aim)
        {
            Vector3 flat = new(aim.x, 0f, aim.z);
            flat = flat.sqrMagnitude > 0.001f ? flat.normalized : context.Transform.forward;
            Vector3 origin = context.Transform.position;

            TargetFinder.FindHostiles(context.Look.CameraPivot.position, range, context.Team, found, context.HitMask);
            foreach (var health in found)
            {
                Vector3 toTarget = health.transform.position - origin;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f && Vector3.Angle(flat, toTarget) > coneAngle)
                    continue;
                var info = context.CreateDamage(damageCoefficientPerTick, damageType, TargetFinder.Center(health), flat);
                info.ProcCoefficient = procCoefficient;
                health.ApplyDamage(info);
                if (dot != null)
                    DotSystem.Apply(health, dot, info);
            }
            if (tickVfx != null)
                for (int i = 1; i <= 3; i++)
                    VfxNetwork.Spawn(tickVfx, origin + Vector3.up + flat * (range * i / 3.5f), Quaternion.LookRotation(flat), tickVfxScale * i);
        }
    }
}
