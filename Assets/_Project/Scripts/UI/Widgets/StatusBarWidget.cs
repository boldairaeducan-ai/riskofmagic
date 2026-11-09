using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Local player's statuses: one icon per active status (ArtLibrary icon, else its initial), stack count,
    /// remaining-time sweep. Buffs are framed with the accent color, debuffs with the danger color.
    /// </summary>
    public class StatusBarWidget : HudWidget
    {
        [SerializeField, Tooltip("Cloned per status: Image (frame) > Icon (Image), Letter (TMP), Stacks (TMP), Timer (Image, veil rising with elapsed time).")]
        private RectTransform slotTemplate;

        private class Slot
        {
            public RectTransform Root;
            public Image Frame, Icon, Timer;
            public TMP_Text Letter, Stacks;
        }

        private static readonly DamageType[] Dots = { DamageType.Fire, DamageType.Poison, DamageType.Bleed };
        private readonly List<Slot> slots = new();
        /// <summary>Per status: when the current duration started (reset when Until moves forward) and its Until.</summary>
        private readonly Dictionary<StatusType, (double start, double until)> timings = new();
        private readonly List<StatusType> ended = new();

        protected override void OnBind()
        {
            if (slotTemplate != null)
                slotTemplate.gameObject.SetActive(false);
        }

        protected override void OnUnbind()
        {
            foreach (var slot in slots)
                slot.Root.gameObject.SetActive(false);
            timings.Clear();
        }

        protected override void Tick()
        {
            var status = Player.Status;
            if (status == null || slotTemplate == null)
                return;
            var library = ArtLibrary.Instance;
            double now = status.CurrentTime;
            int used = 0;
            for (int i = 0; i < status.EntryCount; i++)
            {
                var entry = status.GetEntry(i);
                if (entry.Until <= now)
                    continue;
                var art = library != null ? library.GetStatus(entry.Type) : null;
                if (art != null && !art.visible)
                    continue;
                if (!timings.TryGetValue(entry.Type, out var timing) || entry.Until > timing.until + 0.01d)
                    timing = (now, entry.Until); // new or refreshed
                timings[entry.Type] = timing;
                Fill(Get(used++), entry, art, now, timing.start);
            }
            // Damage over time (replicated mask: no stacks / duration).
            foreach (var type in Dots)
            {
                if (Player.Health == null || !Player.Health.HasDot(type))
                    continue;
                var art = library != null ? library.GetDot(type) : null;
                var slot = Get(used++);
                Fill(slot, new StatusEntry { Stacks = 1, Until = double.MaxValue }, null, now, now);
                var sprite = art != null ? art.icon : null;
                var color = theme != null ? theme.dangerColor : Color.red;
                if (slot.Icon != null)
                {
                    slot.Icon.sprite = sprite;
                    slot.Icon.color = sprite != null ? Color.white : new Color(color.r, color.g, color.b, 0.25f);
                }
                if (slot.Letter != null)
                    slot.Letter.text = sprite != null ? string.Empty : (art != null ? art.name : type.ToString()).Substring(0, 1);
                if (slot.Frame != null)
                    slot.Frame.color = color;
            }
            for (int i = used; i < slots.Count; i++)
                slots[i].Root.gameObject.SetActive(false);

            ended.Clear();
            foreach (var type in timings.Keys)
                if (status.GetStacks(type) == 0)
                    ended.Add(type);
            foreach (var type in ended)
                timings.Remove(type);
        }

        private void Fill(Slot slot, StatusEntry entry, ArtLibrary.StatusArt art, double now, double start)
        {
            slot.Root.gameObject.SetActive(true);
            bool debuff = art != null && art.debuff;
            var color = theme != null ? (debuff ? theme.dangerColor : theme.accentColor) : Color.white;
            if (slot.Frame != null)
                slot.Frame.color = color;
            var sprite = art != null ? art.icon : null;
            if (slot.Icon != null)
            {
                slot.Icon.sprite = sprite;
                slot.Icon.color = sprite != null ? Color.white : new Color(color.r, color.g, color.b, 0.25f);
            }
            if (slot.Letter != null)
            {
                string name = art != null && !string.IsNullOrEmpty(art.name) ? art.name : entry.Type.ToString();
                slot.Letter.text = sprite != null ? string.Empty : name.Substring(0, 1);
            }
            if (slot.Stacks != null)
                slot.Stacks.text = entry.Stacks > 1 ? entry.Stacks.ToString() : string.Empty;
            if (slot.Timer != null)
            {
                bool timed = entry.Until < double.MaxValue * 0.5;
                slot.Timer.enabled = timed;
                if (timed)
                {
                    double total = System.Math.Max(0.01, entry.Until - start);
                    SetFill(slot.Timer.rectTransform, 1f - Mathf.Clamp01((float)((entry.Until - now) / total)), vertical: true); // dark veil rises
                }
            }
        }

        private Slot Get(int index)
        {
            while (slots.Count <= index)
            {
                var root = Instantiate(slotTemplate, slotTemplate.parent);
                slots.Add(new Slot
                {
                    Root = root,
                    Frame = root.GetComponent<Image>(),
                    Icon = Child<Image>(root, "Icon"),
                    Timer = Child<Image>(root, "Timer"),
                    Letter = Child<TMP_Text>(root, "Letter"),
                    Stacks = Child<TMP_Text>(root, "Stacks"),
                });
            }
            return slots[index];
        }

        private static T Child<T>(Transform root, string name) where T : Component
        {
            var child = root.Find(name);
            return child != null ? child.GetComponent<T>() : null;
        }
    }
}
