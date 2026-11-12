using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Run;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    [CreateAssetMenu(menuName = "Risk of Magic/Enemies/ConeAttackData", fileName = "ConeAttackData")]
    public class ConeAttackData : EnemyAttackData
    {
        [Range(5f, 180f)] public float angle = 50f;
        public float damagePerTick = 6f;
        public DamageType damageType = DamageType.Fire;
        public DotEffect dot;
        [Tooltip("Status applied to each target hit by a tick.")] public bool applyStatus;
        public StatusType status = StatusType.Chilled;
        [Min(0.1f)] public float statusDuration = 1.2f;
        [Min(0f)] public float windup = 0.8f;
        [Min(0.1f)] public float duration = 2f;
        [Min(0.05f)] public float tickInterval = 0.25f;
        [Min(0f)] public float recovery = 0.8f;
        [Range(0f, 1f)] public float trackingFactor = 0.12f;
        public float originHeight = 1.2f;
        public LayerMask hitMask = Physics.DefaultRaycastLayers;
        [Tooltip("Ground marker during the windup (VfxNetwork).")] public GameObject telegraphVfx;
        [Tooltip("Spawned along the cone each tick (VfxNetwork).")] public GameObject tickVfx;
    }
}
