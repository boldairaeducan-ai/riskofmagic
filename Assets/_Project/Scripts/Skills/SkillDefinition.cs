using RiskOfMagic.Characters;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills
{
    public enum VfxAnchor : byte
    {
        None,
        Caster,
        Origin,
        Target
    }

    /// <summary>
    /// Data + behaviour of a skill. Definitions are shared assets: never store per-player state here,
    /// use SkillSlotState instead.
    /// Execution: owner (prediction, movement) → server (damage, authoritative effects) → other peers (feedback).
    /// </summary>
    public abstract class SkillDefinition : ScriptableObject
    {
        [Header("Info")]
        public string displayName = "Skill";
        [TextArea] public string description;
        public Sprite icon;

        [Header("Activation")]
        public SkillActivationMode activationMode = SkillActivationMode.Press;
        [Min(0f)] public float cooldown = 3f;
        [Min(1)] public int maxStock = 1;
        [Min(0f), Tooltip("Time during which other skills can't start.")] public float castDuration = 0.2f;
        public bool scalesWithAttackSpeed;
        public bool canCastWhileBusy;
        public bool keepsSprint;
        [Min(0.05f)] public float maxChargeTime = 1.5f;

        [Header("Feedback")]
        public GameObject castVfx;
        public VfxAnchor castVfxAnchor = VfxAnchor.Caster;
        [Min(0f)] public float castVfxScale = 1f;

        public float GetCastDuration(CharacterStats stats)
            => scalesWithAttackSpeed ? castDuration / Mathf.Max(0.01f, stats.AttackSpeed) : castDuration;

        /// <summary>Owner side gate (e.g. needs a target).</summary>
        public virtual bool CanActivate(SkillContext context, SkillSlotState state) => true;

        /// <summary>Owner side: finalize the activation sent to the server.</summary>
        public virtual void PrepareActivation(SkillContext context, SkillSlotState state, ref SkillActivation activation) { }

        public virtual void OnOwnerExecute(SkillContext context, in SkillActivation activation) => SpawnCastVfx(context, activation);

        public virtual void OnServerExecute(SkillContext context, in SkillActivation activation) { }

        public virtual void OnRemoteExecute(SkillContext context, in SkillActivation activation) => SpawnCastVfx(context, activation);

        protected void SpawnCastVfx(SkillContext context, in SkillActivation activation)
        {
            if (castVfx == null || castVfxAnchor == VfxAnchor.None)
                return;
            Vector3 position = castVfxAnchor switch
            {
                VfxAnchor.Origin => activation.Origin,
                VfxAnchor.Target => activation.Target,
                _ => context.Transform.position
            };
            VfxUtility.Spawn(castVfx, position, Quaternion.LookRotation(activation.Direction.sqrMagnitude > 0f ? activation.Direction : Vector3.forward), castVfxScale);
        }
    }
}
