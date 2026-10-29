using System;
using RiskOfMagic.Characters;
using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Skills
{
    /// <summary>
    /// Runs the 4 skill slots. The owner predicts cooldowns and casts; the server re-validates stocks
    /// and executes authoritative effects; other peers replay feedback.
    /// </summary>
    [RequireComponent(typeof(PlayerAvatar), typeof(CharacterStats))]
    public class SkillController : NetworkBehaviour
    {
        public const int SlotCount = 4;

        [SerializeField] private LayerMask aimMask = Physics.DefaultRaycastLayers;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;
        [SerializeField] private float maxAimDistance = 300f;
        [SerializeField, Tooltip("Max distance between the claimed cast origin and the server-side camera.")]
        private float maxOriginError = 4f;
        [SerializeField] private float serverCooldownTolerance = 0.25f;

        private readonly SkillSlotState[] slots = new SkillSlotState[SlotCount];
        private readonly RaycastHit[] aimHits = new RaycastHit[16];
        private PlayerAvatar avatar;
        private CharacterStats stats;
        private float busyUntil;
        private int chargingSlot = -1;
        private float chargeStartTime;

        /// <summary>Owner only, after a successful local cast.</summary>
        public event Action<SkillSlot> SkillActivated;
        public event Action LoadoutChanged;

        public SkillContext Context { get; private set; }
        public LayerMask AimMask => aimMask;
        public LayerMask HitMask => hitMask;
        public bool IsBusy => Time.time < busyUntil;
        public bool IsCharging => chargingSlot >= 0;
        public SkillSlot ChargingSlot => (SkillSlot)Mathf.Max(0, chargingSlot);

        public float ChargeProgress => chargingSlot < 0
            ? 0f
            : Mathf.Clamp01((Time.time - chargeStartTime) / slots[chargingSlot].Definition.maxChargeTime);

        public SkillSlotState GetSlot(SkillSlot slot) => slots[(int)slot];

        private void Awake()
        {
            avatar = GetComponent<PlayerAvatar>();
            stats = GetComponent<CharacterStats>();
            Context = new SkillContext(this, avatar, stats);
        }

        public void SetLoadout(CharacterDefinition character)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var skill = character != null ? character.GetSkill((SkillSlot)i) : null;
                slots[i] = skill != null ? new SkillSlotState(skill) : null;
            }
            chargingSlot = -1;
            busyUntil = 0f;
            LoadoutChanged?.Invoke();
        }

        private void Update()
        {
            if (!IsSpawned)
                return;

            float multiplier = stats.CooldownMultiplier;
            foreach (var slot in slots)
                slot?.Tick(Time.deltaTime, multiplier);

            if (IsOwner)
                HandleInput();
        }

        private void HandleInput()
        {
            if (!avatar.ControlEnabled || !avatar.Look.IsCursorLocked)
            {
                chargingSlot = -1;
                return;
            }

            var input = avatar.Input;
            if (chargingSlot >= 0)
            {
                var charging = (SkillSlot)chargingSlot;
                if (!input.IsSkillHeld(charging))
                {
                    float charge = ChargeProgress;
                    chargingSlot = -1;
                    TryActivate(charging, charge);
                }
                return;
            }

            // Higher slots take priority when several are pressed on the same frame.
            for (int i = SlotCount - 1; i >= 0; i--)
            {
                var state = slots[i];
                if (state == null)
                    continue;

                var slot = (SkillSlot)i;
                var definition = state.Definition;
                bool wants = definition.activationMode == SkillActivationMode.Hold ? input.IsSkillHeld(slot) : input.WasSkillPressed(slot);
                if (!wants || !CanStart(state))
                    continue;

                if (definition.activationMode == SkillActivationMode.Charge)
                {
                    chargingSlot = i;
                    chargeStartTime = Time.time;
                    if (!definition.keepsSprint)
                        avatar.Motor.CancelSprint();
                }
                else
                {
                    TryActivate(slot, 0f);
                }
                return;
            }
        }

        private bool CanStart(SkillSlotState state)
            => state.HasStock && (!IsBusy || state.Definition.canCastWhileBusy) && state.Definition.CanActivate(Context, state);

        private void TryActivate(SkillSlot slot, float charge)
        {
            var state = slots[(int)slot];
            if (state == null || !CanStart(state))
                return;

            var definition = state.Definition;
            state.Consume();
            busyUntil = Time.time + definition.GetCastDuration(stats);
            if (!definition.keepsSprint)
                avatar.Motor.CancelSprint();

            var activation = BuildActivation(charge);
            definition.PrepareActivation(Context, state, ref activation);
            state.LastActivationTime = Time.time;

            definition.OnOwnerExecute(Context, activation);
            SkillActivated?.Invoke(slot);

            if (IsServer)
                ExecuteOnServer(slot, activation);
            else
                ActivateRpc((byte)slot, activation);
        }

        private SkillActivation BuildActivation(float charge)
        {
            Ray aim = avatar.Look.AimRay;
            Vector3 target = aim.GetPoint(maxAimDistance);

            int count = Physics.RaycastNonAlloc(aim, aimHits, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore);
            Array.Sort(aimHits, 0, count, RaycastHitDistanceComparer.Instance);
            for (int i = 0; i < count; i++)
            {
                if (Context.IsSelf(aimHits[i].collider))
                    continue;
                target = aimHits[i].point;
                break;
            }

            return new SkillActivation
            {
                Origin = aim.origin,
                Direction = aim.direction,
                Target = target,
                Charge = charge
            };
        }

        [Rpc(SendTo.Server)]
        private void ActivateRpc(byte slotIndex, SkillActivation activation)
        {
            if (slotIndex >= SlotCount || slots[slotIndex] == null || !avatar.IsAlive)
                return;
            if ((activation.Origin - avatar.Look.CameraPivot.position).sqrMagnitude > maxOriginError * maxOriginError)
                return;

            activation.Direction = activation.Direction.sqrMagnitude > 0.0001f ? activation.Direction.normalized : transform.forward;
            activation.Charge = Mathf.Clamp01(activation.Charge);
            if (!slots[slotIndex].TryConsume(serverCooldownTolerance, stats.CooldownMultiplier))
                return;

            ExecuteOnServer((SkillSlot)slotIndex, activation);
        }

        private void ExecuteOnServer(SkillSlot slot, SkillActivation activation)
        {
            slots[(int)slot].Definition.OnServerExecute(Context, activation);
            ExecutedRpc((byte)slot, activation);
        }

        [Rpc(SendTo.NotOwner)]
        private void ExecutedRpc(byte slotIndex, SkillActivation activation)
        {
            if (slotIndex < SlotCount)
                slots[slotIndex]?.Definition.OnRemoteExecute(Context, activation);
        }

        public override void OnNetworkDespawn() => StopAllCoroutines();
    }
}
