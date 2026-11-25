using RiskOfMagic.Settings;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>FPS counter (Options → Debug → Afficher les FPS): smoothed frames per second and frame time, top right.</summary>
    public sealed class FpsOverlay : MonoBehaviour
    {
        private float smoothedDelta = 1f / 60f;
        private float worstDelta;
        private float worstResetAt;
        private GUIStyle style;
        private string text = string.Empty;
        private float nextText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("FpsOverlay");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            go.AddComponent<FpsOverlay>();
        }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            smoothedDelta = Mathf.Lerp(smoothedDelta, delta, 0.1f);
            if (Time.unscaledTime >= worstResetAt)
            {
                worstResetAt = Time.unscaledTime + 1f;
                worstDelta = 0f;
            }
            worstDelta = Mathf.Max(worstDelta, delta);
            if (GameSettings.ShowFps && Time.unscaledTime >= nextText)
            {
                nextText = Time.unscaledTime + 0.25f;
                text = $"{1f / Mathf.Max(0.0001f, smoothedDelta):0} FPS   {smoothedDelta * 1000f:0.0} ms   (pire {worstDelta * 1000f:0} ms)";
            }
        }

        private void OnGUI()
        {
            if (!GameSettings.ShowFps)
                return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
            var rect = new Rect(Screen.width - 410f, 6f, 400f, 26f);
            float fps = 1f / Mathf.Max(0.0001f, smoothedDelta);
            style.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
            style.normal.textColor = fps >= 55f ? new Color(0.5f, 1f, 0.5f) : fps >= 30f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.4f, 0.35f);
            GUI.Label(rect, text, style);
        }
    }
}
