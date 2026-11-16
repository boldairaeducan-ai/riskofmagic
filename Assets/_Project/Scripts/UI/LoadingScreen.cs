using RiskOfMagic.Flow;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Full-screen cover during map loads (Netcode scene loads: lobby → stage 1, stage → stage): shown as soon as the
    /// load starts, hidden once the local player has been placed on a spawn point of the new map (or after a safety
    /// timeout). Hides the scene camera and the spawn/NavMesh setup. Created at runtime (persistent UIDocument on the menu
    /// panel, UI/Menus/LoadingScreen.uxml, drawn above every other menu). Art: ArtLibrary.loadingBackground.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        private const float MinVisibleTime = 0.4f;
        private const float FallbackAfterPlayerSeen = 2.5f;
        private const float MaxVisibleTime = 25f;
        private const string VisibleClass = "loading--visible";

        private static LoadingScreen instance;

        private VisualElement screen;
        private Label label;
        private NetworkSceneManager subscribedTo;
        private bool visible;
        private bool placed;
        private float shownAt;
        private float playerSeenAt = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var assets = MenuUiAssets.Instance;
            if (assets == null || assets.panelSettings == null || assets.loadingScreen == null)
            {
                Debug.LogWarning("[Risk of Magic] Loading screen disabled: Resources/MenuUI is missing (run Build Prototype Scene).");
                return;
            }
            var go = new GameObject("LoadingScreen");
            go.SetActive(false); // configure the document before it builds its tree
            DontDestroyOnLoad(go);
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = assets.panelSettings;
            document.visualTreeAsset = assets.loadingScreen;
            document.sortingOrder = 1000f; // above the main menu and the pause menu
            instance = go.AddComponent<LoadingScreen>();
            go.SetActive(true);
            instance.Build(document.rootVisualElement);
        }

        /// <summary>Shows the cover now (also called automatically on Netcode scene loads).</summary>
        public static void Show(string message = "Chargement…")
        {
            if (instance == null)
                return;
            instance.visible = true;
            instance.placed = false;
            instance.playerSeenAt = -1f;
            instance.shownAt = Time.unscaledTime;
            MenuUi.SetText(instance.label, message);
            instance.SetVisible(true);
        }

        /// <summary>The local player was moved to a spawn point of the current map.</summary>
        public static void NotifyLocalPlayerPlaced()
        {
            if (instance != null)
                instance.placed = true;
        }

        private void Build(VisualElement root)
        {
            screen = MenuUi.Require<VisualElement>(root, "loadingScreen");
            label = MenuUi.Require<Label>(root, "label");
            var art = RiskOfMagic.Core.ArtLibrary.Instance != null ? RiskOfMagic.Core.ArtLibrary.Instance.loadingBackground : null;
            var background = root.Q("background");
            MenuUi.SetImage(background, art);
            MenuUi.SetShown(background, art != null);
            SetVisible(false);
        }

        /// <summary>Fades in / out (USS transition) and blocks the pointer only while visible.</summary>
        private void SetVisible(bool value)
        {
            if (screen == null)
                return;
            screen.EnableInClassList(VisibleClass, value);
            screen.pickingMode = value ? PickingMode.Position : PickingMode.Ignore;
        }

        private void Update()
        {
            SubscribeToSceneEvents();

            if (visible)
            {
                float elapsed = Time.unscaledTime - shownAt;
                if (PlayerAvatar.Local != null && playerSeenAt < 0f)
                    playerSeenAt = Time.unscaledTime;
                bool fallback = playerSeenAt >= 0f && Time.unscaledTime - playerSeenAt > FallbackAfterPlayerSeen;
                bool disconnected = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
                if (elapsed > MinVisibleTime && (placed || fallback || disconnected || elapsed > MaxVisibleTime))
                {
                    visible = false;
                    SetVisible(false);
                }
                else
                {
                    int dots = 1 + (int)(elapsed * 2f) % 3;
                    MenuUi.SetText(label, "Chargement" + new string('.', dots));
                }
            }
        }

        private void SubscribeToSceneEvents()
        {
            var manager = NetworkManager.Singleton;
            var sceneManager = manager != null && manager.IsListening ? manager.SceneManager : null;
            if (sceneManager == subscribedTo)
                return;
            if (subscribedTo != null)
                subscribedTo.OnSceneEvent -= OnSceneEvent;
            subscribedTo = sceneManager;
            if (subscribedTo != null)
                subscribedTo.OnSceneEvent += OnSceneEvent;
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || sceneEvent.ClientId != manager.LocalClientId)
                return;
            if (sceneEvent.SceneEventType == SceneEventType.Load && sceneEvent.SceneName != GameFlow.MainMenuScene)
                Show();
        }
    }
}
