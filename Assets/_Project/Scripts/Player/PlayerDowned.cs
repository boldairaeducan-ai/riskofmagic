using System;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using RiskOfMagic.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Player
{
    /// <summary>
    /// "Downed" state: a lethal hit leaves the player at 1 HP, unable to move or attack and invulnerable,
    /// with a bleed-out timer. A standing teammate holding E nearby revives them (the timer pauses meanwhile).
    /// Solo / last standing player: no downed state, death is immediate. Runs after PlayerInventory's death
    /// prevention (Phoenix Feather) thanks to the component order.
    /// </summary>
    public class PlayerDowned : NetworkBehaviour, IHealthHooks, IHoldInteractable
    {
        [SerializeField, Min(1f)] private float bleedOutDuration = 30f;
        [SerializeField, Min(0.1f)] private float reviveDuration = 3f;
        [SerializeField, Range(0.05f, 1f)] private float reviveHealthFraction = 0.3f;
        [SerializeField, Min(0.5f)] private float reviveRange = 3f;

        private readonly NetworkVariable<bool> downed = new();
        private readonly NetworkVariable<double> bleedOutAt = new();
        private readonly NetworkVariable<float> reviveProgress = new();
        private readonly HashSet<PlayerAvatar> revivers = new();
        private PlayerAvatar avatar;
        private bool bleedingOut;

        /// <summary>Every peer.</summary>
        public event Action Changed;

        public bool IsDowned => downed.Value;
        public float ReviveProgress => reviveProgress.Value;
        public float BleedOutRemaining => IsDowned && IsSpawned ? Mathf.Max(0f, (float)(bleedOutAt.Value - NetworkManager.ServerTime.Time)) : 0f;
        public bool IsBeingRevived => IsDowned && reviveProgress.Value > 0f;

        private void Awake() => avatar = GetComponent<PlayerAvatar>();

        public override void OnNetworkSpawn() => downed.OnValueChanged += OnDownedChanged;
        public override void OnNetworkDespawn() => downed.OnValueChanged -= OnDownedChanged;

        private void OnDownedChanged(bool previous, bool current) => Changed?.Invoke();

        // ---------------------------------------------------------------- Health hooks (server)

        public float ModifyIncomingDamage(Health self, in DamageInfo info, float amount) => IsDowned ? 0f : amount;

        public bool TryPreventDeath(Health self, in DamageInfo info, out float healthFraction)
        {
            healthFraction = 0f; // Health keeps 1 HP
            if (IsDowned || bleedingOut || !AnyOtherStandingPlayer())
                return false;

            revivers.Clear();
            reviveProgress.Value = 0f;
            bleedOutAt.Value = NetworkManager.ServerTime.Time + bleedOutDuration;
            downed.Value = true;
            return true;
        }

        // ---------------------------------------------------------------- Revive interaction

        public bool CanInteract(PlayerAvatar player)
            => IsDowned && player != avatar && player.IsAlive && !player.IsDowned;

        public string GetPrompt(PlayerAvatar player)
            => CanInteract(player) ? $"[Maintenir E] Relever ({Mathf.RoundToInt(reviveProgress.Value * 100f)}%)" : string.Empty;

        public void Interact(PlayerAvatar player) { }

        public void SetHolding(PlayerAvatar player, bool holding)
        {
            if (!IsServer)
                return;
            if (holding && CanInteract(player))
                revivers.Add(player);
            else
                revivers.Remove(player);
        }

        // ---------------------------------------------------------------- Server loop

        private void Update()
        {
            if (!IsServer || !IsSpawned || !IsDowned)
                return;

            revivers.RemoveWhere(r => r == null || !CanInteract(r)
                                      || (r.transform.position - transform.position).sqrMagnitude > reviveRange * reviveRange);

            if (revivers.Count > 0)
            {
                bleedOutAt.Value += Time.deltaTime; // timer paused while being revived
                reviveProgress.Value = Mathf.Min(1f, reviveProgress.Value + Time.deltaTime / reviveDuration);
                if (reviveProgress.Value >= 1f)
                    Revive();
                return;
            }

            if (NetworkManager.ServerTime.Time >= bleedOutAt.Value || !AnyOtherStandingPlayer())
                BleedOut();
        }

        /// <summary>Server only: back on their feet without healing (new stage / new run).</summary>
        public void ForceStand()
        {
            if (!IsServer || !IsDowned)
                return;
            revivers.Clear();
            reviveProgress.Value = 0f;
            downed.Value = false;
        }

        /// <summary>Server only: instant revive with a health fraction (Intervention divine).</summary>
        public void ReviveNow(float healthFraction)
        {
            if (!IsServer || !IsDowned)
                return;
            revivers.Clear();
            reviveProgress.Value = 0f;
            downed.Value = false;
            avatar.Health.Heal(avatar.Health.Max * Mathf.Clamp01(healthFraction));
        }

        private void Revive()
        {
            revivers.Clear();
            reviveProgress.Value = 0f;
            downed.Value = false;
            avatar.Health.Heal(avatar.Health.Max * reviveHealthFraction);
        }

        private void BleedOut()
        {
            revivers.Clear();
            reviveProgress.Value = 0f;
            downed.Value = false;
            bleedingOut = true;
            avatar.Health.ApplyDamage(new DamageInfo
            {
                Amount = avatar.Health.Max * 10f,
                Type = DamageType.Physical,
                SourceTeam = Team.Neutral,
                IgnoreModifiers = true,
                Point = transform.position + Vector3.up
            });
            bleedingOut = false;
        }

        private bool AnyOtherStandingPlayer()
        {
            foreach (var player in PlayerAvatar.All)
                if (player != avatar && player.IsAlive && !player.IsDowned)
                    return true;
            return false;
        }
    }
}
