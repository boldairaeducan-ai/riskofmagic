using System;
using System.Collections;
using RiskOfMagic.Audio;
using RiskOfMagic.Core;
using RiskOfMagic.Items;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Equipment
{
    /// <summary>Replicated equipment slot. The owner presses F, the server validates the cooldown and activates.</summary>
    public class PlayerEquipment : NetworkBehaviour
    {
        [SerializeField] private float maxOriginError = 4f;
        [SerializeField] private float cooldownTolerance = 0.2f;

        private readonly NetworkVariable<int> equipmentIndex = new(-1);
        private readonly NetworkVariable<double> readyAt = new();

        /// <summary>Every peer.</summary>
        public event Action Changed;
        /// <summary>Every peer: a successful activation (animation hook).</summary>
        public event Action Activated;

        public PlayerAvatar Avatar { get; private set; }

        public EquipmentDefinition Current => GameCatalog.Instance != null ? GameCatalog.Instance.GetEquipment(equipmentIndex.Value) : null;
        public float CooldownRemaining => IsSpawned ? Mathf.Max(0f, (float)(readyAt.Value - NetworkManager.ServerTime.Time)) : 0f;
        public float CooldownProgress
        {
            get
            {
                var current = Current;
                return current == null || CooldownRemaining <= 0f ? 1f : 1f - CooldownRemaining / current.cooldown;
            }
        }

        private void Awake() => Avatar = GetComponent<PlayerAvatar>();

        public override void OnNetworkSpawn()
        {
            equipmentIndex.OnValueChanged += OnEquipmentChanged;
            readyAt.OnValueChanged += OnReadyAtChanged;
        }

        public override void OnNetworkDespawn()
        {
            equipmentIndex.OnValueChanged -= OnEquipmentChanged;
            readyAt.OnValueChanged -= OnReadyAtChanged;
        }

        /// <summary>Server only. The previous equipment is dropped at the player's feet.</summary>
        public void SetEquipment(EquipmentDefinition definition)
        {
            if (!IsServer)
                return;
            var previous = Current;
            if (previous != null && previous != definition)
                ItemPickup.SpawnEquipment(previous, transform.position + transform.forward * 1.5f);
            equipmentIndex.Value = definition != null ? GameCatalog.Instance.IndexOf(definition) : -1;
        }

        /// <summary>Server only (new run).</summary>
        public void ClearEquipment()
        {
            if (!IsServer)
                return;
            equipmentIndex.Value = -1;
            readyAt.Value = 0d;
        }

        /// <summary>Server only: lets equipment run timed effects.</summary>
        public Coroutine Run(IEnumerator routine) => StartCoroutine(routine);

        private void Update()
        {
            if (!IsSpawned || !IsOwner || Current == null)
                return;
            if (!Avatar.ControlEnabled || !Avatar.Look.IsCursorLocked || !Avatar.Input.EquipmentPressed || CooldownRemaining > cooldownTolerance)
                return;
            Ray aim = Avatar.Look.AimRay;
            ActivateRpc(aim.origin, aim.direction);
        }

        [Rpc(SendTo.Server)]
        private void ActivateRpc(Vector3 origin, Vector3 direction)
        {
            var current = Current;
            if (current == null || !Avatar.IsAlive || CooldownRemaining > cooldownTolerance)
                return;
            if ((origin - Avatar.Look.CameraPivot.position).sqrMagnitude > maxOriginError * maxOriginError)
                return;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            if (current.Activate(this, origin, direction))
                readyAt.Value = NetworkManager.ServerTime.Time + current.cooldown;
        }

        private void OnEquipmentChanged(int previous, int current) => Changed?.Invoke();

        // readyAt only grows on a successful activation: free replicated cue for the sound.
        private void OnReadyAtChanged(double previous, double current)
        {
            var equipment = Current;
            if (current > previous && equipment != null)
            {
                AudioService.PlayAt(equipment.activateSound, transform.position);
                Activated?.Invoke();
            }
        }
    }
}
