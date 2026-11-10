using System;
using UnityEngine;

namespace RiskOfMagic.Animation
{
    /// <summary>What the hands do when a skill fires (procedural first-person animation).</summary>
    public enum ViewMotion : byte
    {
        None,
        /// <summary>Horizontal slash, mirrored on every other hit; the 3rd combo step strikes from above.</summary>
        Swing,
        Overhead,
        /// <summary>Wide circular sweep.</summary>
        Spin,
        Thrust,
        /// <summary>Thrust with the main then the off hand (twin daggers, claws).</summary>
        ThrustAlternate,
        /// <summary>Quick draw and release (off hand = bow, main hand = arrow).</summary>
        BowShot,
        Cast,
        CastBoth,
        /// <summary>The main-hand weapon is thrown and comes back in hand.</summary>
        Throw,
        /// <summary>Off hand (shield) raised in front.</summary>
        Guard,
        /// <summary>Both hands drop out of view (dash, roll).</summary>
        Lower,
        /// <summary>Both hands slam down.</summary>
        Slam
    }

    [Serializable]
    public class ViewHand
    {
        [Tooltip("Weapon / object held (empty = bare hand).")] public GameObject prefab;
        [Tooltip("Rest pose in camera space (x right, y up, z forward).")] public Vector3 restPosition = new(0.28f, -0.26f, 0.5f);
        public Vector3 restEuler;
        public float scale = 1f;
        [Tooltip("Simple glove shown at the grip.")] public bool showGlove = true;
        public Vector3 gloveOffset;
    }

    /// <summary>
    /// Procedural first-person view of a character: what each hand holds, where, and the motion of each skill slot.
    /// Created once per character by the builder (never overwritten): tune it freely, even in Play mode.
    /// Used when the character has no viewModelPrefab (real animated arms take over when one is set).
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/View Model", fileName = "ViewModel")]
    public class ViewModelDefinition : ScriptableObject
    {
        public ViewHand mainHand = new();
        public ViewHand offHand = new() { restPosition = new Vector3(-0.28f, -0.26f, 0.5f) };
        public Color gloveColor = new(0.35f, 0.25f, 0.18f);
        [Min(0.02f)] public float gloveSize = 0.09f;

        [Header("Skill motions (primary, secondary, utility, special)")]
        public ViewMotion[] motions = { ViewMotion.Swing, ViewMotion.Cast, ViewMotion.Lower, ViewMotion.Slam };
        public ViewMotion[] alternativeMotions = { ViewMotion.Swing, ViewMotion.Cast, ViewMotion.Lower, ViewMotion.Slam };
        [Tooltip("Charged skills: the main hand pulls back with the charge (bow draw, wind-up).")] public bool drawWhileCharging = true;

        [Header("Feel")]
        [Min(0.1f)] public float motionSpeed = 1f;
        [Min(0f)] public float motionAmplitude = 1f;
        [Min(0f)] public float bobAmount = 0.012f;
        [Min(0f)] public float swayAmount = 0.02f;
        [Min(0f)] public float sprintLower = 0.07f;

        public ViewMotion GetMotion(int slot, bool alternative)
        {
            var list = alternative ? alternativeMotions : motions;
            return list != null && slot >= 0 && slot < list.Length ? list[slot] : ViewMotion.None;
        }
    }
}
