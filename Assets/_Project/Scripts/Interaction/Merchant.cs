using System;
using RiskOfMagic.Core;
using RiskOfMagic.Equipment;
using RiskOfMagic.Items;
using RiskOfMagic.Player;
using RiskOfMagic.Run;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Interaction
{
    /// <summary>
    /// NPC shop: a few items + one equipment with prices by rarity (scaled by difficulty when the stage starts),
    /// and a reroll whose price grows with each use. Only ONE purchase per merchant: the first buyer (server order)
    /// gets it and the merchant leaves for everyone. Purchases are validated by the server.
    /// </summary>
    public class Merchant : NetworkBehaviour, IInteractable
    {
        [SerializeField] private string displayName = "Marchand";
        [SerializeField, Min(1)] private int itemSlots = 4;
        [SerializeField] private RarityWeights stockWeights = new(55f, 30f, 12f, 3f);

        [Header("Prices (before difficulty scaling)")]
        [SerializeField] private int commonPrice = 30;
        [SerializeField] private int uncommonPrice = 60;
        [SerializeField] private int rarePrice = 120;
        [SerializeField] private int legendaryPrice = 300;
        [SerializeField] private int equipmentPrice = 80;
        [SerializeField] private int rerollBasePrice = 15;
        [SerializeField, Min(1f)] private float rerollPriceMultiplier = 1.5f;
        [SerializeField] private float costExponent = 1.25f;

        [Header("Leaving")]
        [SerializeField, Min(0f), Tooltip("Delay between the purchase and the merchant leaving.")] private float leaveDelay = 0.6f;
        [SerializeField, Tooltip("Spawned where the merchant stood when it leaves (VfxNetwork).")] private GameObject leaveVfx;

        [Header("Interaction")]
        [SerializeField, Min(1f)] private float maxShoppingDistance = 8f;

        private NetworkList<int> itemStock;
        private readonly NetworkVariable<int> equipmentStock = new(-1);
        private readonly NetworkVariable<int> rerolls = new();
        private readonly NetworkVariable<float> priceCoefficient = new(1f);
        private readonly NetworkVariable<bool> sold = new();

        /// <summary>Raised on the shopper's machine.</summary>
        public static event Action<Merchant> LocalShopOpened;
        /// <summary>Every peer: stock or prices changed.</summary>
        public event Action StockChanged;

        public string DisplayName => displayName;
        public int ItemSlots => itemStock.Count;
        public float MaxShoppingDistance => maxShoppingDistance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => LocalShopOpened = null;

        private void Awake() => itemStock = new NetworkList<int>();

        public override void OnNetworkSpawn()
        {
            itemStock.OnListChanged += OnStockListChanged;
            equipmentStock.OnValueChanged += OnStockValueChanged;
            rerolls.OnValueChanged += OnStockValueChanged;
            priceCoefficient.OnValueChanged += OnPriceChanged;
            sold.OnValueChanged += OnSoldChanged;
            if (!IsServer)
                return;
            priceCoefficient.Value = Mathf.Pow(RunDifficulty.CurrentCoefficient, costExponent);
            RollStock();
        }

        public override void OnNetworkDespawn()
        {
            itemStock.OnListChanged -= OnStockListChanged;
            equipmentStock.OnValueChanged -= OnStockValueChanged;
            rerolls.OnValueChanged -= OnStockValueChanged;
            priceCoefficient.OnValueChanged -= OnPriceChanged;
            sold.OnValueChanged -= OnSoldChanged;
        }

        private void OnSoldChanged(bool previous, bool current) => StockChanged?.Invoke();

        // ---------------------------------------------------------------- Read (every peer)

        public ItemDefinition GetItem(int slot) => slot >= 0 && slot < itemStock.Count ? GameCatalog.Instance.GetItem(itemStock[slot]) : null;
        public EquipmentDefinition GetEquipment() => GameCatalog.Instance.GetEquipment(equipmentStock.Value);

        public uint GetItemPrice(int slot)
        {
            var item = GetItem(slot);
            if (item == null)
                return 0;
            int basePrice = item.rarity switch
            {
                ItemRarity.Uncommon => uncommonPrice,
                ItemRarity.Rare => rarePrice,
                ItemRarity.Legendary or ItemRarity.Boss => legendaryPrice,
                _ => commonPrice
            };
            return Scale(basePrice);
        }

        public uint GetEquipmentPrice() => Scale(equipmentPrice);
        public uint GetRerollPrice() => Scale(rerollBasePrice * Mathf.Pow(rerollPriceMultiplier, rerolls.Value));

        private uint Scale(float price) => (uint)Mathf.Max(1, Mathf.RoundToInt(price * priceCoefficient.Value));

        public bool IsInRange(PlayerAvatar player)
            => player != null && (player.transform.position - transform.position).sqrMagnitude <= maxShoppingDistance * maxShoppingDistance;

        // ---------------------------------------------------------------- Interaction

        /// <summary>Something was bought: closed for everyone, about to leave.</summary>
        public bool IsSold => sold.Value;

        public bool CanInteract(PlayerAvatar player) => player.IsAlive && !sold.Value;
        public string GetPrompt(PlayerAvatar player) => sold.Value ? string.Empty : $"[E] {displayName} (un seul achat)";

        public void Interact(PlayerAvatar player)
        {
            if (IsServer)
                OpenShopRpc(RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void OpenShopRpc(RpcParams rpcParams = default) => LocalShopOpened?.Invoke(this);

        // ---------------------------------------------------------------- Purchases (client → server)

        public void RequestBuyItem(int slot) => BuyItemRpc(slot);
        public void RequestBuyEquipment() => BuyEquipmentRpc();
        public void RequestReroll() => RerollRpc();

        [Rpc(SendTo.Server)]
        private void BuyItemRpc(int slot, RpcParams rpcParams = default)
        {
            var player = GetShopper(rpcParams);
            var item = GetItem(slot);
            if (sold.Value || player == null || item == null || !player.Wallet.TrySpend(GetItemPrice(slot)))
                return;
            player.Inventory.AddItem(item);
            itemStock[slot] = -1;
            Leave();
        }

        [Rpc(SendTo.Server)]
        private void BuyEquipmentRpc(RpcParams rpcParams = default)
        {
            var player = GetShopper(rpcParams);
            var equipment = GetEquipment();
            if (sold.Value || player == null || equipment == null || player.Equipment == null || !player.Wallet.TrySpend(GetEquipmentPrice()))
                return;
            player.Equipment.SetEquipment(equipment);
            equipmentStock.Value = -1;
            Leave();
        }

        [Rpc(SendTo.Server)]
        private void RerollRpc(RpcParams rpcParams = default)
        {
            var player = GetShopper(rpcParams);
            if (sold.Value || player == null || !player.Wallet.TrySpend(GetRerollPrice()))
                return;
            rerolls.Value++;
            RollStock();
        }

        /// <summary>Server: first purchase wins; the merchant despawns for everyone (open shop menus close).</summary>
        private void Leave()
        {
            sold.Value = true;
            StartCoroutine(LeaveAfterDelay());
        }

        private System.Collections.IEnumerator LeaveAfterDelay()
        {
            yield return new WaitForSeconds(leaveDelay);
            RiskOfMagic.Vfx.VfxNetwork.Spawn(leaveVfx, transform.position, Quaternion.identity, 2f);
            if (IsSpawned)
                NetworkObject.Despawn();
        }

        private PlayerAvatar GetShopper(RpcParams rpcParams)
        {
            var player = PlayerAvatar.FindByClient(rpcParams.Receive.SenderClientId);
            return player != null && player.IsAlive && IsInRange(player) ? player : null;
        }

        private void RollStock()
        {
            var catalog = GameCatalog.Instance;
            if (catalog == null)
                return;

            while (itemStock.Count < itemSlots)
                itemStock.Add(-1);
            for (int i = 0; i < itemSlots; i++)
            {
                var item = catalog.RollItem(stockWeights.Roll());
                itemStock[i] = item != null ? catalog.IndexOf(item) : -1;
            }
            var equipment = catalog.RollEquipment();
            equipmentStock.Value = equipment != null ? catalog.IndexOf(equipment) : -1;
        }

        private void OnStockListChanged(NetworkListEvent<int> change) => StockChanged?.Invoke();
        private void OnStockValueChanged(int previous, int current) => StockChanged?.Invoke();
        private void OnPriceChanged(float previous, float current) => StockChanged?.Invoke();
    }
}
