using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Run;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    [CreateAssetMenu(menuName = "Risk of Magic/Enemies/EruptionAttackData", fileName = "EruptionAttackData")]
    public class EruptionAttackData : EnemyAttackData
    {
        public EruptionPattern pattern = EruptionPattern.UnderPlayers;
        public float damage = 25f;
        public DamageType damageType = DamageType.Physical;
        [Min(0.3f)] public float radius = 2.5f;
        [Min(0f)] public float windup = 0.6f;
        [Min(0.2f), Tooltip("Marker → impact. Keep it under the telegraph VFX lifetime.")] public float delay = 1.2f;
        [Min(0f), Tooltip("Line: delay between two consecutive impacts.")] public float stepDelay = 0.12f;
        [Min(1), Tooltip("Line/Ring: impacts. UnderPlayers: extra impacts around each player.")] public int count = 6;
        [Min(0f), Tooltip("Line: distance between impacts. Ring: ring radius. UnderPlayers: extra impact offset.")] public float spacing = 3f;
        [Min(0f)] public float recovery = 0.8f;
        public bool applyStatus;
        public StatusType status = StatusType.Chilled;
        [Min(0.1f)] public float statusDuration = 1.5f;
        [Min(0f)] public float hazardDuration;
        public float hazardDamagePerTick = 5f;
        [Min(0.1f)] public float hazardTick = 0.5f;
        [Tooltip("Respawned every vfx lifetime while the hazard lasts.")] public GameObject hazardVfx;
        [Min(0.1f)] public float hazardVfxLifetime = 3f;
        public GameObject telegraphVfx;
        public GameObject impactVfx;
        public LayerMask groundMask = Physics.DefaultRaycastLayers;
    }
}
