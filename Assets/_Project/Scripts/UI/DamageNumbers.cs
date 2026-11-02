using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Floating damage numbers: damage dealt to enemies (by anyone) and damage taken by the local player.
    /// Prototype IMGUI implementation, driven by Health.AnyDamaged.
    /// </summary>
    public class DamageNumbers : MonoBehaviour
    {
        private struct Popup
        {
            public Vector3 Position;
            public string Text;
            public Color Color;
            public int FontSize;
            public float Age;
        }

        [SerializeField, Min(0.1f)] private float lifetime = 0.9f;
        [SerializeField] private float riseSpeed = 1.5f;
        [SerializeField] private float maxDistance = 80f;
        [SerializeField, Min(1)] private int maxPopups = 80;
        [SerializeField] private int fontSize = 18;
        [SerializeField] private int criticalFontSize = 26;
        [SerializeField] private Color damageTakenColor = new(1f, 0.25f, 0.25f);

        private readonly List<Popup> popups = new();
        private GUIStyle style;

        private void OnEnable() => Health.AnyDamaged += OnAnyDamaged;
        private void OnDisable() => Health.AnyDamaged -= OnAnyDamaged;

        private void OnAnyDamaged(Health victim, DamageInfo info)
        {
            var local = PlayerAvatar.Local;
            if (local == null || victim == null || info.Amount <= 0f)
                return;

            bool damageTaken = victim == local.Health;
            if (!damageTaken && victim.Team == local.Health.Team)
                return;

            if (popups.Count >= maxPopups)
                popups.RemoveAt(0);

            Vector3 position = info.Point != Vector3.zero ? info.Point : victim.transform.position + Vector3.up * 2f;
            popups.Add(new Popup
            {
                Position = position + Random.insideUnitSphere * 0.3f,
                Text = Mathf.CeilToInt(info.Amount) + (info.IsCritical ? "!" : string.Empty),
                Color = damageTaken ? damageTakenColor : GetColor(info),
                FontSize = info.IsCritical ? criticalFontSize : fontSize
            });
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var popup = popups[i];
                popup.Age += dt;
                popup.Position += Vector3.up * (riseSpeed * dt);
                if (popup.Age >= lifetime)
                    popups.RemoveAt(i);
                else
                    popups[i] = popup;
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || popups.Count == 0)
                return;
            var cam = GetCamera();
            if (cam == null)
                return;

            style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            var previousColor = GUI.color;

            foreach (var popup in popups)
            {
                Vector3 screen = cam.WorldToScreenPoint(popup.Position);
                if (screen.z <= 0f || screen.z > maxDistance)
                    continue;

                float alpha = 1f - popup.Age / lifetime;
                style.fontSize = popup.FontSize;
                var rect = new Rect(screen.x - 60f, Screen.height - screen.y - 15f, 120f, 30f);

                GUI.color = new Color(0f, 0f, 0f, alpha);
                GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), popup.Text, style);
                GUI.color = new Color(popup.Color.r, popup.Color.g, popup.Color.b, alpha);
                GUI.Label(rect, popup.Text, style);
            }
            GUI.color = previousColor;
        }

        private static Camera GetCamera()
        {
            var local = PlayerAvatar.Local;
            if (local != null && local.Look.Camera != null && local.Look.Camera.enabled)
                return local.Look.Camera;
            return Camera.main;
        }

        private static Color GetColor(in DamageInfo info)
        {
            if (info.IsCritical)
                return new Color(1f, 0.85f, 0.2f);
            return info.Type switch
            {
                DamageType.Fire => new Color(1f, 0.55f, 0.2f),
                DamageType.Frost => new Color(0.55f, 0.9f, 1f),
                DamageType.Lightning => new Color(0.75f, 0.85f, 1f),
                DamageType.Arcane => new Color(0.8f, 0.5f, 1f),
                DamageType.Poison => new Color(0.5f, 1f, 0.4f),
                _ => Color.white
            };
        }
    }
}
