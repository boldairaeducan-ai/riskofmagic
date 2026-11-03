using System.Collections;
using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    /// <summary>
    /// Lobbed explosive aimed at where the target stands when the windup starts. The impact zone is marked on
    /// the ground (readable in first person), the shooter must stand still (EnemyBrain stops during attacks).
    /// The projectile's gravity defines the arc; its explosionRadius the area.
    /// </summary>
    public class EnemyArtilleryAttack : EnemyAttack
    {
        [SerializeField] private ProjectileDefinition projectile;
        [SerializeField] private float damage = 14f;
        [SerializeField] private DamageType damageType = DamageType.Fire;
        [SerializeField, Min(0f), Tooltip("Too close: the brain backs off instead.")] private float minRange = 5f;
        [SerializeField, Min(0f)] private float windup = 0.8f;
        [SerializeField, Min(0.2f)] private float flightTime = 1.1f;
        [SerializeField, Min(0f)] private float recovery = 0.6f;
        [SerializeField] private Vector3 muzzleOffset = new(0f, 1.8f, 0.3f);
        [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;
        [SerializeField, Tooltip("Ground marker, replicated via VfxNetwork, scaled by the explosion radius.")]
        private GameObject telegraphVfx;

        public override bool IsInRange(Transform target)
        {
            if (!base.IsInRange(target))
                return false;
            Vector3 offset = target.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude >= minRange * minRange;
        }

        protected override IEnumerator Perform(PlayerAvatar target)
        {
            if (projectile == null || target == null)
                yield break;

            Vector3 impact = GroundPoint(target.transform.position);
            float radius = Mathf.Max(1f, projectile.explosionRadius);
            VfxNetwork.Spawn(telegraphVfx, impact, Quaternion.identity, radius);

            yield return new WaitForSeconds(windup);
            if (!Actor.Health.IsAlive)
                yield break;

            Vector3 origin = transform.TransformPoint(muzzleOffset);
            float gravity = projectile.gravity;
            Vector3 velocity = (impact - origin) / flightTime + Vector3.up * (0.5f * gravity * flightTime);
            float speed = velocity.magnitude;
            var info = DamageInfo.Create(damage * Actor.DamageMultiplier, damageType, NetworkObject, Actor.Health.Team, impact, velocity / speed);
            ProjectileNetwork.FireServer(projectile, origin, velocity / speed, 1f, info, NetworkObject, ProjectileNetwork.NoClient, speed);

            yield return new WaitForSeconds(recovery);
        }

        private Vector3 GroundPoint(Vector3 position)
        {
            return Physics.Raycast(position + Vector3.up, Vector3.down, out var hit, 30f, groundMask, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform)
                ? hit.point
                : position;
        }
    }
}
