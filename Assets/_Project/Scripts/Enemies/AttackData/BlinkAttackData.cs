using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Run;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    [CreateAssetMenu(menuName = "Risk of Magic/Enemies/BlinkAttackData", fileName = "BlinkAttackData")]
    public class BlinkAttackData : EnemyAttackData
    {
        public Vector2 distanceFromTarget = new(7f, 12f);
        [Min(0f)] public float windup = 0.35f;
        [Min(0f)] public float recovery = 0.3f;
        public GameObject blinkVfx;
    }
}
