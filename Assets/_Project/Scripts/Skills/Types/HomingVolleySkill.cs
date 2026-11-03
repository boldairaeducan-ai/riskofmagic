using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>
    /// Charged volley of homing projectiles. While charging, each new missile locks onto the enemy closest to the
    /// crosshair at that moment (sweep the crosshair to spread them, keep it on one enemy to stack them).
    /// The owner sends its locks; the server validates them and fills unlocked missiles itself.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Homing Volley", fileName = "Skill_HomingVolley")]
    public class HomingVolleySkill : SkillDefinition
    {
        [Header("Projectiles")]
        public ProjectileDefinition projectile;
        public DamageType damageType = DamageType.Arcane;
        [Min(0f), Tooltip("Per missile.")] public float damageCoefficient = 1.1f;
        [Min(1)] public int maxTargets = 6;
        [Min(0f), Tooltip("Launch fan (degrees) before homing kicks in.")] public float launchSpread = 30f;
        public Vector3 muzzleOffset = new(0.25f, -0.2f, 0.5f);

        [Header("Targeting")]
        [Min(0f)] public float targetRange = 45f;
        [Range(1f, 90f), Tooltip("Half-angle of the targeting cone around the crosshair.")] public float targetConeAngle = 25f;
        public LayerMask sightMask = Physics.DefaultRaycastLayers;

        private static readonly List<Health> candidates = new();
        private static readonly List<Health> resolved = new();

        public override float MaxRange => targetRange;

        public int GetMissileCount(float charge) => 1 + Mathf.RoundToInt(Mathf.Clamp01(charge) * (maxTargets - 1));

        /// <summary>Enemy currently closest to the crosshair (owner or server), or null.</summary>
        public Health FindBestTarget(SkillContext context)
        {
            Ray aim = context.Look.AimRay;
            FindTargets(context, aim.origin, aim.direction);
            return candidates.Count > 0 ? candidates[0] : null;
        }

        // ---------------------------------------------------------------- Owner

        public override void OnChargeStarted(SkillContext context, SkillSlotState state) => state.LockedTargets.Clear();

        public override void OnChargeTick(SkillContext context, SkillSlotState state, float charge)
        {
            int wanted = GetMissileCount(charge);
            if (state.LockedTargets.Count >= wanted)
                return;
            var best = FindBestTarget(context);
            if (best != null)
                state.LockedTargets.Add(best.NetworkObjectId); // one new lock per frame at most
        }

        public override void PrepareActivation(SkillContext context, SkillSlotState state, ref SkillActivation activation)
        {
            activation.Origin = context.Look.CameraPivot.TransformPoint(muzzleOffset);
            int count = GetMissileCount(activation.Charge);
            if (state.LockedTargets.Count > count)
                state.LockedTargets.RemoveRange(count, state.LockedTargets.Count - count);
            activation.TargetIds = state.LockedTargets.ToArray();
            state.LockedTargets.Clear();
        }

        // ---------------------------------------------------------------- Server

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            int count = GetMissileCount(activation.Charge);
            ResolveLocks(context, activation.TargetIds, count);
            if (resolved.Count == 0)
            {
                // Nothing locked (or locks invalid): fall back to the enemies around the crosshair.
                FindTargets(context, context.Look.CameraPivot.position, activation.Direction);
                resolved.AddRange(candidates);
            }

            for (int i = 0; i < count; i++)
            {
                var target = resolved.Count > 0 ? resolved[i % resolved.Count] : null;
                Vector3 direction = LaunchDirection(activation.Direction, i, count);
                ProjectileNetwork.FireServer(projectile, activation.Origin, direction, 1f, context.CreateDamage(damageCoefficient, damageType),
                    context.NetworkObject, ProjectileNetwork.NoClient, -1f, target != null ? target.NetworkObject : null);
            }
        }

        private void ResolveLocks(SkillContext context, ulong[] ids, int count)
        {
            resolved.Clear();
            if (ids == null)
                return;
            var spawned = NetworkManager.Singleton.SpawnManager.SpawnedObjects;
            float maxRange = targetRange * 1.15f; // small latency slack, locks beyond range are refused
            for (int i = 0; i < ids.Length && i < count; i++)
            {
                if (!spawned.TryGetValue(ids[i], out var networkObject) || !networkObject.TryGetComponent(out Health health))
                    continue;
                if (!health.IsAlive || health.Team == context.Team)
                    continue;
                if ((health.transform.position - context.Transform.position).sqrMagnitude > maxRange * maxRange)
                    continue;
                resolved.Add(health);
            }
        }

        // ---------------------------------------------------------------- Shared

        private void FindTargets(SkillContext context, Vector3 eye, Vector3 aim)
        {
            TargetFinder.FindHostiles(eye, targetRange, context.Team, candidates, context.HitMask);
            candidates.RemoveAll(health =>
            {
                Vector3 toTarget = TargetFinder.Center(health) - eye;
                if (Vector3.Angle(aim, toTarget) > targetConeAngle)
                    return true;
                return Physics.Linecast(eye, TargetFinder.Center(health), out var hit, sightMask, QueryTriggerInteraction.Ignore)
                       && !hit.collider.transform.IsChildOf(health.transform) && !context.IsSelf(hit.collider);
            });
            candidates.Sort((a, b) => Vector3.Angle(aim, TargetFinder.Center(a) - eye).CompareTo(Vector3.Angle(aim, TargetFinder.Center(b) - eye)));
        }

        private Vector3 LaunchDirection(Vector3 aim, int index, int count)
        {
            if (count <= 1)
                return aim;
            Vector3 right = Vector3.Cross(Vector3.up, aim).normalized;
            float yaw = Mathf.Lerp(-launchSpread, launchSpread, index / (float)(count - 1));
            return Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(-12f, right) * aim;
        }
    }
}
