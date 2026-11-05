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
        private readonly NetworkVariable<double> shieldUntil = new();
        private readonly NetworkVariable<float> shieldReduction = new();
        private readonly NetworkVariable<int> dotMask = new();
        private IHealthHooks[] hooks = Array.Empty<IHealthHooks>();

        /// <summary>(current, max)</summary>
        public event Action<float, float> HealthChanged;
        public event Action<DamageInfo> Damaged;
        public event Action<DamageInfo> Died;
        public event Action Revived;

        /// <summary>Server only: every damage instance (item procs, stats).</summary>
        public static event Action<Health, DamageInfo> ServerDamaged;
        /// <summary>Server only: every death.</summary>
        public static event Action<Health, DamageInfo> ServerKilled;
        /// <summary>Every peer: every damage instance (damage numbers, hit markers).</summary>
        public static event Action<Health, DamageInfo> AnyDamaged;
        /// <summary>Every peer: every death (kill markers...).</summary>
        public static event Action<Health, DamageInfo> AnyDied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ServerDamaged = null;
            ServerKilled = null;
            AnyDamaged = null;
            AnyDied = null;
        }

        Health IDamageable.Health => this;
        public Team Team => team;
        public float Current => current.Value;
        public float Max => max.Value;
        public float Normalized => max.Value > 0f ? current.Value / max.Value : 0f;

        /// <summary>Server side, set by stats (items): multiplies incoming damage.</summary>
        public float DamageTakenMultiplier { get; set; } = 1f;
        /// <summary>Server side, set by stats (items): removed from each hit, never below 1.</summary>
        public float FlatDamageReduction { get; set; }
        public bool IsAlive => current.Value > 0f;
        /// <summary>Bit per DamageType with an active damage over time (replicated, for visuals).</summary>
        public int DotMask => dotMask.Value;
        public bool HasDot(DamageType type) => (dotMask.Value & (1 << (int)type)) != 0;

        /// <summary>Server only (DotSystem).</summary>
        public void SetDotMask(int mask)
        {
            if (IsServer && IsSpawned && dotMask.Value != mask)
                dotMask.Value = mask;
        }

        public bool IsShielded => IsSpawned && NetworkManager.ServerTime.Time < shieldUntil.Value;
        public float ShieldTimeRemaining => IsShielded ? (float)(shieldUntil.Value - NetworkManager.ServerTime.Time) : 0f;

        /// <summary>Server only. Reduces incoming damage by <paramref name="reduction"/> (0.95 = -95%) for a duration.</summary>
        public void AddShield(float reduction, float duration)
        {
            if (!AssertServer())
                return;
            double until = NetworkManager.ServerTime.Time + duration;
            shieldReduction.Value = IsShielded ? Mathf.Max(shieldReduction.Value, reduction) : Mathf.Clamp01(reduction);
            if (until > shieldUntil.Value)
                shieldUntil.Value = until;
        }

        private void Awake() => hooks = GetComponents<IHealthHooks>();

        /// <summary>Server only. Health cost that ignores every modifier (can kill).</summary>
        public void PayHealth(float amount)
        {
            ApplyDamage(new DamageInfo
            {
                Amount = amount,
                Type = DamageType.Arcane,
                SourceTeam = Team.Neutral,
                IgnoreModifiers = true,
                Point = transform.position + Vector3.up
            });
        }

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

        /// <summary>Server only. Hits are ignored until this server time (except IgnoreModifiers damage).</summary>
        public double ServerInvulnerableUntil { get; set; }

        public void ApplyDamage(DamageInfo info)
        {
            if (!AssertServer() || !IsAlive || info.Amount <= 0f)
                return;
            if (!info.IgnoreModifiers && NetworkManager.ServerTime.Time < ServerInvulnerableUntil)
                return;
            if (!allowFriendlyFire && team != Team.Neutral && info.SourceTeam == team)
                return;

            if (!info.IgnoreModifiers)
                info.Amount = ModifyDamage(info);
            if (info.Amount <= 0f)
                return;

            float next = Mathf.Max(0f, current.Value - info.Amount);
            bool deathPrevented = false;
            if (next <= 0f)
            {
                foreach (var hook in hooks)
                {
                    if (!hook.TryPreventDeath(this, info, out float healthFraction))
                        continue;
                    next = Mathf.Max(1f, max.Value * Mathf.Clamp01(healthFraction));
                    deathPrevented = true;
                    break;
                }
            }
            current.Value = next;

            ServerDamaged?.Invoke(this, info);
            DamagedRpc(info);
            if (next > 0f)
            {
                if (deathPrevented)
                    RevivedRpc();
                return;
            }
            ServerKilled?.Invoke(this, info);
            DiedRpc(info);
        }

        /// <summary>Attacker bonuses → shield → hooks (status, items) → stats multiplier → flat reduction (min 1).</summary>
        private float ModifyDamage(in DamageInfo info)
        {
            float amount = info.Amount;
            if (info.TryGetSource(out var source) && source.TryGetComponent(out IDamageDealerHooks dealer))
                amount = dealer.ModifyOutgoingDamage(this, info, amount);
            if (IsShielded)
                amount *= 1f - shieldReduction.Value;
            foreach (var hook in hooks)
                amount = hook.ModifyIncomingDamage(this, info, amount);
            amount *= DamageTakenMultiplier;
            if (FlatDamageReduction > 0f && amount > 1f)
                amount = Mathf.Max(1f, amount - FlatDamageReduction);
            return amount;
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
        private void DamagedRpc(DamageInfo info)
        {
            Damaged?.Invoke(info);
            AnyDamaged?.Invoke(this, info);
        }

        [Rpc(SendTo.Everyone)]
        private void DiedRpc(DamageInfo info)
        {
            Died?.Invoke(info);
            AnyDied?.Invoke(this, info);
        }

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
