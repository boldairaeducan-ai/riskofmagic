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
    /// The tick rate scales with the caster's attack speed bonus (relative to the character's base attack speed), and
    /// tick damage ramps up the longer the beam is held.
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
        [Range(0f, 1f), Tooltip("Item proc chance multiplier of each tick.")] public float procCoefficient = 0.6f;
        [Tooltip("Ticks per second x (attack speed / character base attack speed): attack speed items add ticks.")]
        public bool tickRateScalesWithAttackSpeed = true;
        [Min(0f), Tooltip("Tick damage +X per second of channel (0.25 = +75% on the last tick of a 3 s beam).")] public float damageRampPerSecond = 0.25f;
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
            var alreadyHit = new HashSet<Health>();
            for (float elapsed = 0f; elapsed < duration;)
            {
                if (!context.Avatar.IsStanding)
                    yield break;
                Vector3 aim = Quaternion.Euler(0f, context.Transform.eulerAngles.y - yawAtCast, 0f) * direction;
                Tick(context, context.Look.CameraPivot.position, aim.normalized, alreadyHit, 1f + damageRampPerSecond * elapsed);
                float interval = 1f / Mathf.Max(0.1f, ticksPerSecond * AttackSpeedFactor(context));
                yield return new WaitForSeconds(interval);
                elapsed += interval;
            }
        }

        /// <summary>1 at the character's base attack speed, more with attack speed items and buffs.</summary>
        private float AttackSpeedFactor(SkillContext context)
        {
            if (!tickRateScalesWithAttackSpeed)
                return 1f;
            float baseSpeed = context.Avatar.Character != null ? context.Avatar.Character.attackSpeed : 1f;
            return Mathf.Max(0.25f, context.Stats.AttackSpeed / Mathf.Max(0.01f, baseSpeed));
        }

        private void Tick(SkillContext context, Vector3 origin, Vector3 direction, HashSet<Health> alreadyHit, float ramp)
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
                    var info = context.CreateDamage(damageCoefficientPerTick * ramp, damageType, point, direction);
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
