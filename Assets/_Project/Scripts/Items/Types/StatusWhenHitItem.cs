using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>When the owner is hit: grants them a status for a while (with a cooldown).</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Items/Status When Hit", fileName = "Item_StatusWhenHit")]
    public class StatusWhenHitItem : ItemDefinition
    {
        public StatusType status = StatusType.Rally;
        [Min(0.1f)] public float durationFirstStack = 2f;
        [Min(0f)] public float durationPerExtraStack = 1f;
        [Min(0f)] public float cooldown = 5f;
        public GameObject vfx;

        private readonly Dictionary<ulong, float> readyAt = new();

        public override void OnOwnerDamagedServer(PlayerAvatar owner, in DamageInfo info, int stacks)
        {
            if (info.IgnoreModifiers || owner.Status == null)
                return;
            ulong id = owner.NetworkObjectId;
            if (readyAt.TryGetValue(id, out float ready) && Time.time < ready)
                return;
            readyAt[id] = Time.time + cooldown;
            owner.Status.Apply(status, Stacked(durationFirstStack, durationPerExtraStack, stacks));
            VfxNetwork.Spawn(vfx, owner.transform.position, Quaternion.identity);
        }
    }
}
