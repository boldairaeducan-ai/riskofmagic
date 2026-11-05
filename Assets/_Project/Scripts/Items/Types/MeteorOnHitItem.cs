using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Enemies;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Larme de comète: chance that a hit calls a meteor down on the target.</summary>
    public class MeteorOnHitItem : ItemDefinition
    {
        [Range(0f, 1f)] public float chanceFirstStack = 0.08f;
        [Range(0f, 1f)] public float chancePerExtraStack = 0.04f;
        [Min(0f), Tooltip("Fraction of the owner's base damage.")] public float damageCoefficient = 5f;
        public ProjectileDefinition meteor;
        [Min(1f)] public float fallHeight = 25f;
        [Min(0.1f)] public float fallTime = 0.8f;

        public override void OnHitServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
        {
            if (meteor == null || Random.value > Mathf.Clamp01(Stacked(chanceFirstStack, chancePerExtraStack, stacks)) * proc.Damage.ProcCoefficient)
                return;
            Vector3 point = proc.Victim != null ? proc.Victim.transform.position : proc.Position;
            Vector3 origin = point + new Vector3(Random.Range(-4f, 4f), fallHeight, Random.Range(-4f, 4f));
            Vector3 path = point - origin;
            var info = ProcDamage(owner, OwnerBaseDamage(owner) * damageCoefficient, DamageType.Fire, point, path.normalized);
            ProjectileNetwork.FireServer(meteor, origin, path.normalized, 1f, info, owner.NetworkObject, ProjectileNetwork.NoClient, path.magnitude / fallTime);
        }
    }
}
