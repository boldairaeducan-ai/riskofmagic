using System;
using Unity.Netcode;

namespace RiskOfMagic.Economy
{
    /// <summary>Server-authoritative gold.</summary>
    public class PlayerWallet : NetworkBehaviour
    {
        private readonly NetworkVariable<uint> gold = new();

        /// <summary>(previous, current), raised on every peer.</summary>
        public event Action<uint, uint> GoldChanged;

        public uint Gold => gold.Value;

        public override void OnNetworkSpawn() => gold.OnValueChanged += OnGoldChanged;
        public override void OnNetworkDespawn() => gold.OnValueChanged -= OnGoldChanged;

        public bool CanAfford(uint amount) => gold.Value >= amount;

        /// <summary>Server only.</summary>
        public void Add(uint amount)
        {
            if (IsServer && amount > 0)
                gold.Value = (uint)Math.Min((ulong)gold.Value + amount, uint.MaxValue);
        }

        /// <summary>Server only.</summary>
        public bool TrySpend(uint amount)
        {
            if (!IsServer || gold.Value < amount)
                return false;
            gold.Value -= amount;
            return true;
        }

        private void OnGoldChanged(uint previous, uint current) => GoldChanged?.Invoke(previous, current);
    }
}
