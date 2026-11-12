using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Run;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    /// <summary>
    /// Tuning of an enemy attack, shared by every enemy that references it (Configs/Enemies/Attacks).
    /// Created once by the builder, never overwritten: edit freely, changes apply live in Play mode.
    /// Damage and attack rate are further multiplied by GameBalance.enemies (regular vs boss).
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Enemies/Attack Data (base)", fileName = "AttackData")]
    public class EnemyAttackData : ScriptableObject
    {
        [Min(0f)] public float range = 2f;
        [Min(0f)] public float cooldown = 1.5f;
        [Min(0f), Tooltip("Max vertical gap with the target (prevents hitting through platforms).")] public float maxHeightDifference = 2.5f;
        [Min(0f), Tooltip("Multi-attack enemies: relative chance to pick this attack among the ready ones.")] public float selectionWeight = 1f;
        [Tooltip("Animator trigger at the attack start (every peer), with the AttackIndex int parameter.")] public string animatorTrigger = "Attack";
    }
}
