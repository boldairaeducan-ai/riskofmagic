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
    /// Icons floating above enemies: elite affix, active damage over time (poison, bleed, burn), then the statuses
    /// flagged « aboveEnemies » in the ArtLibrary (e.g. Entravé). On bosses, a small number shows the stacks of each
    /// damage over time. Hidden when the level stands between the camera and the enemy.
    /// Icons come from ArtLibrary; without one, a colored square with an initial.
    /// </summary>
    public class EnemyStatusIconsWidget : HudWidget
    {
        [SerializeField, Tooltip("Row of icon slots named Icon0, Icon1… each with an Image and a TMP child « Letter ».")]
        private RectTransform rowTemplate;
        [SerializeField] private float maxDistance = 45f;
        [SerializeField, Tooltip("Above the top of the enemy's collider.")] private float headroom = 0.5f;
        [SerializeField, Tooltip("Level geometry hiding the icons (characters never do).")] private LayerMask occluders = Physics.DefaultRaycastLayers;

        private static readonly DamageType[] Dots = { DamageType.Fire, DamageType.Poison, DamageType.Bleed };

        private class Row
        {
            public RectTransform Root;
            public Image[] Icons;
            public TMP_Text[] Letters;
            public TMP_Text[] Counts;
        }

        private static readonly RaycastHit[] occlusionHits = new RaycastHit[16];

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
                bool statuses = HasShownStatus(enemy, library);
                if (!elite && !HasShownDot(dotMask) && !statuses)
                    continue;
                Vector3 head = enemy.transform.position + Vector3.up * Height(enemy);
                if (!ScreenProjection.TryProject(cam, space, head, maxDistance, out var position))
                    continue;
                if (cam != null && IsOccluded(cam.transform.position, head))
                    continue;

                var row = GetRow(pool.Next());
                row.Root.anchoredPosition = position;
                int used = 0;
                for (int i = 0; i < row.Counts.Length; i++)
                    SetCount(row, i, 0);
                if (elite)
                    SetIcon(row, used++, library != null ? library.GetEliteIcon(enemy.Elite) : null, enemy.Elite.tint, enemy.Elite.nameMasculine);
                foreach (var type in Dots)
                {
                    if ((dotMask & (1 << (int)type)) == 0 || used >= row.Icons.Length)
                        continue;
                    var art = library != null ? library.GetDot(type) : null;
                    SetCount(row, used, enemy.IsBoss ? enemy.Health.GetDotStacks(type) : 0);
                    SetIcon(row, used++, art != null ? art.icon : null, DotColor(type), art != null ? art.name : type.ToString());
                }
                if (statuses)
                    foreach (var art in library.statuses)
                    {
                        if (used >= row.Icons.Length)
                            break;
                        if (art != null && art.aboveEnemies && enemy.Status.Has(art.status))
                            SetIcon(row, used++, art.icon, theme != null ? theme.textMutedColor : Color.gray, art.name);
                    }
                for (int i = used; i < row.Icons.Length; i++)
                    row.Icons[i].gameObject.SetActive(false);
            }
            pool.End();
            Cleanup();
        }

        /// <summary>Something that isn't a character (level, props) between the camera and the point.</summary>
        private bool IsOccluded(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.01f)
                return false;
            int count = Physics.RaycastNonAlloc(from, delta / distance, occlusionHits, distance, occluders, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (occlusionHits[i].collider.GetComponentInParent<Health>() == null)
                    return true;
            return false;
        }

        private static void SetCount(Row row, int index, int stacks)
        {
            if (index >= row.Counts.Length || row.Counts[index] == null)
                return;
            row.Counts[index].text = stacks > 0 ? stacks.ToString() : string.Empty;
        }

        private static bool HasShownStatus(EnemyActor enemy, ArtLibrary library)
        {
            if (library == null || enemy.Status == null)
                return false;
            foreach (var art in library.statuses)
                if (art != null && art.aboveEnemies && enemy.Status.Has(art.status))
                    return true;
            return false;
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
            var counts = new List<TMP_Text>();
            for (int i = 0; ; i++)
            {
                var child = root.Find($"Icon{i}");
                if (child == null)
                    break;
                icons.Add(child.GetComponent<Image>());
                var letter = child.GetComponentInChildren<TMP_Text>(true);
                letters.Add(letter);
                counts.Add(CreateCount(child, letter));
            }
            row = new Row { Root = root, Icons = icons.ToArray(), Letters = letters.ToArray(), Counts = counts.ToArray() };
            rows[root] = row;
            return row;
        }

        /// <summary>Stack counter at the icon's bottom right, made from its letter (same font), at runtime.</summary>
        private static TMP_Text CreateCount(Transform icon, TMP_Text letter)
        {
            var existing = icon.Find("Count");
            if (existing != null)
                return existing.GetComponent<TMP_Text>();
            if (letter == null)
                return null;
            var go = Instantiate(letter.gameObject, icon);
            go.name = "Count";
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.45f, -0.15f);
            rect.anchorMax = new Vector2(1.25f, 0.45f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<TMP_Text>();
            text.text = string.Empty;
            text.enableAutoSizing = false;
            text.fontSize = letter.fontSize * 0.6f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.BottomRight;
            text.color = Color.white;
            text.outlineWidth = 0.25f;
            text.outlineColor = new Color32(0, 0, 0, 255);
            text.raycastTarget = false;
            return text;
        }
    }
}
