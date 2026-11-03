using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RiskOfMagic.Audio
{
    /// <summary>
    /// Click/hover sounds of a Selectable. Added automatically to every active Selectable (GameAudioEvents);
    /// add it by hand to override the sounds or mute a control.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class UiSoundTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISubmitHandler, ISelectHandler
    {
        [SerializeField] private bool muted;
        [SerializeField, Tooltip("Empty = AudioConfig.uiClick.")] private SoundEvent clickOverride;
        [SerializeField, Tooltip("Empty = AudioConfig.uiHover.")] private SoundEvent hoverOverride;

        private Selectable selectable;
        private bool pointerInside;

        private void Awake() => selectable = GetComponent<Selectable>();

        private bool CanPlay => !muted && selectable != null && selectable.IsInteractable();

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            Hover();
        }

        public void OnPointerExit(PointerEventData eventData) => pointerInside = false;

        public void OnSelect(BaseEventData eventData)
        {
            if (!pointerInside)
                Hover(); // gamepad/keyboard navigation (a click also selects: already covered by enter)
        }

        private void OnDisable() => pointerInside = false;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Click();
        }

        public void OnSubmit(BaseEventData eventData) => Click();

        private void Hover()
        {
            if (CanPlay)
                AudioService.Play2D(AudioService.Pick(hoverOverride, AudioService.Config != null ? AudioService.Config.uiHover : null));
        }

        private void Click()
        {
            if (CanPlay)
                AudioService.Play2D(AudioService.Pick(clickOverride, AudioService.Config != null ? AudioService.Config.uiClick : null));
        }
    }
}
