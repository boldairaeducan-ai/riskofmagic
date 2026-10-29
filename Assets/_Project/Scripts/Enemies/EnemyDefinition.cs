using UnityEngine;

namespace RiskOfMagic.Enemies
{
    [CreateAssetMenu(menuName = "Risk of Magic/Enemy Definition", fileName = "EnemyDefinition")]
    public class EnemyDefinition : ScriptableObject
    {
        public string displayName = "Enemy";

        [Header("Stats")]
        [Min(1f)] public float maxHealth = 50f;
        [Min(0f)] public float moveSpeed = 4.5f;
        [Min(0f)] public float acceleration = 20f;
        [Min(0f)] public float angularSpeed = 540f;

        [Header("Perception")]
        [Min(0f)] public float detectionRange = 200f;

        [Header("Rewards")]
        [Min(0)] public int scoreReward = 10;

        [Header("Lifecycle")]
        [Min(0f)] public float despawnDelay = 2f;
    }
}
