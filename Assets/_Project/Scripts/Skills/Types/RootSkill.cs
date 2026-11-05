using System.Collections;
using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>Enracinement: the caster cannot move for a while, regenerates and poisons around itself.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Root", fileName = "Skill_Root")]
    public class RootSkill : SkillDefinition
    {
        [Min(0.1f)] public float duration = 3f;
        [Range(0f, 1f), Tooltip("Fraction of max health healed per second.")] public float healPerSecond = 0.1f;
        [Min(0.05f)] public float tickInterval = 0.5f;
        [Header("Poison cloud")]
        [Min(0.1f)] public float radius = 5f;
        [Min(0f)] public float damageCoefficientPerTick = 0.3f;
        public DamageType damageType = DamageType.Poison;
        public DotEffect dot;
        [Tooltip("Spawned each tick (VfxNetwork).")] public GameObject tickVfx;

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            if (context.Avatar.Status != null)
                context.Avatar.Status.Apply(StatusType.Rooted, duration);
            context.Run(Ticks(context));
        }

        private IEnumerator Ticks(SkillContext context)
        {
            var wait = new WaitForSeconds(tickInterval);
            for (float elapsed = 0f; elapsed < duration; elapsed += tickInterval)
            {
                if (!context.Avatar.IsStanding)
                    yield break;
                var health = context.Avatar.Health;
                health.Heal(health.Max * healPerSecond * tickInterval);
                Vector3 center = context.Transform.position;
                context.DamageArea(center + Vector3.up * 0.5f, radius, damageCoefficientPerTick, damageType, null, dot);
                VfxNetwork.Spawn(tickVfx, center, Quaternion.identity, radius * 0.5f);
                yield return wait;
            }
        }
    }
}
