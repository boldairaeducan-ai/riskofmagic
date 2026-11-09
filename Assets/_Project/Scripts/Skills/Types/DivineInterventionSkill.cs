using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>
    /// Intervention divine: revives every downed ally in range with part of their health and protects them briefly.
    /// With nobody to revive (solo), heals the caster instead.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Divine Intervention", fileName = "Skill_DivineIntervention")]
    public class DivineInterventionSkill : SkillDefinition
    {
        [Min(1f)] public float radius = 30f;
        [Range(0f, 1f)] public float reviveHealthFraction = 0.5f;
        [Range(0f, 1f)] public float protectionReduction = 0.9f;
        [Min(0f)] public float protectionDuration = 3f;
        [Range(0f, 1f), Tooltip("Self heal when nobody was revived.")] public float selfHealFraction = 0.5f;
        public GameObject reviveVfx;

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            Vector3 center = context.Transform.position;
            int revived = 0;
            foreach (var player in PlayerAvatar.All)
            {
                if (player == null || player == context.Avatar || !player.IsAlive || player.Downed == null || !player.Downed.IsDowned)
                    continue;
                if ((player.transform.position - center).sqrMagnitude > radius * radius)
                    continue;
                player.Downed.ReviveNow(reviveHealthFraction);
                player.Health.AddShield(protectionReduction, protectionDuration);
                VfxNetwork.Spawn(reviveVfx, player.transform.position, Quaternion.identity, 2f);
                revived++;
            }
            if (revived > 0)
                return;
            context.Avatar.Health.Heal(context.Avatar.Health.Max * selfHealFraction);
            VfxNetwork.Spawn(reviveVfx, center, Quaternion.identity, 2f);
        }
    }
}
