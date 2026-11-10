using System.Collections.Generic;
using RiskOfMagic.Equipment;
using RiskOfMagic.Interaction;
using RiskOfMagic.Items;
using RiskOfMagic.Player;
using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Shop menu: item cards + one equipment card, reroll, close. Closes when walking away.</summary>
    public class MerchantMenu : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text goldLabel;
        [SerializeField] private OfferCardWidget cardTemplate;
        [SerializeField] private UnityEngine.UI.Button rerollButton;
        [SerializeField] private TMP_Text rerollLabel;
        [SerializeField] private UnityEngine.UI.Button closeButton;

        private readonly List<OfferCardWidget> cards = new();
        private Merchant merchant;

        private void Awake()
        {
            if (cardTemplate != null)
                cardTemplate.gameObject.SetActive(false);
            if (rerollButton != null)
                rerollButton.onClick.AddListener(() => { if (merchant != null) merchant.RequestReroll(); });
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
            SetVisible(false);
        }

        private void OnEnable() => Merchant.LocalShopOpened += Open;
        private void OnDisable() => Merchant.LocalShopOpened -= Open;

        private void Open(Merchant opened)
        {
            if (merchant != null)
                merchant.StockChanged -= Refresh;
            merchant = opened;
            merchant.StockChanged += Refresh;
            if (titleLabel != null)
                titleLabel.text = merchant.DisplayName;
            Refresh();
            SetVisible(true);
            if (PlayerAvatar.Local != null)
                PlayerAvatar.Local.Look.SetCursorLocked(false);
        }

        private void Close()
        {
            if (merchant != null)
                merchant.StockChanged -= Refresh;
            merchant = null;
            SetVisible(false);
            if (PlayerAvatar.Local != null && PlayerAvatar.Local.IsAlive)
                PlayerAvatar.Local.Look.SetCursorLocked(true);
        }

        private void Update()
        {
            if (merchant == null)
            {
                if (group != null && group.alpha > 0f)
                    Close();
                return;
            }

            var player = PlayerAvatar.Local;
            if (player == null || !player.IsAlive || !merchant.IsSpawned || merchant.IsSold || !merchant.IsInRange(player))
            {
                Close();
                return;
            }
            RefreshAffordability(player);
        }

        private void Refresh()
        {
            if (merchant == null || cardTemplate == null)
                return;
            int needed = merchant.ItemSlots + 1;
            while (cards.Count < needed)
                cards.Add(Instantiate(cardTemplate, cardTemplate.transform.parent));

            for (int i = 0; i < merchant.ItemSlots; i++)
            {
                int slot = i;
                var item = merchant.GetItem(slot);
                SetupCard(cards[i], item != null, () => { if (merchant != null) merchant.RequestBuyItem(slot); });
                if (item != null)
                    cards[i].Set(item.displayName, $"<i>{ItemDefinition.GetRarityName(item.rarity)}</i>\n{item.description}", item.icon,
                        ItemDefinition.GetRarityColor(item.rarity), $"{merchant.GetItemPrice(slot)} or", true);
            }

            var equipment = merchant.GetEquipment();
            var equipmentCard = cards[merchant.ItemSlots];
            SetupCard(equipmentCard, equipment != null, () => { if (merchant != null) merchant.RequestBuyEquipment(); });
            if (equipment != null)
                equipmentCard.Set(equipment.displayName, $"<i>Équipement (F) · recharge {equipment.cooldown:0} s</i>\n{equipment.description}",
                    equipment.icon, EquipmentDefinition.DisplayColor, $"{merchant.GetEquipmentPrice()} or", true);

            if (PlayerAvatar.Local != null)
                RefreshAffordability(PlayerAvatar.Local);
        }

        private static void SetupCard(OfferCardWidget card, bool available, UnityEngine.Events.UnityAction onBuy)
        {
            card.gameObject.SetActive(available);
            card.Button.onClick.RemoveAllListeners();
            card.Button.onClick.AddListener(onBuy);
        }

        private void RefreshAffordability(PlayerAvatar player)
        {
            uint gold = player.Wallet.Gold;
            if (goldLabel != null)
                goldLabel.text = $"Or : {gold}";
            for (int i = 0; i < merchant.ItemSlots && i < cards.Count; i++)
                cards[i].Button.interactable = merchant.GetItem(i) != null && gold >= merchant.GetItemPrice(i);
            if (cards.Count > merchant.ItemSlots)
                cards[merchant.ItemSlots].Button.interactable = merchant.GetEquipment() != null && gold >= merchant.GetEquipmentPrice();

            uint rerollPrice = merchant.GetRerollPrice();
            if (rerollLabel != null)
                rerollLabel.text = $"Relancer ({rerollPrice} or)";
            if (rerollButton != null)
                rerollButton.interactable = gold >= rerollPrice;
        }

        private void SetVisible(bool visible)
        {
            if (group == null)
                return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
