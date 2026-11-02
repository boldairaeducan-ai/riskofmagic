using System.Collections.Generic;
using RiskOfMagic.Core;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Items
{
    /// <summary>Floating item dropped by chests; the first living player walking into it gets it.</summary>
    public class ItemPickup : NetworkBehaviour
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
        private MaterialPropertyBlock propertyBlock;
        private float spawnTime;

        public static IReadOnlyList<ItemPickup> Active => active;

        public ItemDefinition Item => GameCatalog.Instance != null ? GameCatalog.Instance.GetItem(itemIndex.Value) : null;
        public Vector3 VisualPosition => transform.position + Vector3.up * floatHeight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active.Clear();

        /// <summary>Server, before Spawn().</summary>
        public void Initialize(ItemDefinition item) => itemIndex.Value = GameCatalog.Instance.IndexOf(item);

        public override void OnNetworkSpawn()
        {
            active.Add(this);
            spawnTime = Time.time;
            itemIndex.OnValueChanged += OnItemChanged;
            ApplyVisual();
        }

        public override void OnNetworkDespawn()
        {
            active.Remove(this);
            itemIndex.OnValueChanged -= OnItemChanged;
        }

        private void OnItemChanged(int previous, int current) => ApplyVisual();

        private void ApplyVisual()
        {
            var item = Item;
            if (item == null || floatingVisual == null)
                return;
            var color = ItemDefinition.GetRarityColor(item.rarity);
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
