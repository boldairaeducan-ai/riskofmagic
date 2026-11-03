using System.Collections.Generic;
using RiskOfMagic.Core;
using RiskOfMagic.Interaction;
using RiskOfMagic.Items;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>"Choose one item" menu opened by a ChoiceChest. "Later" closes it; the chest can be reopened with E.</summary>
    public class ChoiceMenu : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private OfferCardWidget cardTemplate;
        [SerializeField] private UnityEngine.UI.Button laterButton;

        private readonly List<OfferCardWidget> cards = new();
        private ChoiceChest chest;

        private void Awake()
        {
            if (cardTemplate != null)
                cardTemplate.gameObject.SetActive(false);
            if (laterButton != null)
                laterButton.onClick.AddListener(Close);
            SetVisible(false);
        }

        private void OnEnable() => ChoiceChest.LocalChoiceOffered += Open;
        private void OnDisable() => ChoiceChest.LocalChoiceOffered -= Open;

        private void Open(ChoiceChest offeringChest, int[] itemIndices)
        {
            chest = offeringChest;
            var catalog = GameCatalog.Instance;
            while (cards.Count < itemIndices.Length)
                cards.Add(Instantiate(cardTemplate, cardTemplate.transform.parent));

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                var item = i < itemIndices.Length ? catalog.GetItem(itemIndices[i]) : null;
                card.gameObject.SetActive(item != null);
                if (item == null)
                    continue;
                card.Set(item.displayName, $"<i>{ItemDefinition.GetRarityName(item.rarity)}</i>\n{item.description}", item.icon,
                    ItemDefinition.GetRarityColor(item.rarity), "Choisir", true);
                int choice = i;
                card.Button.onClick.RemoveAllListeners();
                card.Button.onClick.AddListener(() => Choose(choice));
            }

            SetVisible(true);
            if (PlayerAvatar.Local != null)
                PlayerAvatar.Local.Look.SetCursorLocked(false);
        }

        private void Choose(int choice)
        {
            if (chest != null)
                chest.Choose(choice);
            Close();
        }

        private void Close()
        {
            chest = null;
            SetVisible(false);
            if (PlayerAvatar.Local != null && PlayerAvatar.Local.IsAlive)
                PlayerAvatar.Local.Look.SetCursorLocked(true);
        }

        private void Update()
        {
            // The chest was despawned (new stage) while the menu was open.
            if (group != null && group.alpha > 0f && chest == null)
                SetVisible(false);
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
