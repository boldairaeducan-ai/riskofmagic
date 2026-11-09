using UnityEngine;

namespace RiskOfMagic.Run.Secrets
{
    /// <summary>
    /// Level design: one per map scene. Each stage, the server picks one SecretAltar spot and
    /// <see cref="fragmentCount"/> SecretFragment spots at random among the SpawnSpots of the map (put more candidates
    /// than fragments). Bringing every fragment to the altar rolls the reward: a stage bonus, or this map's secret boss.
    /// </summary>
    public class MapSecret : MonoBehaviour
    {
        [Range(0f, 1f), Tooltip("Chance that the secret is present on a given stage.")] public float activeChance = 1f;
        [Min(1)] public int fragmentCount = 3;
        [Range(0f, 1f), Tooltip("Chance that solving it summons the secret boss instead of a stage bonus (needs a boss card).")]
        public float bossChance = 0.4f;
        [Tooltip("Secret boss of this map (a boss SpawnCard). Empty = always a stage bonus.")] public SpawnCard bossCard;
        [Min(0.1f)] public float bossHealthMultiplier = 1.5f;
        [Tooltip("Optional name announced when it appears (defaults to the enemy's name).")] public string bossTitle;

        public static MapSecret Current { get; private set; }

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            if (Current == this)
                Current = null;
        }
    }
}
