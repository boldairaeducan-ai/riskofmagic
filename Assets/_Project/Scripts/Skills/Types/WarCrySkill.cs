using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>Cri de guerre: allies in range (caster included) get the Rally buff and a flat heal.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/War Cry", fileName = "Skill_WarCry")]
    public class WarCrySkill : SkillDefinition
    {
        [Min(1f)] public float radius = 15f;
        [Min(0.1f), Tooltip("Rally status duration (+40% attack/move speed, StatusEffects.rallyBonus).")] public float buffDuration = 6f;
        [Min(0f)] public float heal = 20f;
        [Tooltip("Spawned on the caster (VfxNetwork).")] public GameObject pulseVfx;

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            Vector3 center = context.Transform.position;
            VfxNetwork.Spawn(pulseVfx, center, Quaternion.identity, radius);
            foreach (var player in PlayerAvatar.All)
            {
                if (player == null || !player.IsStanding || (player.transform.position - center).sqrMagnitude > radius * radius)
                    continue;
                if (player.Status != null)
                    player.Status.Apply(StatusType.Rally, buffDuration);
                player.Health.Heal(heal);
            }
        }
    }
}
