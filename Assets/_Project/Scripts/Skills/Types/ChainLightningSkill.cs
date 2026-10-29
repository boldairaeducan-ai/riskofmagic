using System;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>Hits the aimed enemy then bounces to the nearest ones. The chain is recomputed on each peer for visuals.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Chain Lightning", fileName = "Skill_ChainLightning")]
    public class ChainLightningSkill : SkillDefinition
    {
        [Header("Targeting")]
        [Min(0f)] public float range = 40f;
        [Min(0f)] public float aimRadius = 0.8f;
        [Min(0)] public int maxBounces = 3;
        [Min(0f)] public float bounceRadius = 10f;

        [Header("Damage")]
        public DamageType damageType = DamageType.Lightning;
        [Min(0f)] public float damageCoefficient = 4f;
        [Range(0f, 1f)] public float bounceFalloff = 0.8f;

        [Header("Feedback")]
        public GameObject boltVfx;

        private static readonly RaycastHit[] castHits = new RaycastHit[16];
        private static readonly Collider[] overlaps = new Collider[64];
        private static readonly List<Health> chain = new();
        private static readonly List<Vector3> points = new();

        public override bool CanActivate(SkillContext context, SkillSlotState state)
        {
            Ray aim = context.Look.AimRay;
            chain.Clear();
            return FindFirst(context, aim.origin, aim.direction) != null;
        }

        public override void OnOwnerExecute(SkillContext context, in SkillActivation activation)
        {
            base.OnOwnerExecute(context, activation);
            DrawChain(context, activation);
        }

        public override void OnRemoteExecute(SkillContext context, in SkillActivation activation)
        {
            base.OnRemoteExecute(context, activation);
            DrawChain(context, activation);
        }

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            BuildChain(context, activation.Origin, activation.Direction);
            float coefficient = damageCoefficient;
            foreach (var target in chain)
            {
                target.ApplyDamage(context.CreateDamage(coefficient, damageType, Center(target), activation.Direction));
                coefficient *= bounceFalloff;
            }
        }

        private void DrawChain(SkillContext context, in SkillActivation activation)
        {
            if (boltVfx == null)
                return;
            BuildChain(context, activation.Origin, activation.Direction);
            points.Clear();
            points.Add(activation.Origin + activation.Direction * 0.5f);
            foreach (var target in chain)
                points.Add(Center(target));
            if (points.Count > 1)
                VfxUtility.SpawnLine(boltVfx, points);
        }

        private void BuildChain(SkillContext context, Vector3 origin, Vector3 direction)
        {
            chain.Clear();
            var current = FindFirst(context, origin, direction);
            while (current != null)
            {
                chain.Add(current);
                if (chain.Count > maxBounces)
                    break;
                current = FindNearest(context, Center(current));
            }
        }

        private Health FindFirst(SkillContext context, Vector3 origin, Vector3 direction)
        {
            int count = Physics.SphereCastNonAlloc(origin, aimRadius, direction, castHits, range, context.HitMask, QueryTriggerInteraction.Ignore);
            Array.Sort(castHits, 0, count, RaycastHitDistanceComparer.Instance);
            for (int i = 0; i < count; i++)
            {
                if (context.IsSelf(castHits[i].collider))
                    continue;
                if (!CombatUtility.TryGetDamageable(castHits[i].collider, out var target))
                    return null; // blocked by the world
                if (IsValid(context, target.Health))
                    return target.Health;
            }
            return null;
        }

        private Health FindNearest(SkillContext context, Vector3 from)
        {
            int count = Physics.OverlapSphereNonAlloc(from, bounceRadius, overlaps, context.HitMask, QueryTriggerInteraction.Ignore);
            Health best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!CombatUtility.TryGetDamageable(overlaps[i], out var target) || !IsValid(context, target.Health))
                    continue;
                float sqr = (Center(target.Health) - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = target.Health;
                }
            }
            return best;
        }

        private static bool IsValid(SkillContext context, Health health)
            => health != null && health.IsAlive && health.Team != context.Team && !chain.Contains(health);

        private static Vector3 Center(Health health) => health.transform.position + Vector3.up;
    }
}
