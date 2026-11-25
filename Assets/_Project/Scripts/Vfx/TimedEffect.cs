using RiskOfMagic.Core;
using UnityEngine;

namespace RiskOfMagic.Vfx
{
    /// <summary>Returns the effect to the pool after its lifetime, optionally animating its scale.</summary>
    public class TimedEffect : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float lifetime = 0.5f;
        [SerializeField] private AnimationCurve scaleOverLifetime = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        private float age;
        private Vector3 startScale;
        private float useLifetime = -1f; // this use only (reset when released)

        public float Lifetime => lifetime;

        /// <summary>Lifetime of the current use only (e.g. an aura that lasts as long as its buff).</summary>
        public void SetLifetimeForThisUse(float seconds) => useLifetime = Mathf.Max(0.01f, seconds);

        /// <summary>Lets plugged-in art (particle systems) finish before the effect is released.</summary>
        public void ExtendLifetime(float seconds) => lifetime = Mathf.Max(lifetime, seconds);

        public void Restart()
        {
            age = 0f;
            startScale = transform.localScale;
        }

        private void OnEnable() => Restart();

        private void OnDisable() => useLifetime = -1f;

        private void Update()
        {
            age += Time.deltaTime;
            float t = age / (useLifetime > 0f ? useLifetime : lifetime);
            transform.localScale = startScale * scaleOverLifetime.Evaluate(Mathf.Clamp01(t));
            if (t >= 1f)
                GameObjectPool.Release(gameObject);
        }
    }
}
