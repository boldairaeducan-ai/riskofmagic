using System;
using RiskOfMagic.Core;
using RiskOfMagic.Items;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Interaction
{
    /// <summary>
    /// Special chest: the buyer chooses one of several items of the same rarity in a menu (the game keeps running).
    /// Closing the menu keeps the offer: interacting again reopens it.
    /// </summary>
    public class ChoiceChest : ChestInteractable
    {
        [SerializeField, Min(1)] private int choiceCount = 3;

        private readonly NetworkVariable<ulong> buyerClient = new(ulong.MaxValue);
        private int[] offered;

        /// <summary>Raised on the buyer's machine: (chest, offered item indices in the GameCatalog).</summary>
        public static event Action<ChoiceChest, int[]> LocalChoiceOffered;

        private bool IsPending => buyerClient.Value != ulong.MaxValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => LocalChoiceOffered = null;

        public override bool CanInteract(PlayerAvatar player)
            => IsPending ? player.OwnerClientId == buyerClient.Value : base.CanInteract(player);

        public override string GetPrompt(PlayerAvatar player)
        {
            if (!IsPending)
                return base.GetPrompt(player);
            return player.OwnerClientId == buyerClient.Value ? "[E] Choisir un objet" : string.Empty;
        }

        public override void Interact(PlayerAvatar player)
        {
            if (!IsServer)
                return;
            if (IsPending)
            {
                if (player.OwnerClientId == buyerClient.Value)
                    SendOffer(player);
                return;
            }
            base.Interact(player);
        }

        protected override void OnPurchased(PlayerAvatar buyer)
        {
            var catalog = GameCatalog.Instance;
            var items = catalog.RollDistinctItems(rarityWeights.Roll(), choiceCount);
            if (items.Count == 0)
                return;

            offered = new int[items.Count];
            for (int i = 0; i < items.Count; i++)
                offered[i] = catalog.IndexOf(items[i]);
            buyerClient.Value = buyer.OwnerClientId;
            SendOffer(buyer);
        }

        private void SendOffer(PlayerAvatar player)
        {
            if (offered != null)
                OfferRpc(offered, RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void OfferRpc(int[] itemIndices, RpcParams rpcParams = default) => LocalChoiceOffered?.Invoke(this, itemIndices);

        /// <summary>Called by the buyer's UI.</summary>
        public void Choose(int choice) => ChooseRpc(choice);

        [Rpc(SendTo.Server)]
        private void ChooseRpc(int choice, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (offered == null || sender != buyerClient.Value || choice < 0 || choice >= offered.Length)
                return;
            var player = PlayerAvatar.FindByClient(sender);
            var item = GameCatalog.Instance.GetItem(offered[choice]);
            if (player == null || item == null)
                return;

            player.Inventory.AddItem(item);
            offered = null;
            buyerClient.Value = ulong.MaxValue;
        }
    }
}
