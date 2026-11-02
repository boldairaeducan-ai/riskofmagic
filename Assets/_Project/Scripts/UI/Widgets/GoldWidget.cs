using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    public class GoldWidget : HudWidget
    {
        [SerializeField] private TMP_Text label;

        protected override void OnBind()
        {
            Player.Wallet.GoldChanged += OnGoldChanged;
            OnGoldChanged(0, Player.Wallet.Gold);
        }

        protected override void OnUnbind() => Player.Wallet.GoldChanged -= OnGoldChanged;

        private void OnGoldChanged(uint previous, uint current)
        {
            if (label != null)
                label.text = current.ToString();
        }
    }
}
