using System.Collections;
using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Equipment
{
    /// <summary>Calls lightning several times on the aimed spot (Pierre de foudre).</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Equipment/Lightning Strike", fileName = "Equipment_LightningStrike")]
    public class LightningStrikeEquipment : EquipmentDefinition
    {
        [Min(0f)] public float range = 60f;
        [Min(1)] public int strikes = 3;
        [Min(0f)] public float strikeInterval = 0.35f;
        [Min(0.1f)] public float radius = 4f;
        [Min(0f), Tooltip("Per strike, x owner base damage.")] public float damageCoefficient = 5f;
        public DamageType damageType = DamageType.Lightning;
        public LayerMask groundMask = Physics.DefaultRaycastLayers;
        public GameObject boltVfx;
        public GameObject impactVfx;

        public override bool Activate(PlayerEquipment user, Vector3 aimOrigin, Vector3 aimDirection)
        {
            Vector3 point = aimOrigin + aimDirection * range;
            if (Physics.Raycast(aimOrigin, aimDirection, out var hit, range, groundMask, QueryTriggerInteraction.Ignore))
                point = hit.point;
            else if (Physics.Raycast(point + Vector3.up * 5f, Vector3.down, out var ground, 50f, groundMask, QueryTriggerInteraction.Ignore))
                point = ground.point;

            user.Run(Strikes(user, point));
            return true;
        }

        private IEnumerator Strikes(PlayerEquipment user, Vector3 point)
        {
            var avatar = user.Avatar;
            for (int i = 0; i < strikes; i++)
            {
                float amount = avatar.Stats.RollDamage(damageCoefficient, out bool critical);
                var info = DamageInfo.Create(amount, damageType, avatar.NetworkObject, avatar.Health.Team, point, Vector3.down);
                info.IsCritical = critical;
                AreaDamage.Apply(point + Vector3.up * 0.5f, radius, info);
                VfxNetwork.SpawnLine(boltVfx, new[] { point + Vector3.up * 25f, point });
                VfxNetwork.Spawn(impactVfx, point, Quaternion.identity, radius);
                if (i < strikes - 1)
                    yield return new WaitForSeconds(strikeInterval);
            }
        }
    }
}
