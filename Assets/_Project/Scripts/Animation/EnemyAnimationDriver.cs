using RiskOfMagic.Combat;
using RiskOfMagic.Enemies;
using UnityEngine;

namespace RiskOfMagic.Animation
{
    /// <summary>
    /// Spawns EnemyDefinition.modelPrefab (placeholder hidden, hitboxes kept) and drives its Animator on every peer:
    /// locomotion from movement, attacks (per-EnemyAttack trigger + AttackIndex), hit, dodge/leap cues, death, freeze.
    /// </summary>
    [RequireComponent(typeof(EnemyActor))]
    public class EnemyAnimationDriver : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float locomotionDamp = 0.1f;
        [SerializeField, Tooltip("Animator speed 0 while frozen (Frozen bool is set too).")] private bool pauseWhenFrozen = true;

        private readonly AnimatorGroup animators = new();
        private EnemyActor actor;
        private EnemyAttack[] attacks;
        private EnemyDodge dodge;
        private EnemyLeap leap;
        private Vector3 lastPosition;
        private Vector3 smoothedVelocity;
        private bool spawned;

        private void Awake()
        {
            actor = GetComponent<EnemyActor>();
            attacks = GetComponents<EnemyAttack>();
            dodge = GetComponent<EnemyDodge>();
            leap = GetComponent<EnemyLeap>();

            var prefab = actor.Definition != null ? actor.Definition.modelPrefab : null;
            if (prefab != null)
            {
                var model = Instantiate(prefab, transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                actor.UseModel(model);
                animators.AddFrom(model);
            }
            // Animators placed directly in the prefab by hand also work.
            if (animators.Count == 0)
                animators.AddFrom(gameObject);
        }

        // Start: EnemyActor.Awake (Health) has run whatever the component order.
        private void Start()
        {
            lastPosition = transform.position;
            actor.Health.Damaged += OnDamaged;
            actor.AnimationCue += OnCue;
            for (int i = 0; i < attacks.Length; i++)
            {
                int index = i;
                var attack = attacks[i];
                attack.AttackStarted += () => OnAttackStarted(index, attack);
            }
            if (dodge != null)
                dodge.DodgeStarted += OnServerDodge;
            if (leap != null)
                leap.LeapStarted += OnServerLeap;
        }

        private void OnDestroy()
        {
            if (actor == null || actor.Health == null)
                return;
            actor.Health.Damaged -= OnDamaged;
            actor.AnimationCue -= OnCue;
            if (dodge != null)
                dodge.DodgeStarted -= OnServerDodge;
            if (leap != null)
                leap.LeapStarted -= OnServerLeap;
            // Attack lambdas die with the object (enemies are destroyed, never pooled).
        }

        private void Update()
        {
            if (!spawned && actor.IsSpawned)
            {
                spawned = true;
                animators.SetTrigger(AnimParams.Spawn);
            }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 position = transform.position;
            Vector3 velocity = (position - lastPosition) / dt;
            lastPosition = position;
            if (velocity.sqrMagnitude > 2500f)
                velocity = Vector3.zero;
            smoothedVelocity = Vector3.Lerp(smoothedVelocity, velocity, 1f - Mathf.Exp(-12f * dt));
            if (animators.Count == 0)
                return;

            Vector3 local = transform.InverseTransformDirection(smoothedVelocity);
            animators.SetFloat(AnimParams.Speed, new Vector2(local.x, local.z).magnitude, locomotionDamp);
            animators.SetFloat(AnimParams.MoveX, local.x, locomotionDamp);
            animators.SetFloat(AnimParams.MoveY, local.z, locomotionDamp);
            animators.SetBool(AnimParams.Dead, actor.IsSpawned && !actor.Health.IsAlive);
            bool frozen = actor.IsFrozen;
            animators.SetBool(AnimParams.Frozen, frozen);
            if (pauseWhenFrozen)
                animators.SetSpeed(frozen ? 0f : 1f);
        }

        private void OnAttackStarted(int index, EnemyAttack attack)
        {
            animators.SetInteger(AnimParams.AttackIndex, index);
            animators.SetTrigger(attack.AnimatorTrigger);
        }

        private void OnDamaged(DamageInfo info)
        {
            if (!info.IgnoreModifiers && actor.Health.IsAlive)
                animators.SetTrigger(AnimParams.Hit);
        }

        // Dodge/leap run on the server: relay them to every peer.
        private void OnServerDodge(Vector3 direction) => actor.BroadcastCue(EnemyAnimationCue.Dodge);
        private void OnServerLeap(Vector3 destination) => actor.BroadcastCue(EnemyAnimationCue.Leap);

        private void OnCue(EnemyAnimationCue cue) =>
            animators.SetTrigger(cue == EnemyAnimationCue.Dodge ? AnimParams.Dodge : AnimParams.Leap);
    }
}
