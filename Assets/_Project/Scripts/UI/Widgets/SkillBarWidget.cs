using UnityEngine;

namespace RiskOfMagic.UI
{
    public class SkillBarWidget : HudWidget
    {
        [SerializeField] private SkillSlotWidget[] slots = new SkillSlotWidget[0];

        protected override void OnBind()
        {
            if (Player.Skills != null)
                Player.Skills.LoadoutChanged += Refresh;
            foreach (var slot in slots)
                if (slot != null)
                    slot.Bind(Player);
        }

        protected override void OnUnbind()
        {
            if (Player.Skills != null)
                Player.Skills.LoadoutChanged -= Refresh;
            foreach (var slot in slots)
                if (slot != null)
                    slot.Unbind();
        }

        private void Refresh()
        {
            foreach (var slot in slots)
                if (slot != null)
                    slot.RefreshDefinition();
        }
    }
}
