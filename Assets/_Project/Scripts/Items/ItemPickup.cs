using System.Collections.Generic;
using RiskOfMagic.Core;
using RiskOfMagic.Equipment;
using RiskOfMagic.Interaction;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Items
{
    /// <summary>
    /// Floating pickup. Items are taken by the first living player walking into them;
    /// equipment requires an interaction (E) and swaps with the current one.
    /// </summary>
    public class ItemPickup : NetworkBehaviour, IInteractable
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly List<ItemPickup> active = new();

        [SerializeField] private Transform floatingVisual;
        [SerializeField] private float pickupRadius = 1.6f;
        [SerializeField] private float pickupDelay = 0.6f;
        [SerializeField] private float floatHeight = 0.8f;
        [SerializeField] private float bobAmplitude = 0.2f;
        [SerializeField] private float spinSpeed = 90f;

        private readonly NetworkVariable<int> itemIndex = new(-1);
        private readonly NetworkVariable<int> equipmentIndex = new(-1);
        private MaterialPropertyBlock propertyBlock;
        private float spawnTime;

        public static IReadOnlyList<ItemPickup> Active => active;

        public ItemDefinition Item => GameCatalog.Instance != null ? GameCatalog.Instance.GetItem(itemIndex.Value) : null;
        public EquipmentDefinition Equipment => GameCatalog.Instance != null ? GameCatalog.Instance.GetEquipment(equipmentIndex.Value) : null;
        public string DisplayName => Item != null ? Item.displayName : Equipment != null ? Equipment.displayName : string.Empty;
        public Color DisplayColor => Item != null ? ItemDefinition.GetRarityColor(Item.rarity) : EquipmentDefinition.DisplayColor;
        public Vector3 VisualPosition => transform.position + Vector3.up * floatHeight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active.Clear();

        /// <summary>Server only, uses GameCatalog.PickupPrefab.</summary>
        public static ItemPickup SpawnItem(ItemDefinition item, Vector3 position) => Spawn(position, p => p.Initialize(item));

        /// <summary>Server only, uses GameCatalog.PickupPrefab.</summary>
        public static ItemPickup SpawnEquipment(EquipmentDefinition equipment, Vector3 position) => Spawn(position, p => p.Initialize(equipment));

        private static ItemPickup Spawn(Vector3 position, System.Action<ItemPickup> init)
        {
            var prefab = GameCatalog.Instance != null ? GameCatalog.Instance.PickupPrefab : null;
            if (prefab == null)
            {
                Debug.LogError("[ItemPickup] GameCatalog has no pickup prefab.");
                return null;
            }
            var pickup = Instantiate(prefab, position, Quaternion.identity);
            init(pickup);
            pickup.NetworkObject.Spawn(true);
            return pickup;
        }

        /// <summary>Server, before Spawn().</summary>
        public void Initialize(ItemDefinition item) => itemIndex.Value = GameCatalog.Instance.IndexOf(item);

        /// <summary>Server, before Spawn().</summary>
        public void Initialize(EquipmentDefinition equipment) => equipmentIndex.Value = GameCatalog.Instance.IndexOf(equipment);

        public override void OnNetworkSpawn()
        {
            active.Add(this);
            spawnTime = Time.time;
            itemIndex.OnValueChanged += OnContentChanged;
            equipmentIndex.OnValueChanged += OnContentChanged;
            ApplyVisual();
        }

        public override void OnNetworkDespawn()
        {
            active.Remove(this);
            itemIndex.OnValueChanged -= OnContentChanged;
            equipmentIndex.OnValueChanged -= OnContentChanged;
        }

        // ---------------------------------------------------------------- Equipment interaction

        public bool CanInteract(PlayerAvatar player) => Equipment != null && player.IsAlive && player.Equipment != null;

        public string GetPrompt(PlayerAvatar player)
            => Equipment != null ? $"[E] Prendre {Equipment.displayName} (équipement)" : string.Empty;

        public void Interact(PlayerAvatar player)
        {
            var equipment = Equipment;
            if (!IsServer || equipment == null || player.Equipment == null)
                return;
            NetworkObject.Despawn();
            player.Equipment.SetEquipment(equipment);
        }

        // ---------------------------------------------------------------- Visuals & item pickup

        private void OnContentChanged(int previous, int current) => ApplyVisual();

        private void ApplyVisual()
        {
            if (floatingVisual == null || (Item == null && Equipment == null))
                return;
            var color = DisplayColor;
            propertyBlock ??= new MaterialPropertyBlock();
            foreach (var r in floatingVisual.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(ColorId, color);
                propertyBlock.SetColor(BaseColorId, color);
                r.SetPropertyBlock(propertyBlock);
            }
        }

        private void Update()
        {
            if (floatingVisual != null)
            {
                floatingVisual.localPosition = Vector3.up * (floatHeight + Mathf.Sin(Time.time * 2f) * bobAmplitude);
                floatingVisual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            }

            if (!IsServer || !IsSpawned || Time.time < spawnTime + pickupDelay)
                return;
            var item = Item;
            if (item == null)
                return;

            foreach (var player in PlayerAvatar.All)
            {
                if (!player.IsAlive)
                    continue;
                Vector3 offset = player.transform.position + Vector3.up * 0.9f - VisualPosition;
                if (offset.sqrMagnitude > pickupRadius * pickupRadius)
                    continue;
                player.Inventory.AddItem(item);
                NetworkObject.Despawn();
                return;
            }
        }
    }
}
