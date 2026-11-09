using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using RiskOfMagic.Enemies;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Icons floating above enemies: elite affix, then active damage over time (poison, bleed, burn).
    /// Icons come from ArtLibrary; without one, a colored square with an initial.
    /// </summary>
    public class EnemyStatusIconsWidget : HudWidget
    {
        [SerializeField, Tooltip("Row of icon slots named Icon0, Icon1… each with an Image and a TMP child « Letter ».")]
        private RectTransform rowTemplate;
        [SerializeField] private float maxDistance = 45f;
        [SerializeField, Tooltip("Above the top of the enemy's collider.")] private float headroom = 0.5f;

        private static readonly DamageType[] Dots = { DamageType.Fire, DamageType.Poison, DamageType.Bleed };

        private class Row
        {
            public RectTransform Root;
            public Image[] Icons;
            public TMP_Text[] Letters;
        }

        private readonly Dictionary<RectTransform, Row> rows = new();
        private readonly Dictionary<EnemyActor, float> heights = new();
        private readonly List<EnemyActor> stale = new();
        private TemplatePool<RectTransform> pool;
        private RectTransform space;

        private void Awake()
        {
            space = (RectTransform)transform;
            if (rowTemplate != null)
                pool = new TemplatePool<RectTransform>(rowTemplate, transform);
        }

        protected override void OnUnbind()
        {
            pool?.Begin();
            pool?.End();
            heights.Clear();
        }

        protected override void Tick()
        {
            if (pool == null)
                return;
            var cam = ScreenProjection.GetGameplayCamera();
            var library = ArtLibrary.Instance;
            pool.Begin();
            foreach (var enemy in EnemyActor.Active)
            {
                if (enemy == null || enemy.Health == null || !enemy.Health.IsAlive)
                    continue;
                bool elite = enemy.IsElite;
                int dotMask = enemy.Health.DotMask;
                if (!elite && !HasShownDot(dotMask))
                    continue;
                Vector3 head = enemy.transform.position + Vector3.up * Height(enemy);
                if (!ScreenProjection.TryProject(cam, space, head, maxDistance, out var position))
                    continue;

                var row = GetRow(pool.Next());
                row.Root.anchoredPosition = position;
                int used = 0;
                if (elite)
                    SetIcon(row, used++, library != null ? library.GetEliteIcon(enemy.Elite) : null, enemy.Elite.tint, enemy.Elite.nameMasculine);
                foreach (var type in Dots)
                {
                    if ((dotMask & (1 << (int)type)) == 0 || used >= row.Icons.Length)
                        continue;
                    var art = library != null ? library.GetDot(type) : null;
                    SetIcon(row, used++, art != null ? art.icon : null, DotColor(type), art != null ? art.name : type.ToString());
                }
                for (int i = used; i < row.Icons.Length; i++)
                    row.Icons[i].gameObject.SetActive(false);
            }
            pool.End();
            Cleanup();
        }

        private static bool HasShownDot(int mask)
        {
            foreach (var type in Dots)
                if ((mask & (1 << (int)type)) != 0)
                    return true;
            return false;
        }

        private Color DotColor(DamageType type)
        {
            if (theme == null)
                return Color.white;
            return type switch
            {
                DamageType.Fire => theme.damageFire,
                DamageType.Poison => theme.damagePoison,
                DamageType.Bleed => theme.damageBleed,
                _ => theme.damagePhysical
            };
        }

        private static void SetIcon(Row row, int index, Sprite sprite, Color color, string name)
        {
            var icon = row.Icons[index];
            icon.gameObject.SetActive(true);
            icon.sprite = sprite;
            icon.color = sprite != null ? Color.white : new Color(color.r, color.g, color.b, 0.85f);
            var letter = row.Letters[index];
            if (letter != null)
                letter.text = sprite != null || string.IsNullOrEmpty(name) ? string.Empty : name.Substring(0, 1).ToUpperInvariant();
        }

        private float Height(EnemyActor enemy)
        {
            if (heights.TryGetValue(enemy, out float height))
                return height;
            height = 2f;
            var collider = enemy.GetComponentInChildren<Collider>();
            if (collider != null)
                height = collider.bounds.max.y - enemy.transform.position.y;
            height += headroom;
            heights[enemy] = height;
            return height;
        }

        private void Cleanup()
        {
            if (heights.Count < 64)
                return;
            stale.Clear();
            foreach (var enemy in heights.Keys)
                if (enemy == null || !enemy.Health.IsAlive)
                    stale.Add(enemy);
            foreach (var enemy in stale)
                heights.Remove(enemy);
        }

        private Row GetRow(RectTransform root)
        {
            if (rows.TryGetValue(root, out var row))
                return row;
            var icons = new List<Image>();
            var letters = new List<TMP_Text>();
            for (int i = 0; ; i++)
            {
                var child = root.Find($"Icon{i}");
                if (child == null)
                    break;
                icons.Add(child.GetComponent<Image>());
                letters.Add(child.GetComponentInChildren<TMP_Text>(true));
            }
            row = new Row { Root = root, Icons = icons.ToArray(), Letters = letters.ToArray() };
            rows[root] = row;
            return row;
        }
    }
}
