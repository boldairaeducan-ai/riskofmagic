using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Equipment
{
    /// <summary>Pays a share of max health to curse every enemy in a cone (Tome sombre).</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Equipment/Curse Cone", fileName = "Equipment_CurseCone")]
    public class CurseConeEquipment : EquipmentDefinition
    {
        [Range(0f, 1f), Tooltip("Fraction of max health paid (can kill).")] public float healthCost = 0.25f;
        [Min(0f)] public float range = 20f;
        [Range(1f, 90f)] public float coneAngle = 35f;
        [Min(0.1f)] public float curseDuration = 8f;
        public GameObject vfx;

        private static readonly List<Health> targets = new();

        public override bool Activate(PlayerEquipment user, Vector3 aimOrigin, Vector3 aimDirection)
        {
            var health = user.Avatar.Health;
            Vector3 flatAim = new Vector3(aimDirection.x, 0f, aimDirection.z).normalized;

            TargetFinder.FindHostiles(aimOrigin, range, health.Team, targets);
            foreach (var target in targets)
            {
                Vector3 toTarget = target.transform.position - user.transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f && Vector3.Angle(flatAim, toTarget) > coneAngle)
                    continue;
                if (target.TryGetComponent(out StatusEffects status))
                    status.Apply(StatusType.Curse, curseDuration);
            }

            VfxNetwork.Spawn(vfx, user.transform.position, Quaternion.LookRotation(flatAim.sqrMagnitude > 0f ? flatAim : user.transform.forward), range);
            health.PayHealth(health.Max * healthCost);
            return true;
        }
    }
}
