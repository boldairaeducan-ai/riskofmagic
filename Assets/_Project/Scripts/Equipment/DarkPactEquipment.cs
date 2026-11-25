using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Equipment
{
    /// <summary>
    /// Tome sombre (Pacte sombre): pays a share of max health (never lethal) for a DarkPact status: more damage, and hits
    /// heal a share of the damage dealt (StatusEffects).
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Equipment/Dark Pact", fileName = "Equipment_DarkPact")]
    public class DarkPactEquipment : EquipmentDefinition
    {
        [Range(0f, 1f), Tooltip("Fraction of max health paid. Never lethal: the user keeps at least 1 HP.")] public float healthCost = 0.1f;
        [Min(0.1f)] public float duration = 8f;
        [Tooltip("Effect on the user (replicated).")] public GameObject vfx;

        public override bool Activate(PlayerEquipment user, Vector3 aimOrigin, Vector3 aimDirection)
        {
            var avatar = user.Avatar;
            if (avatar.Status == null)
                return false;
            var health = avatar.Health;
            float cost = Mathf.Min(health.Max * healthCost, health.Current - 1f);
            if (cost > 0f)
                health.PayHealth(cost);
            avatar.Status.Apply(StatusType.DarkPact, duration);
            VfxNetwork.Spawn(vfx, user.transform.position + Vector3.up, Quaternion.identity, 1.5f);
            return true;
        }
    }
}
