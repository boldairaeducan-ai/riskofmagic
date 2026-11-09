using RiskOfMagic.Characters;
using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using RiskOfMagic.Player;
using RiskOfMagic.Skills;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiskOfMagic.Animation
{
    /// <summary>
    /// Spawns the character's models (CharacterDefinition.modelPrefab / viewModelPrefab) and drives their Animators on
    /// every peer from replicated data. Parameters: see AnimParams. Cosmetic only: gameplay never waits for animations.
    /// </summary>
    [RequireComponent(typeof(PlayerAvatar))]
    public class PlayerAnimationDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("Parent of the third-person model (default: this object).")] private Transform modelRoot;
        [SerializeField, Min(0f)] private float locomotionDamp = 0.08f;
        [SerializeField] private float groundProbe = 0.25f;
        [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;

        private readonly AnimatorGroup animators = new();
        private PlayerAvatar avatar;
        private GameObject model;
        private GameObject viewModel;
        private Vector3 lastPosition;
        private Vector3 smoothedVelocity;
        private bool wasGrounded = true;
        private bool subscribed;

        public GameObject Model => model;
        public GameObject ViewModel => viewModel;

        private void Awake()
        {
            avatar = GetComponent<PlayerAvatar>();
            if (modelRoot == null)
                modelRoot = transform;
        }

        private void OnEnable()
        {
            avatar.CharacterChanged += OnCharacterChanged;
            lastPosition = transform.position;
        }

        private void OnDisable()
        {
            avatar.CharacterChanged -= OnCharacterChanged;
            Unsubscribe();
        }

        private void Start()
        {
            Subscribe();
            if (avatar.Character != null)
                OnCharacterChanged(avatar.Character);
        }

        private void Subscribe()
        {
            if (subscribed)
                return;
            subscribed = true;
            avatar.Health.Damaged += OnDamaged;
            avatar.Health.Revived += OnRevived;
            if (avatar.Skills != null)
                avatar.Skills.SkillExecuted += OnSkillExecuted;
            if (avatar.Equipment != null)
                avatar.Equipment.Activated += OnEquipmentActivated;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;
            subscribed = false;
            avatar.Health.Damaged -= OnDamaged;
            avatar.Health.Revived -= OnRevived;
            if (avatar.Skills != null)
                avatar.Skills.SkillExecuted -= OnSkillExecuted;
            if (avatar.Equipment != null)
                avatar.Equipment.Activated -= OnEquipmentActivated;
        }

        private void OnCharacterChanged(CharacterDefinition character)
        {
            if (model != null)
                Destroy(model);
            if (viewModel != null)
                Destroy(viewModel);
            model = viewModel = null;
            animators.Clear();

            if (character.modelPrefab != null)
            {
                model = Instantiate(character.modelPrefab, modelRoot);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                if (avatar.IsOwner)
                    foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                        r.shadowCastingMode = ShadowCastingMode.ShadowsOnly; // own body: shadow only in first person
                animators.AddFrom(model);
            }
            if (character.viewModelPrefab != null && avatar.IsOwner && avatar.Look.Camera != null)
            {
                viewModel = Instantiate(character.viewModelPrefab, avatar.Look.Camera.transform);
                viewModel.transform.localPosition = Vector3.zero;
                viewModel.transform.localRotation = Quaternion.identity;
                foreach (var r in viewModel.GetComponentsInChildren<Renderer>(true))
                    r.shadowCastingMode = ShadowCastingMode.Off;
                animators.AddFrom(viewModel);
            }
            avatar.SetPlaceholderVisible(model == null);
        }

        private bool hidden;

        /// <summary>Other players don't see a stealthed Shadow (the owner keeps their own view).</summary>
        private void UpdateStealthVisibility()
        {
            bool stealth = !avatar.IsOwner && avatar.Status != null && avatar.Status.Has(StatusType.Stealth);
            if (stealth == hidden)
                return;
            hidden = stealth;
            if (model != null)
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    r.enabled = !stealth;
            avatar.SetPlaceholderVisible(model == null && !stealth);
        }

        private void Update()
        {
            UpdateStealthVisibility();
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 position = transform.position;
            Vector3 velocity = (position - lastPosition) / dt;
            lastPosition = position;
            if (velocity.sqrMagnitude > 2500f)
                velocity = Vector3.zero; // teleport
            smoothedVelocity = Vector3.Lerp(smoothedVelocity, velocity, 1f - Mathf.Exp(-15f * dt));
            if (animators.Count == 0)
                return;

            bool grounded = avatar.IsOwner && avatar.Motor != null
                ? avatar.Motor.IsGrounded
                : Physics.Raycast(position + Vector3.up * 0.1f, Vector3.down, 0.1f + groundProbe, groundMask, QueryTriggerInteraction.Ignore);
            if (grounded != wasGrounded)
                animators.SetTrigger(grounded ? AnimParams.Land : AnimParams.Jump);
            wasGrounded = grounded;

            Vector3 local = transform.InverseTransformDirection(smoothedVelocity);
            animators.SetFloat(AnimParams.Speed, new Vector2(local.x, local.z).magnitude, locomotionDamp);
            animators.SetFloat(AnimParams.MoveX, local.x, locomotionDamp);
            animators.SetFloat(AnimParams.MoveY, local.z, locomotionDamp);
            animators.SetFloat(AnimParams.VerticalSpeed, smoothedVelocity.y);
            animators.SetBool(AnimParams.Grounded, grounded);
            animators.SetBool(AnimParams.Downed, avatar.IsDowned);
            animators.SetBool(AnimParams.Dead, !avatar.IsAlive);
            if (avatar.IsOwner)
            {
                animators.SetFloat(AnimParams.AimPitch, avatar.Look.Pitch);
                animators.SetBool(AnimParams.Sprinting, avatar.Motor != null && avatar.Motor.IsSprinting);
            }
        }

        private void OnDamaged(DamageInfo info)
        {
            if (!info.IgnoreModifiers)
                animators.SetTrigger(AnimParams.Hit);
        }

        private void OnRevived() => animators.SetTrigger(AnimParams.Revive);

        private void OnSkillExecuted(SkillSlot slot, SkillDefinition skill)
        {
            animators.SetInteger(AnimParams.SkillSlot, (int)slot);
            animators.SetTrigger(skill.animatorTrigger);
        }

        private void OnEquipmentActivated() => animators.SetTrigger(AnimParams.UseEquipment);
    }
}
