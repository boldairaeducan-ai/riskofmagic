using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>
    /// Pierre de première taille: the first direct hit a player lands on each enemy deals bonus damage. Damage over
    /// time and item damage (proc coefficient 0) neither benefit nor use up the bonus.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Items/First Strike", fileName = "Item_FirstStrike")]
    public class FirstStrikeItem : ItemDefinition
    {
        [Min(0f)] public float firstStackBonus = 0.6f;
        [Min(0f)] public float extraStackBonus = 0.4f;

        // Victim → players that already hit it (dies with the victim).
        private static readonly ConditionalWeakTable<Health, HashSet<ulong>> struck = new();

        public override float ModifyOutgoingDamage(PlayerAvatar owner, Health victim, in DamageInfo info, float amount, int stacks)
        {
            if (stacks <= 0 || victim == null || owner == null || info.ProcCoefficient <= 0f)
                return amount;
            var hitters = struck.GetOrCreateValue(victim);
            if (!hitters.Add(owner.NetworkObjectId))
                return amount;
            return amount * (1f + firstStackBonus + extraStackBonus * (stacks - 1));
        }
    }
}
