using System.Collections.Generic;
using RiskOfMagic.Core;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Grid of owned items. Slots are cloned from an inactive template child.</summary>
    public class InventoryWidget : HudWidget
    {
        [SerializeField] private ItemSlotWidget slotTemplate;
        [SerializeField] private RectTransform container;

        private readonly List<ItemSlotWidget> slots = new();

        protected override void OnBind()
        {
            Player.Inventory.ItemsChanged += Rebuild;
            Rebuild();
        }

        protected override void OnUnbind()
        {
            Player.Inventory.ItemsChanged -= Rebuild;
            foreach (var slot in slots)
                slot.gameObject.SetActive(false);
        }

        private void Rebuild()
        {
            var catalog = GameCatalog.Instance;
            if (catalog == null || slotTemplate == null || Player == null)
                return;

            int used = 0;
            var inventory = Player.Inventory;
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                int count = inventory.GetCountAt(i);
                var item = count > 0 ? catalog.GetItem(i) : null;
                if (item == null)
                    continue;

                if (used == slots.Count)
                    slots.Add(Instantiate(slotTemplate, container != null ? container : (RectTransform)slotTemplate.transform.parent));
                var slot = slots[used++];
                slot.gameObject.SetActive(true);
                slot.Set(item, count);
            }

            for (int i = used; i < slots.Count; i++)
                slots[i].gameObject.SetActive(false);
        }
    }
}
