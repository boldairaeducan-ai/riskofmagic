using System.Collections;
using System.Collections.Generic;
using RiskOfMagic.Characters;
using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Core;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Skills
{
    /// <summary>Per-player facade given to skill definitions.</summary>
    public sealed class SkillContext
    {
        public SkillContext(SkillController controller, PlayerAvatar avatar, CharacterStats stats)
        {
            Controller = controller;
            Avatar = avatar;
            Stats = stats;
        }

        public SkillController Controller { get; }
        public PlayerAvatar Avatar { get; }
        public CharacterStats Stats { get; }
        public PlayerMotor Motor => Avatar.Motor;
        public PlayerLook Look => Avatar.Look;
        public PlayerInputHandler Input => Avatar.Input;
        public Transform Transform => Avatar.transform;
        public NetworkObject NetworkObject => Controller.NetworkObject;
        public Team Team => Avatar.Health.Team;
        public bool IsServer => Controller.IsServer;
        public bool IsOwner => Controller.IsOwner;
        public LayerMask HitMask => Controller.HitMask;
        public Vector3 BodyCenter => Transform.position + Vector3.up;

        /// <summary>Server side. coefficient 2.5 = 250% of base damage.</summary>
        public DamageInfo CreateDamage(float coefficient, DamageType type, Vector3 point = default, Vector3 direction = default, bool forceCritical = false)
        {
            float amount = Stats.RollDamage(coefficient, out bool isCritical, forceCritical);
            var info = DamageInfo.Create(amount, type, NetworkObject, Team, point, direction);
            info.IsCritical = isCritical;
            return info;
        }

        /// <summary>Server side.</summary>
        public int DamageArea(Vector3 center, float radius, float coefficient, DamageType type, HashSet<Health> alreadyHit = null, DotEffect dot = null)
            => AreaDamage.Apply(center, radius, CreateDamage(coefficient, type, center), HitMask, alreadyHit, dot);

        /// <summary>Server side: authoritative projectile, replicated to other clients.</summary>
        public void FireProjectile(ProjectileDefinition projectile, Vector3 origin, Vector3 direction, float coefficient, DamageType type, float scale = 1f)
            => ProjectileNetwork.FireServer(projectile, origin, direction, scale, CreateDamage(coefficient, type), NetworkObject, Controller.OwnerClientId);

        /// <summary>Owner side: predicted visual copy (skip when hosting, the server copy is visible).</summary>
        public void PredictProjectile(ProjectileDefinition projectile, Vector3 origin, Vector3 direction, float scale = 1f)
        {
            if (!IsServer)
                ProjectileNetwork.LaunchVisual(projectile, origin, direction, scale, Team, Transform);
        }

        public Coroutine Run(IEnumerator routine) => Controller.StartCoroutine(routine);

        public Vector3 ProjectToGround(Vector3 point, float maxDistance = 50f)
        {
            return Physics.Raycast(point + Vector3.up * 2f, Vector3.down, out var hit, maxDistance, Controller.AimMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : point;
        }

        public bool IsSelf(Collider collider) => collider.transform.IsChildOf(Transform);

        /// <summary>True when the attacker comes from behind the target (target facing roughly the same way).</summary>
        public static bool IsBehind(Transform target, Vector3 attackDirection)
        {
            Vector3 facing = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            Vector3 direction = Vector3.ProjectOnPlane(attackDirection, Vector3.up);
            return facing.sqrMagnitude > 0.001f && direction.sqrMagnitude > 0.001f && Vector3.Dot(facing.normalized, direction.normalized) > 0.3f;
        }

        /// <summary>Server: heals every standing player within radius of center.</summary>
        public void HealAllies(Vector3 center, float radius, float flat, float maxHealthFraction = 0f)
        {
            foreach (var player in PlayerAvatar.All)
            {
                if (player == null || !player.IsStanding || (player.transform.position - center).sqrMagnitude > radius * radius)
                    continue;
                player.Health.Heal(flat + player.Health.Max * maxHealthFraction);
            }
        }
    }
}
