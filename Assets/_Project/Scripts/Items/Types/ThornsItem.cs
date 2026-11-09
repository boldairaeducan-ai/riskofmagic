using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>When the owner is hit: deals a multiple of their base damage back to the attacker (with a cooldown).</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Items/Thorns", fileName = "Item_Thorns")]
    public class ThornsItem : ItemDefinition
    {
        public DamageType damageType = DamageType.Physical;
        [Min(0f), Tooltip("Multiple of the owner's base damage.")] public float damageFirstStack = 2f;
        [Min(0f)] public float damagePerExtraStack = 1f;
        [Min(0f)] public float cooldownFirstStack = 15f;
        [Tooltip("Negative: shorter per extra stack.")] public float cooldownPerExtraStack = -2f;
        [Min(0.5f)] public float minCooldown = 3f;
        [Min(0f)] public float maxRange = 15f;
        public GameObject vfx;

        private readonly Dictionary<ulong, float> readyAt = new();

        public override void OnOwnerDamagedServer(PlayerAvatar owner, in DamageInfo info, int stacks)
        {
            if (info.IgnoreModifiers || !info.TryGetSource(out var source) || !source.TryGetComponent(out Health attacker)
                || !attacker.IsAlive || attacker.Team == owner.Health.Team)
                return;
            if ((attacker.transform.position - owner.transform.position).sqrMagnitude > maxRange * maxRange)
                return;
            ulong id = owner.NetworkObjectId;
            if (readyAt.TryGetValue(id, out float ready) && Time.time < ready)
                return;
            readyAt[id] = Time.time + Mathf.Max(minCooldown, Stacked(cooldownFirstStack, cooldownPerExtraStack, stacks));

            float amount = owner.Stats.BaseDamage * Stacked(damageFirstStack, damagePerExtraStack, stacks);
            Vector3 point = TargetFinder.Center(attacker);
            attacker.ApplyDamage(ProcDamage(owner, amount, damageType, point, (point - owner.transform.position).normalized));
            VfxNetwork.Spawn(vfx, point, Quaternion.identity);
        }
    }
}
