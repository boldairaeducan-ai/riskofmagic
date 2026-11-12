using UnityEngine;

namespace RiskOfMagic.Combat
{
    /// <summary>
    /// Damage over time applied on hit (projectiles, areas, dashes). Each application is one stack;
    /// tick damage is a fraction of the applying hit, so it scales with stats and items.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Damage Over Time", fileName = "Dot")]
    public class DotEffect : ScriptableObject
    {
        public DamageType damageType = DamageType.Poison;
        [Min(0f), Tooltip("Damage per tick as a fraction of the applying hit (0.25 = 25%).")] public float tickDamageFraction = 0.25f;
        [Min(0.05f)] public float tickInterval = 0.5f;
        [Min(0.1f)] public float duration = 4f;
        [Min(1), Tooltip("Stacks per target; beyond that the oldest stack is refreshed.")] public int maxStacks = 10;

        [Header("% max health mode (poison): used when > 0, replaces tickDamageFraction")]
        [Min(0f), Tooltip("Fraction of the target's max health per second, per stack (0.006 = 0.6 %/s).")] public float maxHealthPerSecond;
        [Range(0f, 1f), Tooltip("Bosses and elites take this fraction of it.")] public float bossAndEliteFactor = 0.5f;
        [Min(0f), Tooltip("On players (enemy poison): max health per second per stack.")] public float playerMaxHealthPerSecond = 0.005f;
        [Min(1)] public int playerMaxStacks = 4;
        [Tooltip("Each new application refreshes the duration of every stack (keep attacking to keep it going).")]
        public bool refreshAllStacks;
    }
}
