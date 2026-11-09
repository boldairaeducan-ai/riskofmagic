using RiskOfMagic.Combat;
using UnityEngine;

namespace RiskOfMagic.Feedback
{
    /// <summary>
    /// Tuning of enemy hit reactions (Resources/HitFeedback.asset, created once by the builder, never overwritten).
    /// Missing asset = these defaults.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Hit Feedback", fileName = "HitFeedback")]
    public class HitFeedbackConfig : ScriptableObject
    {
        public const string ResourcePath = "HitFeedback";

        [Header("Flash")]
        public Color flashColor = new(1f, 0.97f, 0.9f);
        public Color criticalFlashColor = new(1f, 0.78f, 0.25f);
        [Min(0.01f)] public float flashDuration = 0.09f;
        [Min(0.01f)] public float killFlashDuration = 0.22f;

        [Header("Squash (scale punch)")]
        [Tooltip("Squash at a hit worth 0% of max health / at 25% and more.")] public Vector2 squash = new(0.08f, 0.22f);
        public float criticalSquashMultiplier = 1.4f;
        public float squashFrequency = 28f;
        public float squashDamping = 11f;

        [Header("Hit-stop (visual freeze of the enemy)")]
        [Tooltip("Seconds, light hit / heavy hit (25%+ of max health).")] public Vector2 hitStop = new(0.04f, 0.09f);
        public float criticalHitStopBonus = 0.02f;
        public float killHitStop = 0.11f;
        public float hitStopJitter = 0.04f;

        [Header("Camera (local attacker)")]
        [Tooltip("Trauma for a heavy hit (25%+ of max health) or a critical.")] public float heavyHitTrauma = 0.08f;
        public float killTrauma = 0.14f;
        public float bossKillTrauma = 0.45f;

        [Header("Sparks")]
        [Tooltip("Additive particle material (URP Particles/Unlit). Assigned by the builder.")] public Material sparkMaterial;
        public Vector2Int sparkCount = new(6, 14);
        public int criticalSparkBonus = 8;
        public int killSparkCount = 34;
        [Tooltip("HDR brightness of the spark material, set when the builder creates it (above the bloom threshold).")] public float sparkGlow = 2.2f;
        public Color physical = new(1f, 0.85f, 0.6f);
        public Color fire = new(1f, 0.5f, 0.15f);
        public Color frost = new(0.55f, 0.9f, 1f);
        public Color lightning = new(0.75f, 0.85f, 1f);
        public Color arcane = new(0.8f, 0.45f, 1f);
        public Color poison = new(0.5f, 1f, 0.35f);
        public Color bleed = new(0.9f, 0.1f, 0.1f);
        public Color holy = new(1f, 0.93f, 0.55f);
        public Color critical = new(1f, 0.8f, 0.25f);

        [Header("Death")]
        [Tooltip("Meters the body is thrown back.")] public float killPush = 0.6f;
        [Tooltip("Degrees the body topples (placeholder bodies; animated models only get the push).")] public float killTopple = 80f;
        public float killRecoilDuration = 0.3f;
        [Tooltip("Shrinks out during the last seconds before despawn.")] public float shrinkOutDuration = 0.35f;

        public Color GetColor(DamageType type) => type switch
        {
            DamageType.Fire => fire,
            DamageType.Frost => frost,
            DamageType.Lightning => lightning,
            DamageType.Arcane => arcane,
            DamageType.Poison => poison,
            DamageType.Bleed => bleed,
            DamageType.Holy => holy,
            _ => physical
        };

        private static HitFeedbackConfig instance;

        public static HitFeedbackConfig Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<HitFeedbackConfig>(ResourcePath);
                    if (instance == null)
                        instance = CreateInstance<HitFeedbackConfig>();
                }
                return instance;
            }
        }
    }
}
