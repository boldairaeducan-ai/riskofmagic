using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Equipment
{
    /// <summary>Curses every enemy in a cone in front of the user (Crâne hurlant).</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Equipment/Curse Cone", fileName = "Equipment_CurseCone")]
    public class CurseConeEquipment : EquipmentDefinition
    {
        [Min(0f)] public float range = 20f;
        [Range(1f, 90f), Tooltip("Half-angle of the cone.")] public float coneAngle = 35f;
        [Min(0.1f)] public float curseDuration = 8f;
        public GameObject vfx;

        private static readonly List<Health> targets = new();

        public override bool Activate(PlayerEquipment user, Vector3 aimOrigin, Vector3 aimDirection)
        {
            var health = user.Avatar.Health;
            Vector3 flatAim = new Vector3(aimDirection.x, 0f, aimDirection.z).normalized;
            if (flatAim.sqrMagnitude < 0.001f)
                flatAim = user.transform.forward;

            TargetFinder.FindHostiles(aimOrigin, range, health.Team, targets);
            foreach (var target in targets)
            {
                Vector3 toTarget = target.transform.position - user.transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f && Vector3.Angle(flatAim, toTarget) > coneAngle)
                    continue;
                var status = SealSystem.FindStatus(target);
                if (status != null)
                    status.Apply(StatusType.Curse, curseDuration);
            }

            VfxNetwork.Spawn(vfx, user.transform.position, Quaternion.LookRotation(flatAim), range);
            return true;
        }
    }
}
