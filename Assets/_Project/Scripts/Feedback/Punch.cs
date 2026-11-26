using System.Collections;
using UnityEngine;

namespace RiskOfMagic.Feedback
{
    /// <summary>Cosmetic scale jolt of a transform (chest opening...), runs on the target itself.</summary>
    public sealed class Punch : MonoBehaviour
    {
        private Vector3 baseScale;
        private Coroutine running;

        /// <summary>Squash-and-stretch kick: <paramref name="amount"/> (0.25 = 25%) decaying over <paramref name="duration"/>.</summary>
        public static void Play(Transform target, float amount, float duration)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
                return;
            if (!target.TryGetComponent(out Punch punch))
            {
                punch = target.gameObject.AddComponent<Punch>();
                punch.baseScale = target.localScale;
            }
            if (punch.running != null)
                punch.StopCoroutine(punch.running);
            punch.running = punch.StartCoroutine(punch.Run(amount, duration));
        }

        private IEnumerator Run(float amount, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = 1f - t / duration;
                float wave = Mathf.Sin(t / duration * Mathf.PI * 3f) * amount * k;
                transform.localScale = Vector3.Scale(baseScale, new Vector3(1f + wave, 1f - wave, 1f + wave));
                yield return null;
            }
            transform.localScale = baseScale;
            running = null;
        }
    }
}
