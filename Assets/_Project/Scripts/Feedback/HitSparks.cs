using UnityEngine;

namespace RiskOfMagic.Feedback
{
    /// <summary>One pooled particle system emitting every hit spark burst (stretched additive sparks with gravity).</summary>
    public static class HitSparks
    {
        private static ParticleSystem system;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => system = null;

        /// <summary>Burst at <paramref name="point"/>, sprayed around <paramref name="normal"/> (towards the attacker).</summary>
        public static void Burst(Vector3 point, Vector3 normal, Color color, int count, float speed = 1f, float size = 1f)
        {
            var config = HitFeedbackConfig.Instance;
            if (count <= 0 || config.sparkMaterial == null || !Ensure(config))
                return;
            if (normal.sqrMagnitude < 0.0001f)
                normal = Vector3.up;
            normal.Normalize();
            Color32 tint = new Color(color.r, color.g, color.b, 1f); // particle colors are LDR: the glow comes from the HDR material
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Vector3.Slerp(normal, Random.onUnitSphere, 0.55f).normalized + Vector3.up * 0.25f;
                emit.position = point;
                emit.velocity = direction * Random.Range(3.5f, 9f) * speed;
                emit.startLifetime = Random.Range(0.16f, 0.36f);
                emit.startSize = Random.Range(0.04f, 0.1f) * size;
                emit.startColor = tint;
                system.Emit(emit, 1);
            }
        }

        private static bool Ensure(HitFeedbackConfig config)
        {
            if (system != null)
                return true;
            var go = new GameObject("HitSparks");
            Object.DontDestroyOnLoad(go);
            system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.6f;
            main.startSpeed = 0f;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var drag = system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2.5f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.045f;
            renderer.lengthScale = 1.6f;
            renderer.sharedMaterial = config.sparkMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            system.Play();
            return true;
        }
    }
}
