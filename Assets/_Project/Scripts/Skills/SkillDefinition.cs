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

        [Header("Owner camera feedback")]
        [Tooltip("Field of view change (degrees) on cast: positive widens (dashes), negative narrows.")] public float ownerFovKick;
        [Min(0f)] public float ownerFovKickDuration = 0.25f;
        [Range(0f, 1f), Tooltip("Camera shake on cast, e.g. slams.")] public float ownerCameraTrauma;

        /// <summary>Max reach in meters (0 = melee/unlimited). Used by the crosshair "out of range" feedback.</summary>
        public virtual float MaxRange => 0f;

        public float GetCastDuration(CharacterStats stats)
            => scalesWithAttackSpeed ? castDuration / Mathf.Max(0.01f, stats.AttackSpeed) : castDuration;

        /// <summary>Owner side gate (e.g. needs a target).</summary>
        public virtual bool CanActivate(SkillContext context, SkillSlotState state) => true;

        /// <summary>Owner side: a Charge-mode skill starts charging.</summary>
        public virtual void OnChargeStarted(SkillContext context, SkillSlotState state) { }

        /// <summary>Owner side: every frame while charging (charge in [0,1]).</summary>
        public virtual void OnChargeTick(SkillContext context, SkillSlotState state, float charge) { }

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
