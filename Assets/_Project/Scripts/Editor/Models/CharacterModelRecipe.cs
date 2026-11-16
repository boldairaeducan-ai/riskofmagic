using System;
using System.Collections.Generic;
using RiskOfMagic.Characters;
using RiskOfMagic.Enemies;
using UnityEngine;

namespace RiskOfMagic.Editor.Models
{
    /// <summary>
    /// Everything needed to turn a third-party character model into a game model, rebuilt in one click
    /// (inspector button « Construire le modèle »): a prefab variant (weapons attached to humanoid bones, scaled to a
    /// height) and an Animator Controller generated from the clips below, then plugged into a character or enemy.
    /// The generated prefab and controller are outputs: change the recipe, not them. See docs/ANIMATION.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Character Model Recipe", fileName = "Model_New")]
    public class CharacterModelRecipe : ScriptableObject
    {
        public enum AttachAlignment
        {
            /// <summary>Position / rotation in the bone's own axes (rig dependent).</summary>
            Bone,
            /// <summary>
            /// Held in the fist, whatever the rig axes: the weapon's local Y goes through the fist like the thumb points
            /// (bow limbs, sword blade, staff), its local -Z points back along the arm (bow string toward the body).
            /// Position and rotation below are then extra offsets in that grip frame.
            /// </summary>
            HandGrip
        }

        [Serializable]
        public class Attachment
        {
            [Tooltip("Weapon / prop prefab.")] public GameObject prefab;
            public HumanBodyBones bone = HumanBodyBones.RightHand;
            [Tooltip("HandGrip: held in the fist (recommended for hands). Bone: raw bone axes.")]
            public AttachAlignment alignment = AttachAlignment.HandGrip;
            public Vector3 localPosition;
            public Vector3 localEulerAngles;
            public float scale = 1f;
        }

        [Serializable]
        public class ActionClip
        {
            [Tooltip("Played on this trigger: Cast / Attack (skills, see SkillDefinition.animatorTrigger; enemies: Attack).")]
            public string trigger = "Attack";
            [Tooltip("-1 = any. Players: SkillSlot (0 primary … 3 special). Enemies: AttackIndex (order of the EnemyAttack components).")]
            public int index = -1;
            public AnimationClip clip;
            [Tooltip("The clip lasts exactly the action: skill cast time (attack speed included, live) or enemy windup + recovery.")]
            public bool matchCastDuration = true;
            [Min(0.1f), Tooltip("Matched: 1 = the whole cast time, 2 = twice as fast. Otherwise: plain playback speed.")]
            public float speed = 1f;
        }

        [Header("Source")]
        [Tooltip("Model prefab of the asset pack (Rig: Humanoid).")] public GameObject sourceModel;
        [Min(0f), Tooltip("Height in meters (0 = as authored). Players ~1.8, ghoul 1.8.")] public float height = 1.8f;
        [Tooltip("Extra rotation of the model (if it doesn't face +Z).")] public Vector3 rotation;
        public List<Attachment> attachments = new();

        [Header("Output (generated)")]
        [Tooltip("Folder of the generated prefab and controller.")] public string outputFolder = "Assets/_Project/Prefabs/Models";
        [Tooltip("Plugged in its modelPrefab after the build.")] public CharacterDefinition targetCharacter;
        public EnemyDefinition targetEnemy;

        [Header("Locomotion (Speed parameter, m/s)")]
        public AnimationClip idle;
        public AnimationClip walk;
        [Min(0.1f)] public float walkSpeed = 3f;
        public AnimationClip run;
        [Min(0.1f)] public float runSpeed = 7f;

        [Header("Air (players)")]
        public AnimationClip jump;
        public AnimationClip fall;
        public AnimationClip land;

        [Header("Reactions")]
        public AnimationClip hit;
        public AnimationClip death;
        [Tooltip("Players: on the ground waiting for a revive (loop).")] public AnimationClip downed;
        public AnimationClip revive;
        [Tooltip("Players: equipment (F).")] public AnimationClip useEquipment;

        [Header("Actions (skills / attacks)")]
        [Tooltip("Played on the upper body only, so the legs keep walking.")] public bool actionsUpperBodyOnly = true;
        public List<ActionClip> actions = new();

        /// <summary>Without an idle clip there is no controller: the model is animated by code (ProceduralHumanoidMotion).</summary>
        public bool HasClips => idle != null;
    }
}
