using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RiskOfMagic.UI
{
    /// <summary>Small helpers shared by the UI Toolkit menus (lookups, show/hide with transition, focus, cloning).</summary>
    public static class MenuUi
    {
        /// <summary>USS class: display none (Menus.uss).</summary>
        public const string Hidden = "hidden";
        /// <summary>USS class added for a moment when a screen appears: it fades and slides in.</summary>
        public const string Enter = "screen--enter";

        /// <summary>Named element; logs a clear error when a UXML was edited and lost it.</summary>
        public static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            var element = root?.Q<T>(name);
            if (element == null)
                Debug.LogError($"[Risk of Magic] UI element '{name}' ({typeof(T).Name}) not found under '{root?.name}'. Was it renamed in the UXML?");
            return element;
        }

        public static bool IsShown(VisualElement element) => element != null && !element.ClassListContains(Hidden);

        public static void Show(VisualElement element, bool animate = true)
        {
            if (element == null || IsShown(element))
                return;
            element.RemoveFromClassList(Hidden);
            if (!animate)
                return;
            element.AddToClassList(Enter);
            element.schedule.Execute(() => element.RemoveFromClassList(Enter)).StartingIn(20);
        }

        public static void Hide(VisualElement element) => element?.AddToClassList(Hidden);

        public static void SetShown(VisualElement element, bool shown, bool animate = false)
        {
            if (shown)
                Show(element, animate);
            else
                Hide(element);
        }

        /// <summary>Focus on the next frame (the element may have just been displayed).</summary>
        public static void Focus(VisualElement element)
        {
            element?.schedule.Execute(() =>
            {
                if (element.panel != null && element.canGrabFocus)
                    element.Focus();
            }).StartingIn(30);
        }

        /// <summary>Focuses the first focusable control under the container (keyboard / gamepad navigation).</summary>
        public static void FocusFirst(VisualElement container)
        {
            container?.schedule.Execute(() =>
            {
                var first = container.Query<VisualElement>().Where(e => e.focusable && e.canGrabFocus && e.enabledInHierarchy).First();
                first?.Focus();
            }).StartingIn(30);
        }

        /// <summary>Clones a single-root template under the parent and returns its root element.</summary>
        public static VisualElement Clone(VisualTreeAsset template, VisualElement parent)
        {
            if (template == null || parent == null)
            {
                Debug.LogError("[Risk of Magic] Missing UI template (Resources/MenuUI): rebuild the scenes or plug the template.");
                return null;
            }
            var container = template.Instantiate();
            if (container.childCount == 0)
                return null;
            var root = container[0];
            parent.Add(root);
            MenuSounds.Register(root);
            return root;
        }

        /// <summary>Image coming from game data (icons, portraits, ArtLibrary art); null clears it.</summary>
        public static void SetImage(VisualElement element, Sprite sprite)
        {
            if (element != null)
                element.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.Null);
        }

        public static void SetText(TextElement element, string text)
        {
            if (element != null)
                element.text = text ?? string.Empty;
        }

        /// <summary>Esc (keyboard) or B (gamepad) pressed this frame.</summary>
        public static bool BackPressed()
        {
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
        }
    }
}
