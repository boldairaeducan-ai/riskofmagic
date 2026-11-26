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
        private readonly NetworkVariable<Vector3> launchFrom = new();
        private MaterialPropertyBlock propertyBlock;
        private float spawnTime;
        private Transform beam;
        private bool landed = true;
        private Vector3 visualScale = Vector3.one;
        private const float LaunchDuration = 0.55f;

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

        /// <summary>Server, before Spawn(): the visual flies from this point to the pickup in an arc (chest opening).</summary>
        public void SetLaunchOrigin(Vector3 worldPosition) => launchFrom.Value = worldPosition;

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
            landed = launchFrom.Value == Vector3.zero;
            if (floatingVisual != null)
                visualScale = floatingVisual.localScale;
            ApplyVisual();
        }

        public override void OnNetworkDespawn()
        {
            // Picked up (or cleared): a flash in the rarity color.
            if (Item != null || Equipment != null)
                RiskOfMagic.Feedback.HitSparks.Burst(VisualPosition, Vector3.up, DisplayColor, 22, 1.1f, 1.3f);
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
            EnsureBeam(color);
        }

        /// <summary>Light beam in the rarity color, visible from afar (taller for rarer items).</summary>
        private void EnsureBeam(Color color)
        {
            var config = RiskOfMagic.Feedback.HitFeedbackConfig.Instance;
            if (config == null || config.sparkMaterial == null)
                return;
            if (beam == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "RarityBeam";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                var renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterial = config.sparkMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                beam = go.transform;
            }
            float height = Item == null ? 3f : Item.rarity switch
            {
                ItemRarity.Common => 2.5f,
                ItemRarity.Uncommon => 4f,
                _ => 7f
            };
            beam.localPosition = Vector3.up * height;
            beam.localScale = new Vector3(0.12f, height, 0.12f); // cylinder = 2 m tall at scale 1
            var block = new MaterialPropertyBlock();
            var glow = new Color(color.r, color.g, color.b, 0.35f) * new Color(config.sparkGlow, config.sparkGlow, config.sparkGlow, 1f);
            block.SetColor(ColorId, glow);
            block.SetColor(BaseColorId, glow);
            beam.GetComponent<Renderer>().SetPropertyBlock(block);
        }

        /// <summary>Flies from the chest to the resting point in an arc, then lands with a burst.</summary>
        private void AnimateLaunch()
        {
            float t = (Time.time - spawnTime) / LaunchDuration;
            if (t >= 1f)
            {
                landed = true;
                floatingVisual.localScale = visualScale;
                RiskOfMagic.Feedback.HitSparks.Burst(VisualPosition, Vector3.up, DisplayColor, 16, 0.8f, 1.2f);
                return;
            }
            Vector3 from = launchFrom.Value;
            Vector3 to = floatingVisual.position;
            Vector3 position = Vector3.Lerp(from, to, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 2.2f);
            floatingVisual.position = position;
            floatingVisual.localScale = visualScale * Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(t * 2f));
        }

        private void Update()
        {
            if (floatingVisual != null)
            {
                floatingVisual.localPosition = Vector3.up * (floatHeight + Mathf.Sin(Time.time * 2f) * bobAmplitude);
                floatingVisual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
                if (!landed)
                    AnimateLaunch();
            }
            if (beam != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.25f;
                var scale = beam.localScale;
                beam.localScale = new Vector3(0.12f * pulse, scale.y, 0.12f * pulse);
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
