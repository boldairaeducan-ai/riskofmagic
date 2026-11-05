using System;
using RiskOfMagic.Core;
using RiskOfMagic.Player;
using RiskOfMagic.Run;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Flow
{
    public struct LobbyPlayer : INetworkSerializeByMemcpy, IEquatable<LobbyPlayer>
    {
        public ulong ClientId;
        public int Character;
        public bool Ready;
        /// <summary>Skill alternatives (bit per slot).</summary>
        public byte Loadout;

        public bool Equals(LobbyPlayer other) => ClientId == other.ClientId && Character == other.Character && Ready == other.Ready && Loadout == other.Loadout;
        public override bool Equals(object obj) => obj is LobbyPlayer other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ClientId, Character, Ready, Loadout);
    }

    /// <summary>
    /// Replicated pre-game lobby (spawned by the host in the main menu): each player's character and ready
    /// state, and the difficulty chosen by the host. Starting copies the choices to RunSettings.
    /// </summary>
    public class LobbyState : NetworkBehaviour
    {
        private NetworkList<LobbyPlayer> players;
        private readonly NetworkVariable<DifficultyLevel> difficulty = new(DifficultyLevel.Normal);

        public static LobbyState Instance { get; private set; }

        /// <summary>Every peer.</summary>
        public event Action Changed;

        public int PlayerCount => players.Count;
        public LobbyPlayer GetPlayer(int index) => players[index];
        public DifficultyLevel Difficulty => difficulty.Value;

        /// <summary>Every guest is ready (the host starts the game, which counts as ready).</summary>
        public bool AllGuestsReady
        {
            get
            {
                foreach (var player in players)
                    if (player.ClientId != NetworkManager.ServerClientId && !player.Ready)
                        return false;
                return true;
            }
        }

        public bool TryGetLocalPlayer(out LobbyPlayer player)
        {
            foreach (var entry in players)
            {
                if (entry.ClientId != NetworkManager.LocalClientId)
                    continue;
                player = entry;
                return true;
            }
            player = default;
            return false;
        }

        private void Awake() => players = new NetworkList<LobbyPlayer>();

        public override void OnNetworkSpawn()
        {
            Instance = this;
            players.OnListChanged += OnPlayersChanged;
            difficulty.OnValueChanged += OnDifficultyChanged;

            if (IsServer)
            {
                foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
                    AddPlayer(clientId);
                NetworkManager.OnClientConnectedCallback += AddPlayer;
                NetworkManager.OnClientDisconnectCallback += RemovePlayer;
            }
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            players.OnListChanged -= OnPlayersChanged;
            difficulty.OnValueChanged -= OnDifficultyChanged;
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= AddPlayer;
                NetworkManager.OnClientDisconnectCallback -= RemovePlayer;
            }
            if (Instance == this)
                Instance = null;
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- Requests (any peer)

        public void SelectCharacter(int characterIndex)
        {
            PlayerAvatar.PreferredCharacterIndex = characterIndex;
            SetCharacterRpc(characterIndex);
        }

        public void SetReady(bool ready) => SetReadyRpc(ready);

        /// <summary>Local player: chooses the skill alternatives (from their profile unlocks).</summary>
        public void SetLoadout(byte mask) => SetLoadoutRpc(mask);

        [Rpc(SendTo.Server)]
        private void SetLoadoutRpc(byte mask, RpcParams rpcParams = default)
            => Mutate(rpcParams.Receive.SenderClientId, (ref LobbyPlayer p) => p.Loadout = mask);

        /// <summary>Host only.</summary>
        public void SetDifficulty(DifficultyLevel level)
        {
            if (IsServer)
                difficulty.Value = level;
        }

        /// <summary>Host only: copies the choices to RunSettings and loads the game scene for everyone.</summary>
        public bool TryStartGame()
        {
            if (!IsServer || !AllGuestsReady)
                return false;
            RunSettings.Difficulty = difficulty.Value;
            foreach (var player in players)
            {
                RunSettings.SetCharacter(player.ClientId, player.Character);
                RunSettings.SetLoadout(player.ClientId, player.Loadout);
            }
            GameFlow.StartGame();
            return true;
        }

        [Rpc(SendTo.Server)]
        private void SetCharacterRpc(int characterIndex, RpcParams rpcParams = default)
        {
            if (GameCatalog.Instance == null || GameCatalog.Instance.GetCharacter(characterIndex) == null)
                return;
            Mutate(rpcParams.Receive.SenderClientId, (ref LobbyPlayer p) => p.Character = characterIndex);
        }

        [Rpc(SendTo.Server)]
        private void SetReadyRpc(bool ready, RpcParams rpcParams = default)
            => Mutate(rpcParams.Receive.SenderClientId, (ref LobbyPlayer p) => p.Ready = ready);

        // ---------------------------------------------------------------- Server

        private delegate void Mutation(ref LobbyPlayer player);

        private void Mutate(ulong clientId, Mutation mutation)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].ClientId != clientId)
                    continue;
                var player = players[i];
                mutation(ref player);
                players[i] = player;
                return;
            }
        }

        private void AddPlayer(ulong clientId)
        {
            foreach (var player in players)
                if (player.ClientId == clientId)
                    return;
            players.Add(new LobbyPlayer { ClientId = clientId, Character = 0, Ready = false });
        }

        private void RemovePlayer(ulong clientId)
        {
            for (int i = players.Count - 1; i >= 0; i--)
                if (players[i].ClientId == clientId)
                    players.RemoveAt(i);
        }

        private void OnPlayersChanged(NetworkListEvent<LobbyPlayer> change) => Changed?.Invoke();
        private void OnDifficultyChanged(DifficultyLevel previous, DifficultyLevel current) => Changed?.Invoke();
    }
}
