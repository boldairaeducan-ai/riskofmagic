using RiskOfMagic.Combat;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Player
{
    /// <summary>Per-player run statistics for the end screen (server writes, every peer reads).</summary>
    [RequireComponent(typeof(PlayerAvatar))]
    public class PlayerRunStats : NetworkBehaviour
    {
        private readonly NetworkVariable<float> damageDealt = new();
        private readonly NetworkVariable<int> kills = new();
        private readonly NetworkVariable<int> deaths = new();
        private readonly NetworkVariable<int> goldEarned = new();

        private PlayerAvatar avatar;

        public float DamageDealt => damageDealt.Value;
        public int Kills => kills.Value;
        /// <summary>Knocked down or killed outright.</summary>
        public int Deaths => deaths.Value;
        public int GoldEarned => goldEarned.Value;

        private void Awake() => avatar = GetComponent<PlayerAvatar>();

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;
            Health.ServerDamaged += OnServerDamaged;
            Health.ServerKilled += OnServerKilled;
            avatar.Wallet.GoldChanged += OnGoldChanged;
            if (avatar.Downed != null)
                avatar.Downed.Changed += OnDownedChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
                return;
            Health.ServerDamaged -= OnServerDamaged;
            Health.ServerKilled -= OnServerKilled;
            avatar.Wallet.GoldChanged -= OnGoldChanged;
            if (avatar.Downed != null)
                avatar.Downed.Changed -= OnDownedChanged;
        }

        /// <summary>Server only (new run).</summary>
        public void ResetStats()
        {
            if (!IsServer)
                return;
            damageDealt.Value = 0f;
            kills.Value = 0;
            deaths.Value = 0;
            goldEarned.Value = 0;
        }

        private bool IsFromMe(in DamageInfo info) => info.HasSource && info.SourceId == NetworkObjectId;

        private void OnServerDamaged(Health victim, DamageInfo info)
        {
            if (IsFromMe(info) && victim.gameObject != gameObject)
                damageDealt.Value += info.Amount;
        }

        private void OnServerKilled(Health victim, DamageInfo info)
        {
            if (victim.gameObject == gameObject)
            {
                // A downed player bleeding out was already counted when knocked down.
                if (avatar.Downed == null || !avatar.Downed.IsDowned)
                    deaths.Value++;
            }
            else if (IsFromMe(info))
            {
                kills.Value++;
            }
        }

        private void OnGoldChanged(uint previous, uint current)
        {
            if (current > previous)
                goldEarned.Value += (int)(current - previous);
        }

        private void OnDownedChanged()
        {
            if (avatar.Downed.IsDowned)
                deaths.Value++;
        }
    }
}
