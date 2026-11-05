using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Enemies;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Anneau de givre: chance to freeze the enemy hit and deal frost damage.</summary>
    public class FreezeOnHitItem : ItemDefinition
    {
        [Range(0f, 1f)] public float chanceFirstStack = 0.1f;
        [Range(0f, 1f)] public float chancePerExtraStack = 0.05f;
        [Min(0f)] public float freezeDuration = 1.5f;
        [Min(0f), Tooltip("Fraction of the owner's base damage.")] public float damageCoefficient = 2.5f;
        public GameObject vfx;

        public override void OnHitServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
        {
            if (proc.Victim == null || !proc.Victim.IsAlive)
                return;
            if (Random.value > Mathf.Clamp01(Stacked(chanceFirstStack, chancePerExtraStack, stacks)) * proc.Damage.ProcCoefficient)
                return;
            if (proc.Victim.TryGetComponent(out EnemyActor enemy))
                enemy.Freeze(freezeDuration);
            proc.Victim.ApplyDamage(ProcDamage(owner, OwnerBaseDamage(owner) * damageCoefficient, DamageType.Frost, proc.Position));
            VfxNetwork.Spawn(vfx, proc.Position, Quaternion.identity, 0.4f);
        }
    }
}
