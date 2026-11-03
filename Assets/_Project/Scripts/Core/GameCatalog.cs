using System.Collections.Generic;
using RiskOfMagic.Characters;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Equipment;
using RiskOfMagic.Items;
using RiskOfMagic.Maps;
using UnityEngine;

namespace RiskOfMagic.Core
{
    /// <summary>
    /// Global registry giving stable network ids (list index) to shared assets.
    /// Must live at Resources/GameCatalog.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Game Catalog", fileName = "GameCatalog")]
    public class GameCatalog : ScriptableObject
    {
        public const string ResourcePath = "GameCatalog";

        private static GameCatalog instance;

        [SerializeField] private List<CharacterDefinition> characters = new();
        [SerializeField] private List<ProjectileDefinition> projectiles = new();
        [SerializeField] private List<ItemDefinition> items = new();
        [SerializeField] private List<GameObject> effects = new();
        [SerializeField] private List<EquipmentDefinition> equipment = new();
        [SerializeField, Tooltip("Prefab with an ItemPickup, used to drop items and equipment.")] private ItemPickup pickupPrefab;
        [SerializeField, Tooltip("Map scenes per stage.")] private MapRotation mapRotation;

        private readonly List<ItemDefinition> rollBuffer = new();

        public static GameCatalog Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<GameCatalog>(ResourcePath);
                    if (instance == null)
                        Debug.LogError($"[GameCatalog] Missing Resources/{ResourcePath} asset.");
                }
                return instance;
            }
        }

        public IReadOnlyList<CharacterDefinition> Characters => characters;
        public IReadOnlyList<ProjectileDefinition> Projectiles => projectiles;
        public IReadOnlyList<ItemDefinition> Items => items;
        public IReadOnlyList<EquipmentDefinition> Equipment => equipment;
        public ItemPickup PickupPrefab => pickupPrefab;
        public MapRotation MapRotation => mapRotation;

        public CharacterDefinition GetCharacter(int index) => Get(characters, index);
        public ProjectileDefinition GetProjectile(int index) => Get(projectiles, index);
        public ItemDefinition GetItem(int index) => Get(items, index);
        public GameObject GetEffect(int index) => Get(effects, index);
        public EquipmentDefinition GetEquipment(int index) => Get(equipment, index);
        public int IndexOf(EquipmentDefinition definition) => equipment.IndexOf(definition);

        public EquipmentDefinition RollEquipment()
            => equipment.Count > 0 ? equipment[Random.Range(0, equipment.Count)] : null;

        /// <summary>Up to <paramref name="count"/> distinct items of one rarity (fewer if the pool is small).</summary>
        public List<ItemDefinition> RollDistinctItems(ItemRarity rarity, int count)
        {
            var pool = new List<ItemDefinition>();
            for (int r = (int)rarity; r >= 0 && pool.Count == 0; r--)
                foreach (var item in items)
                    if (item != null && (int)item.rarity == r)
                        pool.Add(item);

            var result = new List<ItemDefinition>();
            while (result.Count < count && pool.Count > 0)
            {
                int index = Random.Range(0, pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return result;
        }

        public int IndexOf(ProjectileDefinition projectile) => projectiles.IndexOf(projectile);
        public int IndexOf(ItemDefinition item) => items.IndexOf(item);
        public int IndexOfEffect(GameObject effect) => effects.IndexOf(effect);

        /// <summary>Random item of the given rarity, falling back to lower rarities if none exist.</summary>
        public ItemDefinition RollItem(ItemRarity rarity)
        {
            for (int r = (int)rarity; r >= 0; r--)
            {
                rollBuffer.Clear();
                foreach (var item in items)
                    if (item != null && (int)item.rarity == r)
                        rollBuffer.Add(item);
                if (rollBuffer.Count > 0)
                    return rollBuffer[Random.Range(0, rollBuffer.Count)];
            }
            return null;
        }

        private static T Get<T>(List<T> list, int index) where T : Object
            => index >= 0 && index < list.Count ? list[index] : null;

#if UNITY_EDITOR
        public void EditorSetMapRotation(MapRotation rotation) => mapRotation = rotation;

        public void EditorSet(List<CharacterDefinition> newCharacters, List<ProjectileDefinition> newProjectiles,
            List<ItemDefinition> newItems, List<GameObject> newEffects, List<EquipmentDefinition> newEquipment, ItemPickup newPickupPrefab)
        {
            characters = newCharacters;
            projectiles = newProjectiles;
            items = newItems;
            effects = newEffects;
            equipment = newEquipment;
            pickupPrefab = newPickupPrefab;
        }
#endif
    }
}
