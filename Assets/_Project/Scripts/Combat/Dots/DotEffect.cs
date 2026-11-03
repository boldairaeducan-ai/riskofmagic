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
    }
}
