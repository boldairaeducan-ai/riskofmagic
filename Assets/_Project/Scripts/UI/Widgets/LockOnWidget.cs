using System.Collections.Generic;
using RiskOfMagic.Skills.Types;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// While charging a lock-on skill: a marker on each locked enemy (with the number of missiles on it),
    /// a hint on the enemy that the next lock would take, and a "locks / missiles" counter.
    /// </summary>
    public class LockOnWidget : HudWidget
    {
        [SerializeField] private RectTransform markerTemplate;
        [SerializeField] private RectTransform nextTargetMarker;
        [SerializeField] private TMP_Text counterLabel;
        [SerializeField] private float maxDistance = 80f;
        [SerializeField] private float heightOffset = 1.2f;

        private readonly Dictionary<ulong, int> lockCounts = new();
        private TemplatePool<RectTransform> pool;
        private RectTransform space;

        private void Awake()
        {
            space = (RectTransform)transform;
            if (markerTemplate != null)
                pool = new TemplatePool<RectTransform>(markerTemplate, transform);
            Hide();
        }

        protected override void OnUnbind() => Hide();

        protected override void Tick()
        {
            var skills = Player.Skills;
            var state = skills != null && skills.IsCharging ? skills.GetSlot(skills.ChargingSlot) : null;
            if (state == null || state.Definition is not HomingVolleySkill volley || pool == null)
            {
                Hide();
                return;
            }

            var cam = ScreenProjection.GetGameplayCamera();
            var spawned = NetworkManager.Singleton.SpawnManager.SpawnedObjects;

            lockCounts.Clear();
            foreach (ulong id in state.LockedTargets)
                lockCounts[id] = lockCounts.TryGetValue(id, out int n) ? n + 1 : 1;

            pool.Begin();
            foreach (var pair in lockCounts)
            {
                if (!spawned.TryGetValue(pair.Key, out var target)
                    || !ScreenProjection.TryProject(cam, space, target.transform.position + Vector3.up * heightOffset, maxDistance, out var position))
                    continue;
                var marker = pool.Next();
                marker.anchoredPosition = position;
                var label = marker.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = pair.Value > 1 ? $"x{pair.Value}" : string.Empty;
            }
            pool.End();

            if (nextTargetMarker != null)
            {
                var next = state.LockedTargets.Count < volley.maxTargets ? volley.FindBestTarget(skills.Context) : null;
                bool visible = next != null && ScreenProjection.TryProject(cam, space, next.transform.position + Vector3.up * heightOffset, maxDistance, out var nextPosition)
                               && SetPosition(nextTargetMarker, nextPosition);
                nextTargetMarker.gameObject.SetActive(visible);
            }

            if (counterLabel != null)
            {
                int missiles = volley.GetMissileCount(skills.ChargeProgress);
                string text = $"{state.LockedTargets.Count} cible(s) · {missiles}/{volley.maxTargets} projectiles";
                if (counterLabel.text != text)
                    counterLabel.text = text;
            }
        }

        private static bool SetPosition(RectTransform rect, Vector2 position)
        {
            rect.anchoredPosition = position;
            return true;
        }

        private void Hide()
        {
            if (pool != null)
            {
                pool.Begin();
                pool.End();
            }
            if (nextTargetMarker != null)
                nextTargetMarker.gameObject.SetActive(false);
            if (counterLabel != null)
                counterLabel.text = string.Empty;
        }
    }
}
