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
        [Min(0f), Tooltip("Not available before this difficulty coefficient.")] public float minCoefficient;
    }
}
