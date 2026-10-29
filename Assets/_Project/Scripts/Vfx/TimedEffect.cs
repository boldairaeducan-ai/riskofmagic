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

        public void Restart()
        {
            age = 0f;
            startScale = transform.localScale;
        }

        private void OnEnable() => Restart();

        private void Update()
        {
            age += Time.deltaTime;
            float t = age / lifetime;
            transform.localScale = startScale * scaleOverLifetime.Evaluate(Mathf.Clamp01(t));
            if (t >= 1f)
                GameObjectPool.Release(gameObject);
        }
    }
}
