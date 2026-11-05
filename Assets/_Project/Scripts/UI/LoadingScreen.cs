using RiskOfMagic.Flow;
using RiskOfMagic.Player;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Full-screen cover during map loads (Netcode scene loads: lobby → stage 1, stage → stage): shown as soon as the
    /// load starts, hidden once the local player has been placed on a spawn point of the new map (or after a safety
    /// timeout). Hides the scene camera and the spawn/NavMesh setup. Built at runtime, persistent, no setup needed.
    /// Art: set a sprite on Background (child of "LoadingScreen") or replace this class.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        private const float MinVisibleTime = 0.4f;
        private const float FallbackAfterPlayerSeen = 2.5f;
        private const float MaxVisibleTime = 25f;
        private const float FadeSpeed = 3f;

        private static LoadingScreen instance;

        private CanvasGroup group;
        private TMP_Text label;
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
            var go = new GameObject("LoadingScreen");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<LoadingScreen>();
            instance.Build();
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
            instance.label.text = message;
            instance.group.blocksRaycasts = true;
        }

        /// <summary>The local player was moved to a spawn point of the current map.</summary>
        public static void NotifyLocalPlayerPlaced()
        {
            if (instance != null)
                instance.placed = true;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            gameObject.AddComponent<GraphicRaycaster>();
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(transform, false);
            var backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0.04f, 0.035f, 0.05f, 1f);

            var text = new GameObject("Label", typeof(RectTransform));
            text.transform.SetParent(transform, false);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(1200f, 120f);
            label = text.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 48f;
            label.color = new Color(0.92f, 0.88f, 0.8f);
            label.text = "Chargement…";
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
                    group.blocksRaycasts = false;
                }
                else if (label != null)
                {
                    int dots = 1 + (int)(elapsed * 2f) % 3;
                    label.text = "Chargement" + new string('.', dots);
                }
            }

            group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, (visible ? 10f : FadeSpeed) * Time.unscaledDeltaTime);
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
