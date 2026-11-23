using RiskOfMagic.Combat;
using RiskOfMagic.Enemies;
using UnityEngine;

namespace RiskOfMagic.Animation
{
    /// <summary>
    /// Spawns EnemyDefinition.modelPrefab (placeholder hidden, hitboxes kept) and drives its Animator on every peer:
    /// locomotion from movement, attacks (per-EnemyAttack trigger + AttackIndex), hit, dodge/leap cues, death, freeze.
    /// A humanoid model without Animator Controller is animated by code instead (ProceduralHumanoidMotion), a rigid model
    /// (no skeleton, no controller) by ProceduralRigidMotion.
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
        private ProceduralHumanoidMotion procedural;
        private ProceduralRigidMotion rigid;

        private void Awake()
        {
            actor = GetComponent<EnemyActor>();
            attacks = GetComponents<EnemyAttack>();
            dodge = GetComponent<EnemyDodge>();
            leap = GetComponent<EnemyLeap>();

            var prefab = actor.Definition != null ? actor.Definition.modelPrefab : null;
            if (prefab != null)
            {
                var definition = actor.Definition;
                var model = Instantiate(prefab, transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, definition.modelYaw, 0f) * prefab.transform.localRotation;
                if (definition.modelMaterial != null)
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    {
                        var materials = renderer.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++)
                            materials[i] = definition.modelMaterial;
                        renderer.sharedMaterials = materials;
                    }
                FitHeight(model, definition.modelHeight);
                actor.UseModel(model);
                if (ProceduralHumanoidMotion.CanDrive(model))
                    procedural = model.AddComponent<ProceduralHumanoidMotion>();
                else if (!HasController(model))
                    rigid = model.AddComponent<ProceduralRigidMotion>();
                else
                    animators.AddFrom(model);
            }
            // Animators placed directly in the prefab by hand also work.
            if (animators.Count == 0)
                animators.AddFrom(gameObject);
        }

        private static bool HasController(GameObject model)
        {
            foreach (var animator in model.GetComponentsInChildren<Animator>(true))
                if (animator.runtimeAnimatorController != null)
                    return true;
            return false;
        }

        /// <summary>Scales the model so its renderers are <paramref name="height"/> tall (0 = as authored).</summary>
        private static void FitHeight(GameObject model, float height)
        {
            if (height <= 0f)
                return;
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);
            float current = bounds.size.y / Mathf.Max(0.0001f, model.transform.lossyScale.y);
            if (current > 0.01f)
                model.transform.localScale *= height / current;
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
            if (procedural != null)
            {
                Vector3 flat = new(smoothedVelocity.x, 0f, smoothedVelocity.z);
                procedural.SetLocomotion(flat.magnitude, actor.Definition != null ? actor.Definition.moveSpeed : 5f);
                procedural.SetDead(actor.IsSpawned && !actor.Health.IsAlive);
                procedural.SetFrozen(actor.IsFrozen);
            }
            if (rigid != null)
            {
                Vector3 flat = new(smoothedVelocity.x, 0f, smoothedVelocity.z);
                rigid.SetLocomotion(flat.magnitude, actor.Definition != null ? actor.Definition.moveSpeed : 5f);
                rigid.SetDead(actor.IsSpawned && !actor.Health.IsAlive);
                rigid.SetFrozen(actor.IsFrozen);
            }
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
            animators.SetFloat(AnimParams.ActionSpeed, 1f / Mathf.Max(0.15f, attack.WindupTime + attack.RecoveryTime));
            animators.SetTrigger(attack.AnimatorTrigger);
            if (procedural != null)
                procedural.PlayAttack(attack.WindupTime, attack.RecoveryTime);
            if (rigid != null)
                rigid.PlayAttack(attack.WindupTime, attack.RecoveryTime);
        }

        private void OnDamaged(DamageInfo info)
        {
            if (info.IgnoreModifiers || !actor.Health.IsAlive)
                return;
            animators.SetTrigger(AnimParams.Hit);
            if (procedural != null)
                procedural.PlayHit();
            if (rigid != null)
                rigid.PlayHit();
        }

        // Dodge/leap run on the server: relay them to every peer.
        private void OnServerDodge(Vector3 direction) => actor.BroadcastCue(EnemyAnimationCue.Dodge);
        private void OnServerLeap(Vector3 destination) => actor.BroadcastCue(EnemyAnimationCue.Leap);

        private void OnCue(EnemyAnimationCue cue)
        {
            animators.SetTrigger(cue == EnemyAnimationCue.Dodge ? AnimParams.Dodge : AnimParams.Leap);
            if (rigid != null)
                rigid.PlayHop();
        }
    }
}
