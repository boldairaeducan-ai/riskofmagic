using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Each kill adds a Warhorn stack (speed bonus, see StatusEffects); every stack is lost when hit.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Items/War Horn", fileName = "Item_WarHorn")]
    public class WarHornItem : ItemDefinition
    {
        [Min(1)] public int maxStacksFirst = 5;
        [Min(0)] public int maxStacksPerExtra = 1;

        public override void OnKillServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
        {
            if (owner.Status != null)
                owner.Status.Apply(StatusType.Warhorn, 0f, 1, maxStacksFirst + maxStacksPerExtra * (stacks - 1));
        }

        public override void OnOwnerDamagedServer(PlayerAvatar owner, in DamageInfo info, int stacks)
        {
            if (owner.Status != null && !info.IgnoreModifiers)
                owner.Status.Clear(StatusType.Warhorn);
        }
    }
}
