using UnityEngine;

namespace RiskOfMagic.Animation
{
    /// <summary>
    /// Animates a rigid model (one mesh, no skeleton, e.g. low poly monster packs) by moving its root: idle breathing,
    /// waddle + bob while walking, wind-up / lunge attacks, flinch on hit, hop for dodges and leaps, fall on death.
    /// Added by EnemyAnimationDriver when the model has neither a usable Animator Controller nor humanoid bones.
    /// Offsets are in the enemy's space (+Z = facing), scaled by the model height.
    /// </summary>
    public sealed class ProceduralRigidMotion : MonoBehaviour
    {
        [Header("Locomotion")]
        [SerializeField, Tooltip("Steps per second at full speed.")] private float stepRate = 1.7f;
        [SerializeField, Tooltip("Vertical bob per step, fraction of the height.")] private float bob = 0.045f;
        [SerializeField, Tooltip("Side-to-side roll per step (degrees).")] private float waddle = 7f;
        [SerializeField, Tooltip("Forward lean at full speed (degrees).")] private float lean = 8f;
        [SerializeField, Tooltip("Idle breathing (scale Y amplitude).")] private float breathe = 0.018f;

        [Header("Actions")]
        [SerializeField] private float windupLean = -14f;
        [SerializeField] private float strikeLean = 24f;
        [SerializeField, Tooltip("Forward lunge of the strike, fraction of the height.")] private float strikeReach = 0.12f;
        [SerializeField] private float hitDuration = 0.25f;
        [SerializeField] private float hopDuration = 0.45f;
        [SerializeField, Tooltip("Hop height, fraction of the model height.")] private float hopHeight = 0.3f;
        [SerializeField] private float deathFallTime = 0.45f;

        private Vector3 basePosition;
        private Quaternion baseRotation;
        private Vector3 baseScale;
        private float height = 1.8f;
        private float time;
        private float phase;
        private float speed01;
        private bool frozen;
        private bool dead;
        private float deathTime = -100f;
        private float attackTime = -100f;
        private float windup;
        private float recovery;
        private float hitTime = -100f;
        private float hopTime = -100f;
        private float sideSign = 1f;

        private void Awake()
        {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            baseScale = transform.localScale;
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers)
                    bounds.Encapsulate(r.bounds);
                float parentScale = transform.parent != null ? Mathf.Max(0.0001f, transform.parent.lossyScale.y) : 1f;
                height = Mathf.Max(0.2f, bounds.size.y / parentScale);
            }
            time = Random.value * 10f; // desynchronise idle breathing between enemies
            sideSign = Random.value < 0.5f ? -1f : 1f;
        }

        public void SetLocomotion(float horizontalSpeed, float maxSpeed)
            => speed01 = Mathf.Clamp01(horizontalSpeed / Mathf.Max(0.1f, maxSpeed));

        public void SetFrozen(bool value) => frozen = value;

        public void SetDead(bool value)
        {
            if (value && !dead)
                deathTime = time;
            dead = value;
        }

        public void PlayAttack(float windupTime, float recoveryTime)
        {
            attackTime = time;
            windup = Mathf.Max(0.05f, windupTime);
            recovery = Mathf.Max(0.1f, recoveryTime);
        }

        public void PlayHit() => hitTime = time;

        public void PlayHop() => hopTime = time;

        private void LateUpdate()
        {
            if (!frozen)
                time += Time.deltaTime;

            float pitch = 0f, roll = 0f;
            Vector3 offset = Vector3.zero;
            Vector3 scale = Vector3.one;

            if (dead)
            {
                float u = Mathf.Clamp01((time - deathTime) / deathFallTime);
                u *= u; // falls faster and faster
                roll = 88f * u * sideSign;
                offset.y = -0.05f * height * u;
                Apply(offset, pitch, roll, scale);
                return;
            }

            // Locomotion: steps advance with the speed (no foot sliding at low speed).
            if (!frozen)
                phase += Time.deltaTime * stepRate * Mathf.PI * 2f * Mathf.Lerp(0.6f, 1f, speed01) * (speed01 > 0.05f ? 1f : 0f);
            float step = Mathf.Sin(phase);
            offset.y += Mathf.Abs(step) * bob * height * speed01;
            roll += step * waddle * speed01;
            pitch += lean * speed01;
            float breath = Mathf.Sin(time * 2.2f) * breathe * (1f - speed01);
            scale = new Vector3(1f - breath * 0.5f, 1f + breath, 1f - breath * 0.5f);

            // Attack: lean back during the wind-up, then a fast forward strike and a slow return.
            float a = time - attackTime;
            if (a >= 0f && a < windup + recovery)
            {
                if (a < windup)
                {
                    float k = Smooth(a / windup);
                    pitch += windupLean * k;
                    scale = Vector3.Scale(scale, new Vector3(1f + 0.05f * k, 1f - 0.07f * k, 1f + 0.05f * k));
                }
                else
                {
                    float r = (a - windup) / recovery;
                    float strike = r < 0.2f ? r / 0.2f : 1f - Smooth((r - 0.2f) / 0.8f);
                    float release = r < 0.2f ? 1f - r / 0.2f : 0f;
                    pitch += strikeLean * strike + windupLean * release;
                    offset.z += strikeReach * height * strike;
                    scale = Vector3.Scale(scale, new Vector3(1f - 0.03f * strike, 1f + 0.05f * strike, 1f - 0.03f * strike));
                }
            }

            // Hit: short flinch backwards with a scale punch.
            float h = time - hitTime;
            if (h >= 0f && h < hitDuration)
            {
                float k = 1f - h / hitDuration;
                pitch -= 10f * k;
                offset.z -= 0.04f * height * k;
                scale *= 1f + 0.06f * k;
            }

            // Dodge / leap: a hop.
            float p = time - hopTime;
            if (p >= 0f && p < hopDuration)
            {
                float u = p / hopDuration;
                offset.y += Mathf.Sin(Mathf.PI * u) * hopHeight * height;
                pitch += Mathf.Sin(Mathf.PI * u) * 10f;
            }

            Apply(offset, pitch, roll, scale);
        }

        private void Apply(Vector3 offset, float pitch, float roll, Vector3 scale)
        {
            transform.localPosition = basePosition + offset;
            transform.localRotation = Quaternion.Euler(pitch, 0f, roll) * baseRotation;
            transform.localScale = Vector3.Scale(baseScale, scale);
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
