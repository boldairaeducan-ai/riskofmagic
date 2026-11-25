using RiskOfMagic.Enemies;
using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Health bar of the first living boss (EnemyDefinition.isBoss).</summary>
    public class BossBarWidget : HudWidget
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private RectTransform fill;
        [SerializeField] private RectTransform trail;
        [SerializeField, Min(0f)] private float trailSpeed = 0.4f;
        [SerializeField, Tooltip("Max health removed by the curse (end of the bar).")] private Color cursedColor = new(0.55f, 0.2f, 0.75f, 1f);

        private RectTransform cursedSection;

        private EnemyActor boss;
        private float trailRatio;

        protected override void OnUnbind() => SetVisible(false);

        protected override void Tick()
        {
            if (boss == null || !boss.IsSpawned || !boss.Health.IsAlive)
                boss = FindBoss();

            SetVisible(boss != null);
            if (boss == null)
                return;

            // The bar spans the full max health: the cursed part (removed max health) is a purple section at the end.
            float debuff = boss.Health.MaxHealthDebuff;
            float total = Mathf.Max(1f, boss.Health.Max + debuff);
            float ratio = boss.Health.Current / total;
            SetFill(fill, ratio);
            UpdateCursedSection(debuff / total);
            trailRatio = trailRatio > ratio ? Mathf.MoveTowards(trailRatio, ratio, trailSpeed * Time.deltaTime) : ratio;
            SetFill(trail, trailRatio);
            if (nameLabel != null)
            {
                string text = $"{boss.DisplayName}  <size=70%>niv. {boss.Level}</size>";
                if (nameLabel.text != text)
                    nameLabel.text = text;
            }
        }

        private void UpdateCursedSection(float share)
        {
            if (cursedSection == null && fill != null && share > 0f)
            {
                var go = new GameObject("CursedSection", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                cursedSection = (RectTransform)go.transform;
                cursedSection.SetParent(fill.parent, false);
                cursedSection.SetSiblingIndex(fill.GetSiblingIndex() + 1);
                var image = go.GetComponent<UnityEngine.UI.Image>();
                image.color = cursedColor;
                image.raycastTarget = false;
            }
            if (cursedSection == null)
                return;
            bool visible = share > 0.001f;
            if (cursedSection.gameObject.activeSelf != visible)
                cursedSection.gameObject.SetActive(visible);
            if (!visible)
                return;
            cursedSection.anchorMin = new Vector2(1f - share, 0f);
            cursedSection.anchorMax = Vector2.one;
            cursedSection.offsetMin = cursedSection.offsetMax = Vector2.zero;
        }

        private static EnemyActor FindBoss()
        {
            foreach (var enemy in EnemyActor.Active)
                if (enemy.Definition != null && enemy.Definition.isBoss && !enemy.IsIllusion && enemy.Health.IsAlive)
                    return enemy;
            return null;
        }

        private void SetVisible(bool visible)
        {
            if (group != null)
                group.alpha = visible ? 1f : 0f;
            if (!visible)
                trailRatio = 1f;
        }
    }
}
