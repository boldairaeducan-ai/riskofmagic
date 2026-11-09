using RiskOfMagic.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RiskOfMagic.Feedback
{
    /// <summary>
    /// Local player only: screen reactions layered over the scene volumes.
    /// Hit = short red vignette + chromatic pulse; low health / downed = desaturated, darkened edges, slow heartbeat.
    /// Each effect is a runtime global Volume whose weight is driven here (0 = no change to the scene grading).
    /// </summary>
    public class CombatPostFx : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.35f;
        [SerializeField] private float hitDecay = 3.5f;
        [SerializeField] private float heartbeatSpeed = 5.5f;

        private PlayerAvatar player;
        private Volume hitVolume;
        private Volume lowVolume;
        private float hit;
        private float low;

        public static void Attach(PlayerAvatar target)
        {
            if (target == null || target.GetComponent<CombatPostFx>() != null)
                return;
            target.gameObject.AddComponent<CombatPostFx>().player = target;
        }

        private void Start()
        {
            hitVolume = CreateVolume("HitFx", 200, profile =>
            {
                var vignette = profile.Add<Vignette>();
                vignette.color.Override(new Color(0.55f, 0.02f, 0.02f));
                vignette.intensity.Override(0.42f);
                vignette.smoothness.Override(0.55f);
                profile.Add<ChromaticAberration>().intensity.Override(0.35f);
            });
            lowVolume = CreateVolume("LowHealthFx", 190, profile =>
            {
                var color = profile.Add<ColorAdjustments>();
                color.saturation.Override(-55f);
                color.contrast.Override(25f);
                var vignette = profile.Add<Vignette>();
                vignette.color.Override(new Color(0.25f, 0f, 0.02f));
                vignette.intensity.Override(0.45f);
                vignette.smoothness.Override(0.6f);
            });
            if (player != null)
                player.Health.HealthChanged += OnHealthChanged;
        }

        private Volume CreateVolume(string name, float priority, System.Action<VolumeProfile> build)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.weight = 0f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            build(profile);
            volume.profile = profile;
            return volume;
        }

        private void OnHealthChanged(float previous, float current)
        {
            float max = player != null ? player.Health.Max : 0f;
            if (max <= 0f || current >= previous)
                return;
            hit = Mathf.Clamp01(hit + 0.35f + (previous - current) / max * 3f);
        }

        private void Update()
        {
            if (player == null || hitVolume == null)
                return;
            hit = Mathf.MoveTowards(hit, 0f, hitDecay * Time.unscaledDeltaTime * Mathf.Max(0.3f, hit));
            hitVolume.weight = hit;

            float ratio = player.Health.Max > 0f ? player.Health.Current / player.Health.Max : 1f;
            float target = player.IsDowned ? 1f : player.IsAlive ? Mathf.Clamp01(1f - ratio / lowHealthThreshold) : 0f;
            low = Mathf.MoveTowards(low, target, 2f * Time.unscaledDeltaTime);
            float beat = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * heartbeatSpeed);
            lowVolume.weight = low * (player.IsDowned ? 1f : beat);
        }

        private void OnDestroy()
        {
            if (player != null && player.Health != null)
                player.Health.HealthChanged -= OnHealthChanged;
            foreach (var volume in new[] { hitVolume, lowVolume })
                if (volume != null && volume.profile != null)
                    Destroy(volume.profile);
        }
    }
}
