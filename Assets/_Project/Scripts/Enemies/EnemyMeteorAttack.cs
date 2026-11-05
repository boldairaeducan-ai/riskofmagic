using System.Collections;
using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    /// <summary>
    /// Meteor shower: a giant meteor on every player in range plus extra ones around them, each marked on the
    /// ground, falling from the sky to land exactly after `fallTime`. Damage and radius come from the projectile's
    /// explosion (ProjectileDefinition.explosionRadius).
    /// </summary>
    public class EnemyMeteorAttack : EnemyAttack
    {
        [SerializeField] private ProjectileDefinition meteor;
        [SerializeField] private float damage = 45f;
        [SerializeField] private DamageType damageType = DamageType.Fire;
        [SerializeField, Min(0f)] private float windup = 0.6f;
        [SerializeField, Min(0.3f), Tooltip("Time between the ground marker and the impact.")] private float fallTime = 1.6f;
        [SerializeField, Min(0)] private int extraMeteorsPerPlayer = 2;
        [SerializeField] private Vector2 extraOffset = new(5f, 11f);
        [SerializeField, Min(5f)] private float fallHeight = 35f;
        [SerializeField, Min(0f)] private float recovery = 0.6f;
        [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;
        [SerializeField] private GameObject telegraphVfx;

        /// <summary>Phase tuning: extra meteors per player on top of the serialized value.</summary>
        public int BonusMeteors { get; set; }

        protected override IEnumerator Perform(PlayerAvatar target)
        {
            if (meteor == null)
                yield break;
            yield return new WaitForSeconds(windup);
            if (!Actor.Health.IsAlive)
                yield break;

            foreach (var player in PlayerAvatar.All)
            {
                if (player == null || !player.IsStanding || (player.transform.position - transform.position).sqrMagnitude > range * range)
                    continue;
                Drop(player.transform.position);
                for (int i = 0; i < extraMeteorsPerPlayer + BonusMeteors; i++)
                {
                    Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(extraOffset.x, extraOffset.y);
                    Drop(player.transform.position + new Vector3(offset.x, 0f, offset.y));
                }
            }
            yield return new WaitForSeconds(recovery);
        }

        private void Drop(Vector3 around)
        {
            Vector3 point = Physics.Raycast(around + Vector3.up * 3f, Vector3.down, out var hit, 30f, groundMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : around;
            float radius = Mathf.Max(1f, meteor.explosionRadius);
            VfxNetwork.Spawn(telegraphVfx, point, Quaternion.identity, radius);

            Vector2 tilt = Random.insideUnitCircle * fallHeight * 0.35f;
            Vector3 origin = point + new Vector3(tilt.x, fallHeight, tilt.y);
            Vector3 path = point - origin;
            float speed = path.magnitude / fallTime;
            var info = DamageInfo.Create(damage * Actor.DamageMultiplier, damageType, NetworkObject, Actor.Health.Team, point, path.normalized);
            ProjectileNetwork.FireServer(meteor, origin, path.normalized, 1f, info, NetworkObject, ProjectileNetwork.NoClient, speed);
        }
    }
}
