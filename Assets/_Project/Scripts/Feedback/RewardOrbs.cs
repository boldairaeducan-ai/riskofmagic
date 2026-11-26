using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using RiskOfMagic.Enemies;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Feedback
{
    /// <summary>
    /// Cosmetic gold and experience orbs (every peer, local player only): when an enemy dies, glowing orbs pop out of it,
    /// hang for a moment, then home into the local player. The gold and experience are credited by the server as before;
    /// the orbs only show it. One pooled particle system steered by hand.
    /// </summary>
    public sealed class RewardOrbs : MonoBehaviour
    {
        private static readonly Color GoldColor = new(1f, 0.78f, 0.2f);
        private static readonly Color ExperienceColor = new(0.45f, 0.75f, 1f);
        private const float HangTime = 0.35f;
        private const float MaxSpeed = 32f;

        private static RewardOrbs instance;
        private ParticleSystem system;
        private ParticleSystem.Particle[] particles = new ParticleSystem.Particle[256];

        /// <summary>Orbs made of ArtLibrary prefabs (pooled), moved like the particles.</summary>
        private struct PrefabOrb
        {
            public GameObject Instance;
            public Vector3 Velocity;
            public float Age;
        }

        private readonly List<PrefabOrb> prefabOrbs = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null)
                return;
            var go = new GameObject("RewardOrbs");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RewardOrbs>();
        }

        private void OnEnable() => Health.AnyDied += OnAnyDied;
        private void OnDisable() => Health.AnyDied -= OnAnyDied;

        private void OnAnyDied(Health victim, DamageInfo info)
        {
            if (victim == null || PlayerAvatar.Local == null || !victim.TryGetComponent(out EnemyActor enemy) || enemy.IsIllusion)
                return;
            int gold = enemy.Definition != null ? enemy.Definition.goldReward : 5;
            Vector3 center = victim.transform.position + Vector3.up * 1.1f;
            var juice = ArtLibrary.Juice;
            Burst(center, GoldColor, Mathf.Clamp(gold / 3, 2, enemy.IsBoss ? 24 : 8), 0.22f, juice?.goldOrb);
            Burst(center, ExperienceColor, enemy.IsBoss ? 12 : Mathf.Clamp(gold / 6, 1, 4), 0.16f, juice?.experienceOrb);
        }

        private void Burst(Vector3 point, Color color, int count, float size, ArtLibrary.EffectSlot slot)
        {
            if (slot != null && slot.IsSet)
            {
                for (int i = 0; i < count; i++)
                {
                    var orb = GameObjectPool.Spawn(slot.prefab, point, Quaternion.identity);
                    orb.transform.localScale = slot.prefab.transform.localScale * slot.scale;
                    prefabOrbs.Add(new PrefabOrb { Instance = orb, Velocity = (Random.onUnitSphere + Vector3.up * 1.4f).normalized * Random.Range(3f, 6.5f) });
                }
                return;
            }
            if (!Ensure())
                return;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = (Random.onUnitSphere + Vector3.up * 1.4f).normalized;
                emit.position = point;
                emit.velocity = direction * Random.Range(3f, 6.5f);
                emit.startLifetime = 3f; // killed on arrival
                emit.startSize = size * Random.Range(0.8f, 1.2f);
                emit.startColor = color;
                system.Emit(emit, 1);
            }
        }

        private bool Ensure()
        {
            if (system != null)
                return true;
            var config = HitFeedbackConfig.Instance;
            if (config == null || config.sparkMaterial == null)
                return false;
            system = gameObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var trails = system.trails;
            trails.enabled = true;
            trails.lifetime = 0.12f;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            trails.inheritParticleColor = true;
            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = config.sparkMaterial;
            renderer.trailMaterial = config.sparkMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            system.Play();
            return true;
        }

        /// <summary>Same motion as the particles: pop out, hang, then home into the player faster and faster.</summary>
        private Vector3 Steer(Vector3 position, Vector3 velocity, float age, Vector3 target, out bool arrived)
        {
            arrived = false;
            float dt = Time.deltaTime;
            if (age < HangTime)
                return velocity * (1f - Mathf.Clamp01(5f * dt));
            Vector3 toTarget = target - position;
            float distance = toTarget.magnitude;
            if (distance < 0.7f)
            {
                arrived = true;
                return velocity;
            }
            float speed = Mathf.Min(MaxSpeed, 6f + (age - HangTime) * 45f);
            return Vector3.Lerp(velocity, toTarget / distance * speed, Mathf.Clamp01(8f * dt));
        }

        private void UpdatePrefabOrbs()
        {
            var local = PlayerAvatar.Local;
            Vector3 target = local != null ? local.transform.position + Vector3.up * 1.1f : Vector3.zero;
            for (int i = prefabOrbs.Count - 1; i >= 0; i--)
            {
                var orb = prefabOrbs[i];
                if (orb.Instance == null)
                {
                    prefabOrbs.RemoveAt(i);
                    continue;
                }
                orb.Age += Time.deltaTime;
                var orbTransform = orb.Instance.transform;
                orb.Velocity = Steer(orbTransform.position, orb.Velocity, orb.Age, target, out bool arrived);
                if (local == null || arrived || orb.Age > 4f)
                {
                    GameObjectPool.Release(orb.Instance);
                    prefabOrbs.RemoveAt(i);
                    continue;
                }
                orbTransform.position += orb.Velocity * Time.deltaTime;
                prefabOrbs[i] = orb;
            }
        }

        private void LateUpdate()
        {
            if (prefabOrbs.Count > 0)
                UpdatePrefabOrbs();
            if (system == null || system.particleCount == 0)
                return;
            var local = PlayerAvatar.Local;
            if (particles.Length < system.main.maxParticles)
                particles = new ParticleSystem.Particle[system.main.maxParticles];
            int count = system.GetParticles(particles);
            float dt = Time.deltaTime;
            Vector3 target = local != null ? local.transform.position + Vector3.up * 1.1f : Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                var p = particles[i];
                float age = p.startLifetime - p.remainingLifetime;
                if (local == null)
                {
                    p.remainingLifetime = 0f;
                }
                else if (age < HangTime)
                {
                    p.velocity *= 1f - Mathf.Clamp01(5f * dt); // pop out, then slow down
                }
                else
                {
                    // Home in, faster and faster, with a turn rate that tightens over time.
                    Vector3 toTarget = target - p.position;
                    float distance = toTarget.magnitude;
                    if (distance < 0.7f)
                    {
                        p.remainingLifetime = 0f;
                    }
                    else
                    {
                        float speed = Mathf.Min(MaxSpeed, 6f + (age - HangTime) * 45f);
                        p.velocity = Vector3.Lerp(p.velocity, toTarget / distance * speed, Mathf.Clamp01(8f * dt));
                    }
                }
                particles[i] = p;
            }
            system.SetParticles(particles, count);
        }
    }
}
