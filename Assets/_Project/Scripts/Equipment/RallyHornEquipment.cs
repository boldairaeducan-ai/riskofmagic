using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Equipment
{
    /// <summary>Rally status (attack/move speed) on the user and nearby allies.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Equipment/Rally Horn", fileName = "Equipment_RallyHorn")]
    public class RallyHornEquipment : EquipmentDefinition
    {
        [Min(0f)] public float radius = 25f;
        [Min(0.1f)] public float duration = 8f;
        public GameObject vfx;

        public override bool Activate(PlayerEquipment user, Vector3 aimOrigin, Vector3 aimDirection)
        {
            Vector3 center = user.transform.position;
            foreach (var player in PlayerAvatar.All)
                if (player.IsAlive && player.Status != null && (player.transform.position - center).sqrMagnitude <= radius * radius)
                    player.Status.Apply(StatusType.Rally, duration);
            VfxNetwork.Spawn(vfx, center, Quaternion.identity, radius);
            return true;
        }
    }
}
