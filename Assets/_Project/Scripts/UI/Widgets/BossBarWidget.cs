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

            float ratio = boss.Health.Normalized;
            SetFill(fill, ratio);
            trailRatio = trailRatio > ratio ? Mathf.MoveTowards(trailRatio, ratio, trailSpeed * Time.deltaTime) : ratio;
            SetFill(trail, trailRatio);
            if (nameLabel != null)
            {
                string text = $"{boss.Definition.displayName}  <size=70%>niv. {boss.Level}</size>";
                if (nameLabel.text != text)
                    nameLabel.text = text;
            }
        }

        private static EnemyActor FindBoss()
        {
            foreach (var enemy in EnemyActor.Active)
                if (enemy.Definition != null && enemy.Definition.isBoss && enemy.Health.IsAlive)
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
