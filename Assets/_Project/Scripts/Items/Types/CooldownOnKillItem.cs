using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Cœur d'ambre: each kill shortens the remaining cooldown of every skill but the primary.</summary>
    public class CooldownOnKillItem : ItemDefinition
    {
        [Min(0f)] public float secondsFirstStack = 0.5f;
        [Min(0f)] public float secondsPerExtraStack = 0.5f;

        public override void OnKillServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
        {
            if (owner.Skills != null)
                owner.Skills.ReduceCooldowns(Stacked(secondsFirstStack, secondsPerExtraStack, stacks));
        }
    }
}
