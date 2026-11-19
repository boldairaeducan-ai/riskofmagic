using RiskOfMagic.Combat;
using RiskOfMagic.Networking;
using Unity.Collections;
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
        private readonly NetworkVariable<ushort> ping = new();
        private readonly NetworkVariable<FixedString64Bytes> displayName = new();
        private float nextPingUpdate;

        private PlayerAvatar avatar;

        public float DamageDealt => damageDealt.Value;
        public int Kills => kills.Value;
        /// <summary>Knocked down or killed outright.</summary>
        public int Deaths => deaths.Value;
        public int GoldEarned => goldEarned.Value;
        /// <summary>Round trip to the host in ms (0 for the host itself), updated every second by the server.</summary>
        public int PingMs => ping.Value;
        /// <summary>Platform name (Steam persona) sent by the owner; empty when unknown.</summary>
        public string DisplayName => displayName.Value.ToString();

        private void Update()
        {
            if (!IsServer || !IsSpawned || Time.unscaledTime < nextPingUpdate)
                return;
            nextPingUpdate = Time.unscaledTime + 1f;
            ulong rtt = 0;
            if (OwnerClientId != NetworkManager.ServerClientId && NetworkManager.NetworkConfig.NetworkTransport != null)
                rtt = NetworkManager.NetworkConfig.NetworkTransport.GetCurrentRtt(OwnerClientId);
            ping.Value = (ushort)System.Math.Min(rtt, 9999UL);
        }

        private void Awake() => avatar = GetComponent<PlayerAvatar>();

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                string playerName = null;
                try { playerName = OnlineSessions.LocalPlayerName?.Invoke(); }
                catch (System.Exception exception) { Debug.LogWarning($"[PlayerRunStats] {exception.Message}"); }
                if (!string.IsNullOrWhiteSpace(playerName))
                    SetNameRpc(Sanitize(playerName));
            }
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

        [Rpc(SendTo.Server)]
        private void SetNameRpc(FixedString64Bytes name, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId)
                displayName.Value = name;
        }

        /// <summary>Strips rich text markers and truncates to what a FixedString64Bytes holds (UTF-8).</summary>
        private static FixedString64Bytes Sanitize(string name)
        {
            name = name.Trim().Replace('<', '‹').Replace('>', '›');
            var builder = new System.Text.StringBuilder();
            int bytes = 0;
            foreach (char c in name)
            {
                int size = char.IsSurrogate(c) ? 2 : System.Text.Encoding.UTF8.GetByteCount(c.ToString());
                if (bytes + size > 60)
                    break;
                bytes += size;
                builder.Append(c);
            }
            // Drop a dangling high surrogate left by the cut.
            if (builder.Length > 0 && char.IsHighSurrogate(builder[builder.Length - 1]))
                builder.Length--;
            return new FixedString64Bytes(builder.ToString());
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
