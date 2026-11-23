using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Enemies;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Bannière de guerre: allies in range (owner included) deal more damage (Banner status, refreshed).</summary>
    public class BannerItem : ItemDefinition
    {
        [Min(1f)] public float radiusFirstStack = 12f;
        [Min(0f)] public float radiusPerExtraStack = 2f;
        [Tooltip("Damage bonus: GameBalance bannerDamageBonus (+ bannerDamageBonusPerExtraStack per extra stack). Several banners: the biggest wins.")]
        [Min(1)] public int maxStacks = 50;

        public override void OnServerTick(PlayerAvatar owner, int stacks, float interval)
        {
            float radius = Stacked(radiusFirstStack, radiusPerExtraStack, stacks);
            Vector3 center = owner.transform.position;
            foreach (var player in PlayerAvatar.All)
            {
                if (player == null || !player.IsStanding || player.Status == null)
                    continue;
                if ((player.transform.position - center).sqrMagnitude <= radius * radius)
                {
                    int wanted = Mathf.Clamp(Mathf.Max(stacks, player.Status.GetStacks(StatusType.Banner)), 1, maxStacks);
                    player.Status.Apply(StatusType.Banner, interval * 2.5f, wanted, wanted);
                }
            }
        }
    }
}
