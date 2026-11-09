using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>Égide: the caster and allies in range take less damage for a while (Health shield).</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Aegis", fileName = "Skill_Aegis")]
    public class AegisSkill : SkillDefinition
    {
        [Min(1f)] public float radius = 10f;
        [Range(0f, 1f)] public float damageReduction = 0.5f;
        [Min(0.1f)] public float duration = 4f;
        public GameObject pulseVfx;

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            Vector3 center = context.Transform.position;
            VfxNetwork.Spawn(pulseVfx, center, Quaternion.identity, radius);
            foreach (var player in PlayerAvatar.All)
                if (player != null && player.IsStanding && (player.transform.position - center).sqrMagnitude <= radius * radius)
                    player.Health.AddShield(damageReduction, duration);
        }
    }
}
