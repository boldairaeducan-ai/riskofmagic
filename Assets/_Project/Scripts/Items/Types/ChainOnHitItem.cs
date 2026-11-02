using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Chance on hit to chain lightning to nearby enemies (more targets per stack).</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Items/Chain On Hit", fileName = "Item_ChainOnHit")]
    public class ChainOnHitItem : ItemDefinition
    {
        public DamageType damageType = DamageType.Lightning;
        [Range(0f, 1f)] public float chance = 0.25f;
        [Min(0f), Tooltip("Fraction of the triggering hit.")] public float damageFraction = 0.8f;
        [Min(1)] public int targetsFirstStack = 3;
        [Min(0)] public int targetsPerExtraStack = 2;
        [Min(0f)] public float bounceRadius = 15f;
        public GameObject boltVfx;

        private static readonly List<Health> hit = new();
        private static readonly List<Vector3> points = new();

        public override void OnHitServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
        {
            if (Random.value > chance * proc.Damage.ProcCoefficient)
                return;

            int targets = Mathf.RoundToInt(Stacked(targetsFirstStack, targetsPerExtraStack, stacks));
            float amount = proc.Damage.Amount * damageFraction;
            hit.Clear();
            points.Clear();
            if (proc.Victim != null)
                hit.Add(proc.Victim);
            Vector3 from = proc.Position;
            points.Add(from);

            for (int i = 0; i < targets; i++)
            {
                var next = TargetFinder.FindNearestHostile(from, bounceRadius, owner.Health.Team, hit);
                if (next == null)
                    break;
                hit.Add(next);
                Vector3 to = TargetFinder.Center(next);
                next.ApplyDamage(ProcDamage(owner, amount, damageType, to, (to - from).normalized));
                points.Add(to);
                from = to;
            }

            if (points.Count > 1)
                VfxNetwork.SpawnLine(boltVfx, points.ToArray());
        }
    }
}
