using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Binds every HudWidget below it to the local player and shows the gameplay layer while one exists.</summary>
    public class HudController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup gameplayGroup;

        private HudWidget[] widgets;
        private PlayerAvatar bound;

        private void Awake()
        {
            widgets = GetComponentsInChildren<HudWidget>(true);
            SetGameplayVisible(false);
        }

        private void Update()
        {
            var local = PlayerAvatar.Local;
            if (local == bound)
                return;

            if (bound != null)
                foreach (var widget in widgets)
                    widget.Unbind();

            bound = local;
            if (bound != null)
                foreach (var widget in widgets)
                    widget.Bind(bound);

            SetGameplayVisible(bound != null);
        }

        private void SetGameplayVisible(bool visible)
        {
            if (gameplayGroup == null)
                return;
            gameplayGroup.alpha = visible ? 1f : 0f;
            gameplayGroup.blocksRaycasts = false;
        }
    }
}
