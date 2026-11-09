using System.Collections.Generic;
using RiskOfMagic.Audio;
using RiskOfMagic.Combat;
using RiskOfMagic.Enemies;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Feedback
{
    /// <summary>
    /// Cosmetic hit reactions of an enemy, on every peer (added by EnemyActor): flash, squash-and-stretch punch,
    /// visual hit-stop with jitter, sparks and impact sound at the hit point, camera kick for the local attacker,
    /// and on death a long flash, a spark burst, the body thrown back / toppled, then shrunk out before despawn.
    /// Only the visual children move: colliders, NavMesh and network state are untouched.
    /// </summary>
    public class EnemyHitFeedback : MonoBehaviour
    {
        private const float HeavyHitFraction = 0.25f;

        private struct Part
        {
            public Transform Transform;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        private EnemyActor actor;
        private Health health;
        private readonly List<Part> parts = new();
        private Animator[] animators = System.Array.Empty<Animator>();
        private float[] animatorSpeeds = System.Array.Empty<float>();
        private bool animatorsPaused;
        private int partsChildCount = -1;

        private float squashAmount;
        private float squashTime = 999f;
        private float hitStopUntil;
        private bool dead;
        private float deathTime;
        private float despawnAt;
        private Vector3 recoilDirection;
        private bool topple;

        private void Awake()
        {
            actor = GetComponent<EnemyActor>();
            health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            if (health == null)
                return;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }
            Restore();
            dead = false;
        }

        // ---------------------------------------------------------------- Events

        private void OnDamaged(DamageInfo info)
        {
            if (info.IgnoreModifiers || dead || info.Amount <= 0f)
                return;
            var config = HitFeedbackConfig.Instance;
            float fraction = health.Max > 0f ? Mathf.Clamp01(info.Amount / health.Max) : 0f;
            float heavy = Mathf.Clamp01(fraction / HeavyHitFraction);
            bool dot = info.ProcCoefficient <= 0f && !info.IsCritical; // DoT ticks and item damage: light touch only

            if (actor != null)
                actor.Flash(info.IsCritical ? config.criticalFlashColor : config.flashColor, config.flashDuration * (dot ? 0.6f : 1f));
            if (dot)
                return;

            CollectParts();
            squashAmount = Mathf.Lerp(config.squash.x, config.squash.y, heavy) * (info.IsCritical ? config.criticalSquashMultiplier : 1f);
            squashTime = 0f;
            float stop = Mathf.Lerp(config.hitStop.x, config.hitStop.y, heavy) + (info.IsCritical ? config.criticalHitStopBonus : 0f);
            hitStopUntil = Mathf.Max(hitStopUntil, Time.time + stop);

            Vector3 point = HitPoint(info);
            Vector3 toAttacker = -HitDirection(info);
            int sparks = Mathf.RoundToInt(Mathf.Lerp(config.sparkCount.x, config.sparkCount.y, heavy)) + (info.IsCritical ? config.criticalSparkBonus : 0);
            HitSparks.Burst(point, toAttacker, info.IsCritical ? config.critical : config.GetColor(info.Type), sparks, 1f, info.IsCritical ? 1.3f : 1f);

            var audio = AudioService.Config;
            if (audio != null)
                AudioService.PlayAt(info.IsCritical && audio.enemyImpactCritical != null ? audio.enemyImpactCritical : audio.enemyImpact, point);

            if (IsLocalAttacker(info) && (heavy >= 1f || info.IsCritical))
                AddTrauma(config.heavyHitTrauma);
        }

        private void OnDied(DamageInfo info)
        {
            if (dead)
                return;
            var config = HitFeedbackConfig.Instance;
            CollectParts();
            dead = true;
            deathTime = Time.time + config.killHitStop; // the recoil starts after the freeze
            hitStopUntil = Time.time + config.killHitStop;
            float delay = actor != null && actor.Definition != null ? actor.Definition.despawnDelay : 2f;
            despawnAt = Time.time + Mathf.Max(0.3f, delay);
            squashAmount = config.squash.y * config.criticalSquashMultiplier;
            squashTime = 0f;

            Vector3 direction = HitDirection(info);
            direction.y = 0f;
            recoilDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : -transform.forward;
            topple = animators.Length == 0; // animated models play their own death

            if (actor != null)
                actor.Flash(info.IsCritical ? config.criticalFlashColor : config.flashColor, config.killFlashDuration);
            Vector3 center = Center();
            HitSparks.Burst(center, Vector3.up, info.IsCritical ? config.critical : config.GetColor(info.Type), config.killSparkCount, 0.8f, 1.6f);

            var audio = AudioService.Config;
            if (audio != null)
                AudioService.PlayAt(audio.enemyKillImpact, center);

            if (IsLocalAttacker(info))
                AddTrauma(actor != null && actor.IsBoss ? config.bossKillTrauma : config.killTrauma);
        }

        // ---------------------------------------------------------------- Animation

        private void LateUpdate()
        {
            if (parts.Count == 0)
                return;
            var config = HitFeedbackConfig.Instance;
            bool frozen = Time.time < hitStopUntil;
            PauseAnimators(frozen);

            if (!frozen)
                squashTime += Time.deltaTime;
            float punch = squashAmount * Mathf.Sin(squashTime * config.squashFrequency) * Mathf.Exp(-squashTime * config.squashDamping);
            if (squashTime * config.squashDamping > 8f)
                punch = 0f;
            Vector3 squash = new(1f + punch, 1f - punch, 1f + punch); // positive = flattened and widened

            Vector3 jitter = frozen ? Random.insideUnitSphere * config.hitStopJitter : Vector3.zero;

            Quaternion recoilRotation = Quaternion.identity;
            Vector3 recoilOffset = Vector3.zero;
            float shrink = 1f;
            if (dead)
            {
                float t = Mathf.Clamp01((Time.time - deathTime) / Mathf.Max(0.01f, config.killRecoilDuration));
                float eased = 1f - (1f - t) * (1f - t);
                Vector3 localDirection = transform.InverseTransformDirection(recoilDirection);
                recoilOffset = localDirection * (config.killPush * eased);
                if (topple)
                {
                    Vector3 axis = Vector3.Cross(Vector3.up, localDirection);
                    if (axis.sqrMagnitude > 0.0001f)
                        recoilRotation = Quaternion.AngleAxis(config.killTopple * eased * eased, axis.normalized);
                }
                float remaining = despawnAt - Time.time;
                if (remaining < config.shrinkOutDuration)
                    shrink = Mathf.Clamp01(remaining / Mathf.Max(0.01f, config.shrinkOutDuration));
            }

            foreach (var part in parts)
            {
                if (part.Transform == null)
                    continue;
                Vector3 position = Vector3.Scale(part.Position, squash) * shrink;
                part.Transform.localPosition = recoilRotation * position + recoilOffset + jitter;
                part.Transform.localRotation = recoilRotation * part.Rotation;
                part.Transform.localScale = Vector3.Scale(part.Scale, squash) * shrink;
            }
        }

        // ---------------------------------------------------------------- Helpers

        /// <summary>Direct children holding renderers (placeholder parts or the model), captured at rest.</summary>
        private void CollectParts()
        {
            if (partsChildCount == transform.childCount)
                return;
            Restore();
            parts.Clear();
            partsChildCount = transform.childCount;
            foreach (Transform child in transform)
            {
                if (child.GetComponentInChildren<Renderer>(true) == null)
                    continue;
                parts.Add(new Part { Transform = child, Position = child.localPosition, Rotation = child.localRotation, Scale = child.localScale });
            }
            animators = GetComponentsInChildren<Animator>(true);
            animatorsPaused = false;
        }

        private void Restore()
        {
            foreach (var part in parts)
            {
                if (part.Transform == null)
                    continue;
                part.Transform.localPosition = part.Position;
                part.Transform.localRotation = part.Rotation;
                part.Transform.localScale = part.Scale;
            }
            PauseAnimators(false);
            squashTime = 999f;
            hitStopUntil = 0f;
        }

        /// <summary>Hit-stop: pauses the Animators, then gives back the speed they had (freeze, slows).</summary>
        private void PauseAnimators(bool pause)
        {
            if (pause == animatorsPaused)
                return;
            animatorsPaused = pause;
            if (pause)
                animatorSpeeds = new float[animators.Length];
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] == null)
                    continue;
                if (pause)
                {
                    animatorSpeeds[i] = animators[i].speed;
                    animators[i].speed = 0f;
                }
                else if (i < animatorSpeeds.Length)
                    animators[i].speed = animatorSpeeds[i];
            }
        }

        private Vector3 Center()
        {
            Bounds? bounds = null;
            if (actor != null && actor.Renderers != null)
                foreach (var r in actor.Renderers)
                {
                    if (r == null || !r.enabled)
                        continue;
                    if (bounds.HasValue)
                    {
                        var b = bounds.Value;
                        b.Encapsulate(r.bounds);
                        bounds = b;
                    }
                    else
                        bounds = r.bounds;
                }
            return bounds.HasValue ? bounds.Value.center : transform.position + Vector3.up;
        }

        private Vector3 HitPoint(in DamageInfo info) => info.Point != Vector3.zero ? info.Point : Center();

        /// <summary>Direction the hit travels (attacker → enemy).</summary>
        private Vector3 HitDirection(in DamageInfo info)
        {
            if (info.Direction.sqrMagnitude > 0.0001f)
                return info.Direction.normalized;
            if (info.TryGetSource(out var source))
            {
                Vector3 away = transform.position - source.transform.position;
                if (away.sqrMagnitude > 0.0001f)
                    return away.normalized;
            }
            return -transform.forward;
        }

        private static bool IsLocalAttacker(in DamageInfo info)
        {
            var local = PlayerAvatar.Local;
            return local != null && info.HasSource && info.SourceId == local.NetworkObjectId;
        }

        private static void AddTrauma(float amount)
        {
            var local = PlayerAvatar.Local;
            if (local != null && local.Look != null && local.Look.Effects != null && amount > 0f)
                local.Look.Effects.AddTrauma(amount);
        }
    }
}
