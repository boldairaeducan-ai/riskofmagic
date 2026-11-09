using System.Collections;
using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>Marque de mort: the aimed enemy takes more damage (Marked status); its death resets the cooldown.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Mark", fileName = "Skill_Mark")]
    public class MarkSkill : SkillDefinition
    {
        [Min(1f)] public float targetRange = 40f;
        [Min(0.5f)] public float aimTolerance = 3f;
        [Min(0.1f)] public float duration = 6f;
        public bool resetOnKill = true;
        public GameObject markVfx;

        public override float MaxRange => targetRange;

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            var target = SkillUtility.FindAimedHostile(context, activation.Target, targetRange, aimTolerance);
            if (target == null || !target.TryGetComponent(out StatusEffects status))
                return;
            status.Apply(StatusType.Marked, duration);
            VfxNetwork.Spawn(markVfx, TargetFinder.Center(target), Quaternion.identity, 1.5f);
            if (resetOnKill)
                context.Run(WatchKill(context, target));
        }

        private IEnumerator WatchKill(SkillContext context, Health target)
        {
            float end = Time.time + duration;
            while (Time.time < end && target != null && target.IsAlive)
                yield return null;
            if (target != null && !target.IsAlive && Time.time <= end + 0.1f)
                context.Controller.RefillSkill(this);
        }
    }
}
