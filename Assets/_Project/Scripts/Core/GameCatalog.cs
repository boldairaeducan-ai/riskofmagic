using System.Collections.Generic;
using RiskOfMagic.Characters;
using RiskOfMagic.Combat.Projectiles;
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

        public CharacterDefinition GetCharacter(int index) => index >= 0 && index < characters.Count ? characters[index] : null;
        public ProjectileDefinition GetProjectile(int index) => index >= 0 && index < projectiles.Count ? projectiles[index] : null;
        public int IndexOf(ProjectileDefinition projectile) => projectiles.IndexOf(projectile);

#if UNITY_EDITOR
        public void EditorSet(List<CharacterDefinition> newCharacters, List<ProjectileDefinition> newProjectiles)
        {
            characters = newCharacters;
            projectiles = newProjectiles;
        }
#endif
    }
}
