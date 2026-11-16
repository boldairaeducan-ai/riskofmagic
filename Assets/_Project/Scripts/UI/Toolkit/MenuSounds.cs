using RiskOfMagic.Audio;
using UnityEngine.UIElements;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Hover / click sounds of UI Toolkit buttons (AudioConfig.uiHover / uiClick), the counterpart of UiSoundTrigger
    /// for uGUI. Add the USS class "silent" to a button to mute it. Hovering a button also gives it the focus, so mouse,
    /// keyboard and gamepad share a single highlight (unless a text field is being typed in).
    /// </summary>
    public static class MenuSounds
    {
        private const string WiredClass = "ui-sound";
        private const string SilentClass = "silent";

        public static void Register(VisualElement root) => root?.Query<Button>().ForEach(Wire);

        public static void Wire(Button button)
        {
            if (button == null || button.ClassListContains(WiredClass))
                return;
            button.AddToClassList(WiredClass);
            bool hovered = false;
            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                hovered = true;
                Play(button, hover: true);
                FocusOnHover(button);
            });
            button.RegisterCallback<PointerLeaveEvent>(_ => hovered = false);
            button.RegisterCallback<FocusInEvent>(_ =>
            {
                if (!hovered)
                    Play(button, hover: true); // keyboard / gamepad navigation
            });
            button.clicked += () => Play(button, hover: false);
        }

        private static void FocusOnHover(Button button)
        {
            if (!button.enabledInHierarchy || !button.canGrabFocus)
                return;
            var focused = button.focusController?.focusedElement as VisualElement;
            if (focused is TextField || focused?.GetFirstAncestorOfType<TextField>() != null)
                return; // don't steal focus from a field being typed in
            button.Focus();
        }

        private static void Play(Button button, bool hover)
        {
            if (!button.enabledInHierarchy || button.ClassListContains(SilentClass) || AudioService.Config == null)
                return;
            AudioService.Play2D(hover ? AudioService.Config.uiHover : AudioService.Config.uiClick);
        }
    }
}
