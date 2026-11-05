using RiskOfMagic.Enemies;
using UnityEngine;

namespace RiskOfMagic.Run
{
    /// <summary>What the director can buy. The prefab must be registered in the NetworkPrefabs list.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Spawn Card", fileName = "SpawnCard")]
    public class SpawnCard : ScriptableObject
    {
        public EnemyActor prefab;
        [Min(1)] public int cost = 10;
        [Min(0f)] public float weight = 1f;
        [Min(0f), Tooltip("Not available before this run progress (difficulty coefficient without the player factor, 1 at start).")]
        public float minCoefficient;
        [Min(1), Tooltip("Bosses: first stage where the teleporter can pick this card.")]
        public int minStage = 1;
    }
}
