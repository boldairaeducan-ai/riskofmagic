using System;
using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Crosshair turning red with an "out of range" hint when the aimed point is beyond the primary skill's range.</summary>
    public class CrosshairWidget : HudWidget
    {
        [SerializeField] private UnityEngine.UI.Image crosshair;
        [SerializeField] private TMP_Text outOfRangeLabel;
        [SerializeField] private LayerMask aimMask = Physics.DefaultRaycastLayers;
        [SerializeField] private float maxCheckDistance = 300f;

        private readonly RaycastHit[] hits = new RaycastHit[16];

        protected override void OnUnbind() => SetOutOfRange(false);

        protected override void Tick()
        {
            var primary = Player.Skills != null ? Player.Skills.GetSlot(SkillSlot.Primary) : null;
            float range = primary != null ? primary.Definition.MaxRange : 0f;
            SetOutOfRange(range > 0f && AimDistance() > range);
        }

        private float AimDistance()
        {
            Ray aim = Player.Look.AimRay;
            int count = Physics.RaycastNonAlloc(aim, hits, maxCheckDistance, aimMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, 0, count, RaycastHitDistanceComparer.Instance);
            for (int i = 0; i < count; i++)
                if (!hits[i].collider.transform.IsChildOf(Player.transform))
                    return hits[i].distance;
            return float.MaxValue; // sky
        }

        private void SetOutOfRange(bool outOfRange)
        {
            if (crosshair != null && theme != null)
                crosshair.color = outOfRange ? theme.dangerColor : theme.textColor;
            if (outOfRangeLabel != null)
                outOfRangeLabel.gameObject.SetActive(outOfRange);
        }
    }
}
