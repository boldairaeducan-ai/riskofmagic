using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Chance on hit to fire a projectile at the victim (damage stacks).</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Items/Projectile On Hit", fileName = "Item_ProjectileOnHit")]
    public class ProjectileOnHitItem : ItemDefinition
    {
        public ProjectileDefinition projectile;
        public DamageType damageType = DamageType.Arcane;
        [Range(0f, 1f)] public float chance = 0.1f;
        [Min(0f), Tooltip("Fraction of the triggering hit, per stack.")] public float damageFractionPerStack = 3f;

        public override void OnHitServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
        {
            if (projectile == null || Random.value > chance * proc.Damage.ProcCoefficient)
                return;

            Vector3 origin = owner.Look.CameraPivot.position + Vector3.up * 0.5f;
            Vector3 target = proc.Victim != null && proc.Victim.IsAlive ? TargetFinder.Center(proc.Victim) : proc.Position;
            Vector3 direction = target - origin;
            float amount = proc.Damage.Amount * damageFractionPerStack * stacks;
            ProjectileNetwork.FireServer(projectile, origin, direction, 1f, ProcDamage(owner, amount, damageType, target, direction.normalized), owner.NetworkObject);
        }
    }
}
