using RiskOfMagic.Run;
using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Current stage objective (teleporter) with its charge bar.</summary>
    public class ObjectiveWidget : HudWidget
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup chargeGroup;
        [SerializeField] private RectTransform chargeFill;

        protected override void Tick()
        {
            var teleporter = Teleporter.Instance;
            string text = string.Empty;
            bool showCharge = false;

            if (teleporter != null)
            {
                switch (teleporter.State)
                {
                    case TeleporterState.Idle:
                        text = "Trouvez et activez le téléporteur";
                        break;
                    case TeleporterState.Charging:
                        showCharge = true;
                        text = $"Chargement du téléporteur : {Mathf.FloorToInt(teleporter.Charge01 * 100f)}%";
                        if (!teleporter.IsInside(Player.transform.position))
                            text += "\n<size=80%>Restez dans la zone pour charger</size>";
                        break;
                    case TeleporterState.Charged:
                        showCharge = true;
                        text = "Vainquez le boss";
                        break;
                    case TeleporterState.Ready:
                        text = "Téléporteur prêt : rejoignez-le pour partir";
                        break;
                }
            }

            if (label != null && label.text != text)
                label.text = text;
            if (chargeGroup != null)
                chargeGroup.alpha = showCharge ? 1f : 0f;
            if (showCharge)
                SetFill(chargeFill, teleporter.Charge01);
        }
    }
}
