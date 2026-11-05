using System.Collections;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>
    /// Channelled beam: damage ticks for a duration along the aim, through every enemy, stopped by the level.
    /// Ticks can proc items (procCoefficient). The caster is slowed while channelling (Channeling status).
    /// Server aim: activation pitch + the caster's current yaw (body rotation is replicated).
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Beam", fileName = "Skill_Beam")]
    public class BeamSkill : SkillDefinition
    {
        [Header("Beam")]
        [Min(1f)] public float range = 40f;
        [Min(0.05f)] public float beamRadius = 0.6f;
        [Min(0.1f)] public float duration = 3f;
        [Min(1f)] public float ticksPerSecond = 8f;
        [Min(0f)] public float damageCoefficientPerTick = 0.8f;
        public DamageType damageType = DamageType.Arcane;
        [Range(0f, 1f), Tooltip("Item proc chance multiplier of each tick.")] public float procCoefficient = 0.5f;
        [Tooltip("Level geometry stopping the beam (characters never do).")] public LayerMask obstacleMask = Physics.DefaultRaycastLayers;

        [Header("Feedback")]
        [Tooltip("Line VFX (VfxNetwork, in the GameCatalog effects) spawned each tick.")] public GameObject beamVfx;
        public GameObject impactVfx;

        private static readonly RaycastHit[] hits = new RaycastHit[64];
        private static readonly List<RaycastHit> sorted = new();

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            if (context.Avatar.Status != null)
                context.Avatar.Status.Apply(StatusType.Channeling, duration);
            context.Run(Channel(context, activation.Direction, context.Transform.eulerAngles.y));
        }

        private IEnumerator Channel(SkillContext context, Vector3 direction, float yawAtCast)
        {
            float interval = 1f / ticksPerSecond;
            var wait = new WaitForSeconds(interval);
            var alreadyHit = new HashSet<Health>();
            for (float elapsed = 0f; elapsed < duration; elapsed += interval)
            {
                if (!context.Avatar.IsStanding)
                    yield break;
                Vector3 aim = Quaternion.Euler(0f, context.Transform.eulerAngles.y - yawAtCast, 0f) * direction;
                Tick(context, context.Look.CameraPivot.position, aim.normalized, alreadyHit);
                yield return wait;
            }
        }

        private void Tick(SkillContext context, Vector3 origin, Vector3 direction, HashSet<Health> alreadyHit)
        {
            int count = Physics.SphereCastNonAlloc(origin, beamRadius, direction, hits, range, obstacleMask | context.HitMask, QueryTriggerInteraction.Ignore);
            sorted.Clear();
            for (int i = 0; i < count; i++)
                sorted.Add(hits[i]);
            sorted.Sort((a, b) => a.distance.CompareTo(b.distance));

            float end = range;
            alreadyHit.Clear();
            foreach (var hit in sorted)
            {
                if (context.IsSelf(hit.collider))
                    continue;
                if (CombatUtility.TryGetDamageable(hit.collider, out var target))
                {
                    if (target.Team == context.Team || !target.IsAlive || !alreadyHit.Add(target.Health))
                        continue;
                    Vector3 point = hit.distance > 0f ? hit.point : TargetFinder.Center(target.Health);
                    var info = context.CreateDamage(damageCoefficientPerTick, damageType, point, direction);
                    info.ProcCoefficient = procCoefficient;
                    target.ApplyDamage(info);
                    continue;
                }
                if (hit.distance <= 0f)
                    continue;
                end = hit.distance; // level geometry stops the beam
                break;
            }

            Vector3 endPoint = origin + direction * end;
            VfxNetwork.SpawnLine(beamVfx, new[] { origin + direction * 0.6f - Vector3.up * 0.25f, endPoint });
            VfxNetwork.Spawn(impactVfx, endPoint, Quaternion.identity, 0.6f);
        }
    }
}
