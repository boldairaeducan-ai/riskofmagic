using System.Collections.Generic;
using RiskOfMagic.Combat;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Arcs around the crosshair pointing toward where the local player was hit from (kept up to date while turning).</summary>
    public class DamageDirectionWidget : HudWidget
    {
        private struct Indicator
        {
            public Vector3 SourcePosition;
            public float Time;
        }

        [SerializeField, Tooltip("Pivot at the screen center; its child arc sits above it.")] private RectTransform template;
        [SerializeField, Min(0.1f)] private float lifetime = 1.2f;
        [SerializeField, Min(1)] private int maxIndicators = 6;

        private readonly List<Indicator> indicators = new();
        private TemplatePool<RectTransform> pool;

        private void Awake()
        {
            if (template != null)
                pool = new TemplatePool<RectTransform>(template, template.parent);
        }

        protected override void OnBind() => Player.Health.Damaged += OnDamaged;

        protected override void OnUnbind()
        {
            Player.Health.Damaged -= OnDamaged;
            indicators.Clear();
            if (pool != null)
            {
                pool.Begin();
                pool.End();
            }
        }

        private void OnDamaged(DamageInfo info)
        {
            if (info.IgnoreModifiers)
                return;
            Vector3 source;
            if (info.TryGetSource(out var attacker))
                source = attacker.transform.position;
            else if (info.Direction.sqrMagnitude > 0.01f)
                source = Player.transform.position - info.Direction * 5f;
            else
                return;

            if (indicators.Count >= maxIndicators)
                indicators.RemoveAt(0);
            indicators.Add(new Indicator { SourcePosition = source, Time = Time.time });
        }

        protected override void Tick()
        {
            if (pool == null)
                return;
            Vector3 forward = Player.transform.forward;
            forward.y = 0f;

            pool.Begin();
            for (int i = indicators.Count - 1; i >= 0; i--)
            {
                float age = Time.time - indicators[i].Time;
                if (age >= lifetime)
                {
                    indicators.RemoveAt(i);
                    continue;
                }

                Vector3 toSource = indicators[i].SourcePosition - Player.transform.position;
                toSource.y = 0f;
                if (toSource.sqrMagnitude < 0.01f)
                    continue;
                float angle = Vector3.SignedAngle(forward, toSource, Vector3.up);

                var indicator = pool.Next();
                indicator.localEulerAngles = new Vector3(0f, 0f, -angle);
                var group = indicator.GetComponent<CanvasGroup>();
                if (group != null)
                    group.alpha = 1f - age / lifetime;
            }
            pool.End();
        }
    }
}
