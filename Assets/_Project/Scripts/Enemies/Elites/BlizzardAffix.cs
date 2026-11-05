using RiskOfMagic.Combat;
using UnityEngine;

namespace RiskOfMagic.Enemies.Elites
{
    /// <summary>Glacial: permanent blizzard around it, low damage + slow (Chilled) on players inside.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Elites/Blizzard", fileName = "Elite_Blizzard")]
    public class BlizzardAffix : EliteAffix
    {
        [Min(1f), Tooltip("Keep in sync with the aura prefab size.")] public float radius = 6f;
        public float damagePerTick = 2f;
        [Min(0.1f)] public float tickInterval = 0.5f;
        [Min(0.1f)] public float chillDuration = 1.2f;

        public override void ServerTick(EliteRuntime elite)
        {
            if (Time.time < elite.Timer)
                return;
            elite.Timer = Time.time + tickInterval;
            elite.DamagePlayersAround(elite.transform.position, radius, damagePerTick, DamageType.Frost,
                player =>
                {
                    if (player.Status != null)
                        player.Status.Apply(StatusType.Chilled, chillDuration);
                });
        }
    }
}
