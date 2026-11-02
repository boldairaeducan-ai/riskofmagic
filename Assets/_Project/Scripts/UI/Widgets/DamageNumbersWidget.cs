using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Floating damage numbers (damage to enemies by anyone, damage taken by the local player).
    /// The template label defines the look; colors and sizes come from the HudTheme.
    /// </summary>
    public class DamageNumbersWidget : MonoBehaviour
    {
        private struct Popup
        {
            public Vector3 World;
            public string Text;
            public Color Color;
            public float Size;
            public float Age;
        }

        [SerializeField] private HudTheme theme;
        [SerializeField] private TMP_Text template;
        [SerializeField, Min(0.1f)] private float lifetime = 0.9f;
        [SerializeField] private float riseSpeed = 1.5f;
        [SerializeField] private float maxDistance = 80f;
        [SerializeField, Min(1)] private int maxPopups = 80;
        [SerializeField] private AnimationCurve scaleOverLifetime = new(new Keyframe(0f, 1.4f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0.9f));

        private readonly List<Popup> popups = new();
        private TemplatePool<TMP_Text> pool;
        private RectTransform space;

        private void Awake()
        {
            space = (RectTransform)transform;
            if (template != null)
                pool = new TemplatePool<TMP_Text>(template, transform);
        }

        private void OnEnable() => Health.AnyDamaged += OnAnyDamaged;
        private void OnDisable() => Health.AnyDamaged -= OnAnyDamaged;

        private void OnAnyDamaged(Health victim, DamageInfo info)
        {
            var local = PlayerAvatar.Local;
            if (local == null || victim == null || info.Amount <= 0f || theme == null)
                return;

            bool taken = victim == local.Health;
            if (!taken && victim.Team == local.Health.Team)
                return;
            if (popups.Count >= maxPopups)
                popups.RemoveAt(0);

            Vector3 world = info.Point != Vector3.zero ? info.Point : victim.transform.position + Vector3.up * 2f;
            popups.Add(new Popup
            {
                World = world + Random.insideUnitSphere * 0.3f,
                Text = Mathf.CeilToInt(info.Amount) + (info.IsCritical ? "!" : string.Empty),
                Color = theme.GetDamageColor(info, taken),
                Size = info.IsCritical ? theme.criticalFontSize : theme.damageFontSize
            });
        }

        private void LateUpdate()
        {
            if (pool == null)
                return;

            var cam = ScreenProjection.GetGameplayCamera();
            float dt = Time.deltaTime;
            pool.Begin();
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var popup = popups[i];
                popup.Age += dt;
                popup.World += Vector3.up * (riseSpeed * dt);
                if (popup.Age >= lifetime)
                {
                    popups.RemoveAt(i);
                    continue;
                }
                popups[i] = popup;

                if (!ScreenProjection.TryProject(cam, space, popup.World, maxDistance, out var position))
                    continue;

                float t = popup.Age / lifetime;
                var label = pool.Next();
                label.rectTransform.anchoredPosition = position;
                label.rectTransform.localScale = Vector3.one * scaleOverLifetime.Evaluate(t);
                label.fontSize = popup.Size;
                label.text = popup.Text;
                label.color = new Color(popup.Color.r, popup.Color.g, popup.Color.b, 1f - t * t);
            }
            pool.End();
        }
    }
}
