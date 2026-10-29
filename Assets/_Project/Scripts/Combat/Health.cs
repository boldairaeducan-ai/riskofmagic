using System;
using RiskOfMagic.Core;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Combat
{
    /// <summary>
    /// Server-authoritative health. Events are raised on every peer.
    /// </summary>
    public class Health : NetworkBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float baseMaxHealth = 100f;
        [SerializeField] private Team team = Team.Neutral;
        [SerializeField] private bool allowFriendlyFire;

        private readonly NetworkVariable<float> current = new();
        private readonly NetworkVariable<float> max = new();

        /// <summary>(current, max)</summary>
        public event Action<float, float> HealthChanged;
        public event Action<DamageInfo> Damaged;
        public event Action<DamageInfo> Died;
        public event Action Revived;

        public Health Health => this;
        public Team Team => team;
        public float Current => current.Value;
        public float Max => max.Value;
        public float Normalized => max.Value > 0f ? current.Value / max.Value : 0f;
        public bool IsAlive => current.Value > 0f;

        /// <summary>Call before spawn (e.g. from Awake) to override the inspector value.</summary>
        public void SetBaseMaxHealth(float value) => baseMaxHealth = Mathf.Max(1f, value);

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                max.Value = baseMaxHealth;
                current.Value = baseMaxHealth;
            }

            current.OnValueChanged += OnHealthValueChanged;
            max.OnValueChanged += OnHealthValueChanged;
            HealthChanged?.Invoke(current.Value, max.Value);
        }

        public override void OnNetworkDespawn()
        {
            current.OnValueChanged -= OnHealthValueChanged;
            max.OnValueChanged -= OnHealthValueChanged;
        }

        public void ApplyDamage(DamageInfo info)
        {
            if (!AssertServer() || !IsAlive || info.Amount <= 0f)
                return;
            if (!allowFriendlyFire && team != Team.Neutral && info.SourceTeam == team)
                return;

            float previous = current.Value;
            float next = Mathf.Max(0f, previous - info.Amount);
            info.Amount = previous - next;
            current.Value = next;

            DamagedRpc(info);
            if (next <= 0f)
                DiedRpc(info);
        }

        public void Heal(float amount)
        {
            if (!AssertServer() || !IsAlive || amount <= 0f)
                return;
            current.Value = Mathf.Min(max.Value, current.Value + amount);
        }

        public void SetMaxHealth(float value, bool keepRatio = true)
        {
            if (!AssertServer())
                return;
            float ratio = Normalized;
            max.Value = Mathf.Max(1f, value);
            current.Value = keepRatio ? max.Value * ratio : Mathf.Min(current.Value, max.Value);
        }

        public void Revive(float normalizedHealth = 1f)
        {
            if (!AssertServer() || IsAlive)
                return;
            current.Value = Mathf.Max(1f, max.Value * Mathf.Clamp01(normalizedHealth));
            RevivedRpc();
        }

        [Rpc(SendTo.Everyone)]
        private void DamagedRpc(DamageInfo info) => Damaged?.Invoke(info);

        [Rpc(SendTo.Everyone)]
        private void DiedRpc(DamageInfo info) => Died?.Invoke(info);

        [Rpc(SendTo.Everyone)]
        private void RevivedRpc() => Revived?.Invoke();

        private void OnHealthValueChanged(float previous, float next) => HealthChanged?.Invoke(current.Value, max.Value);

        private bool AssertServer()
        {
            if (IsServer)
                return true;
            Debug.LogWarning($"[Health] Server-only call ignored on {name}.", this);
            return false;
        }
    }
}
