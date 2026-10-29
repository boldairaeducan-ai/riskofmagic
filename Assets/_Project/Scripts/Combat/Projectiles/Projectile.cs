using System;
using System.Collections.Generic;
using RiskOfMagic.Core;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Combat.Projectiles
{
    /// <summary>
    /// Non-networked projectile simulated identically on every peer.
    /// Only the authoritative (server) instance deals damage; the others are visual.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        private static readonly RaycastHit[] hits = new RaycastHit[16];

        private readonly HashSet<Health> damaged = new();
        private ProjectileDefinition definition;
        private DamageInfo damage;
        private bool authoritative;
        private Transform ignoreRoot;
        private Vector3 velocity;
        private float age;
        private float scale;
        private int pierced;
        private TrailRenderer[] trails;

        public static Projectile Launch(ProjectileDefinition definition, Vector3 origin, Vector3 direction, float scale,
            DamageInfo damage, bool authoritative, Transform ignoreRoot)
        {
            if (definition == null || definition.visualPrefab == null)
            {
                Debug.LogWarning("[Projectile] Missing definition or visual prefab.");
                return null;
            }

            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            var go = GameObjectPool.Spawn(definition.visualPrefab, origin, Quaternion.LookRotation(direction));
            if (!go.TryGetComponent(out Projectile projectile))
                projectile = go.AddComponent<Projectile>();
            projectile.Init(definition, direction, scale, damage, authoritative, ignoreRoot);
            return projectile;
        }

        private void Init(ProjectileDefinition def, Vector3 direction, float newScale, DamageInfo info, bool isAuthoritative, Transform ignore)
        {
            definition = def;
            damage = info;
            authoritative = isAuthoritative;
            ignoreRoot = ignore;
            scale = newScale;
            velocity = direction * def.speed;
            age = 0f;
            pierced = 0;
            damaged.Clear();
            transform.localScale = def.visualPrefab.transform.localScale * newScale;

            trails ??= GetComponentsInChildren<TrailRenderer>(true);
            foreach (var trail in trails)
                trail.Clear();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age >= definition.lifetime)
            {
                if (definition.explodeOnExpire)
                    Impact(transform.position);
                else
                    Release();
                return;
            }

            velocity += Vector3.down * (definition.gravity * dt);
            Vector3 step = velocity * dt;
            float distance = step.magnitude;
            if (distance < 0.00001f)
                return;

            Vector3 position = transform.position;
            Vector3 direction = step / distance;
            int count = Physics.SphereCastNonAlloc(position, definition.radius, direction, hits, distance, definition.hitMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, 0, count, RaycastHitDistanceComparer.Instance);

            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot))
                    continue;

                Vector3 point = hit.distance <= 0f ? position : hit.point;
                if (CombatUtility.TryGetDamageable(hit.collider, out var target))
                {
                    if (target.Team == damage.SourceTeam || !target.IsAlive || !damaged.Add(target.Health))
                        continue; // allies and already-hit targets don't block

                    if (authoritative)
                    {
                        var info = damage;
                        info.Point = point;
                        info.Direction = direction;
                        target.ApplyDamage(info);
                    }

                    if (pierced < definition.pierceCount)
                    {
                        pierced++;
                        continue;
                    }
                }

                Impact(point);
                return;
            }

            transform.position = position + step;
            transform.rotation = Quaternion.LookRotation(velocity);
        }

        private void Impact(Vector3 point)
        {
            if (authoritative && definition.explosionRadius > 0f)
            {
                var info = damage;
                AreaDamage.Apply(point, definition.explosionRadius * scale, info, definition.hitMask, damaged);
            }
            VfxUtility.Spawn(definition.impactVfx, point, Quaternion.identity, scale);
            Release();
        }

        private void Release() => GameObjectPool.Release(gameObject);
    }
}
