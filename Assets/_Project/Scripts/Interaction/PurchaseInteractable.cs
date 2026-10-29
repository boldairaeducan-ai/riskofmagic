using System;
using RiskOfMagic.Player;
using RiskOfMagic.Run;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Interaction
{
    /// <summary>
    /// Base for anything bought with gold (chests, shrines, drones...). Cost scales with the run
    /// difficulty like in RoR2: baseCost * coefficient^exponent, locked when spawned.
    /// </summary>
    public class PurchaseInteractable : NetworkBehaviour, IInteractable
    {
        [SerializeField] private string displayName = "Coffre";
        [SerializeField] private uint baseCost = 25;
        [SerializeField] private bool scaleWithDifficulty = true;
        [SerializeField] private float costExponent = 1.25f;
        [SerializeField, Min(0), Tooltip("0 = infinite")] private int maxUses = 1;
        [SerializeField, Min(1f)] private float costMultiplierPerUse = 1f;

        private readonly NetworkVariable<uint> cost = new();
        private readonly NetworkVariable<int> uses = new();

        /// <summary>Server only.</summary>
        public event Action<PlayerAvatar> Purchased;
        /// <summary>Every peer (feedback).</summary>
        public event Action PurchasedFeedback;

        public string DisplayName => displayName;
        public uint Cost => cost.Value;
        public int Uses => uses.Value;
        public bool IsAvailable => maxUses <= 0 || uses.Value < maxUses;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                RefreshCost();
        }

        /// <summary>Server only.</summary>
        public void RefreshCost()
        {
            float value = baseCost * Mathf.Pow(costMultiplierPerUse, uses.Value);
            if (scaleWithDifficulty)
                value *= Mathf.Pow(RunDifficulty.CurrentCoefficient, costExponent);
            cost.Value = (uint)Mathf.Max(0, Mathf.RoundToInt(value));
        }

        public virtual bool CanInteract(PlayerAvatar player)
            => IsAvailable && player.IsAlive && player.Wallet.CanAfford(Cost);

        public virtual string GetPrompt(PlayerAvatar player)
        {
            if (!IsAvailable)
                return string.Empty;
            string costText = Cost == 0 ? "gratuit" : $"{Cost} or";
            return player.Wallet.CanAfford(Cost) ? $"[E] {displayName} ({costText})" : $"{displayName} ({costText}) - pas assez d'or";
        }

        public void Interact(PlayerAvatar player)
        {
            if (!IsServer || !CanInteract(player) || !player.Wallet.TrySpend(Cost))
                return;

            uses.Value++;
            OnPurchased(player);
            Purchased?.Invoke(player);
            PurchasedRpc();
            if (IsAvailable)
                RefreshCost();
        }

        /// <summary>Server only: grant the reward here.</summary>
        protected virtual void OnPurchased(PlayerAvatar buyer) { }

        [Rpc(SendTo.Everyone)]
        private void PurchasedRpc() => PurchasedFeedback?.Invoke();
    }
}
