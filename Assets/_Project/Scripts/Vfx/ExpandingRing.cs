using RiskOfMagic.Core;
using UnityEngine;

namespace RiskOfMagic.Vfx
{
    /// <summary>
    /// Ground ring growing at a fixed speed up to a max radius given by the spawn scale (VfxUtility/VfxNetwork scale),
    /// then returned to the pool. Keep `speed` equal to the gameplay wave speed (EnemyShockwaveAttack).
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class ExpandingRing : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 14f;
        [SerializeField, Min(8)] private int segments = 64;
        [SerializeField] private float height = 0.15f;

        private LineRenderer line;
        private float radius;
        private float maxRadius;
        private bool started;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segments;
        }

        private void OnEnable()
        {
            started = false;
            radius = 0f;
            SetRadius(0f);
        }

        private void Update()
        {
            if (!started)
            {
                // The spawn scale is applied after OnEnable by VfxUtility: read it on the first frame.
                maxRadius = Mathf.Max(0.5f, transform.localScale.x);
                transform.localScale = Vector3.one;
                transform.rotation = Quaternion.identity;
                started = true;
            }

            radius += speed * Time.deltaTime;
            if (radius >= maxRadius)
            {
                GameObjectPool.Release(gameObject);
                return;
            }
            SetRadius(radius);
        }

        private void SetRadius(float r)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, height, Mathf.Sin(a) * r));
            }
        }
    }
}
