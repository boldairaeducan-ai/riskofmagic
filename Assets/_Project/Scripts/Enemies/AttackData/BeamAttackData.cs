using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Run;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    [CreateAssetMenu(menuName = "Risk of Magic/Enemies/BeamAttackData", fileName = "BeamAttackData")]
    public class BeamAttackData : EnemyAttackData
    {
        [Min(1)] public int beams = 4;
        [Min(2f)] public float length = 22f;
        [Min(0.2f), Tooltip("Half width of the damaging band.")] public float halfWidth = 1f;
        public float rotationSpeed = 35f;
        [Min(0.2f)] public float duration = 4f;
        [Min(0.05f)] public float tick = 0.15f;
        public float damagePerTick = 6f;
        public DamageType damageType = DamageType.Arcane;
        public float height = 1.2f;
        [Min(0f)] public float windup = 1.2f;
        [Min(0f)] public float recovery = 0.8f;
        [Tooltip("Thin line during the windup.")] public GameObject previewVfx;
        public GameObject beamVfx;
    }
}
