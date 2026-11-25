using UnityEngine;

namespace RiskOfMagic.Vfx
{
    /// <summary>
    /// Keeps a pooled local effect on a moving target (auras like the arcane shield) without parenting it: the pool can
    /// reuse it freely and it never dies with the target. Stops following when the target is gone or the effect released.
    /// </summary>
    public sealed class VfxFollow : MonoBehaviour
    {
        private Transform target;
        private Vector3 offset;

        public static void Attach(GameObject effect, Transform followTarget, Vector3 worldOffset)
        {
            if (effect == null || followTarget == null)
                return;
            if (!effect.TryGetComponent(out VfxFollow follow))
                follow = effect.AddComponent<VfxFollow>();
            follow.target = followTarget;
            follow.offset = worldOffset;
            follow.LateUpdate();
        }

        private void LateUpdate()
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                target = null;
                return;
            }
            transform.position = target.position + offset;
        }

        private void OnDisable() => target = null; // back to the pool: the next use may not follow
    }
}
