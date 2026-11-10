using System;
using System.Threading.Tasks;
using Netcode.Transports;
using RiskOfMagic.Networking;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Steam
{
    /// <summary>
    /// Online games through Steam: a Steam lobby (its id, in base 36, is the join code; friends can also join from
    /// the Steam overlay) and Steam's relayed peer-to-peer sockets (no port forwarding, free).
    /// The NetworkManager switches to the Steam transport while in a Steam game, then back to the local one.
    /// </summary>
    internal sealed class SteamSessionService : IOnlineSessionService
    {
        private const string HostKey = "rom_host";
        private const string VersionKey = "rom_version";
        private const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        private readonly CallResult<LobbyCreated_t> lobbyCreated = new();
        private readonly CallResult<LobbyEnter_t> lobbyEntered = new();
        private CSteamID lobby = CSteamID.Nil;
        private NetworkTransport localTransport;

        public string JoinCode => lobby.IsValid() ? Encode(lobby.m_SteamID) : null;
        public string BackendName => "Steam";

        public async Task<string> HostAsync(int maxPlayers)
        {
            var result = await Call(lobbyCreated, SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, Mathf.Clamp(maxPlayers, 1, 4)));
            if (result.m_eResult != EResult.k_EResultOK)
                throw new InvalidOperationException($"Création du salon Steam refusée ({result.m_eResult}).");
            lobby = new CSteamID(result.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(lobby, HostKey, SteamUser.GetSteamID().m_SteamID.ToString());
            SteamMatchmaking.SetLobbyData(lobby, VersionKey, Application.version);

            UseSteamTransport(0);
            if (!NetworkManager.Singleton.StartHost())
            {
                await LeaveAsync();
                throw new InvalidOperationException("Impossible de démarrer l'hôte Steam.");
            }
            OnlineSessions.InviteFriends = () => SteamFriends.ActivateGameOverlayInviteDialog(lobby);
            return JoinCode;
        }

        public async Task JoinAsync(string code)
        {
            if (!TryDecode(code, out ulong id))
                throw new ArgumentException("Code de partie invalide.");
            var result = await Call(lobbyEntered, SteamMatchmaking.JoinLobby(new CSteamID(id)));
            if (result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                throw new InvalidOperationException("Partie introuvable, pleine ou fermée.");
            lobby = new CSteamID(result.m_ulSteamIDLobby);

            string version = SteamMatchmaking.GetLobbyData(lobby, VersionKey);
            if (!string.IsNullOrEmpty(version) && version != Application.version)
            {
                await LeaveAsync();
                throw new InvalidOperationException($"Versions différentes (hôte {version}, vous {Application.version}).");
            }
            if (!ulong.TryParse(SteamMatchmaking.GetLobbyData(lobby, HostKey), out ulong host))
                host = SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID;

            UseSteamTransport(host);
            if (!NetworkManager.Singleton.StartClient())
            {
                await LeaveAsync();
                throw new InvalidOperationException("Impossible de se connecter à l'hôte Steam.");
            }
            OnlineSessions.InviteFriends = () => SteamFriends.ActivateGameOverlayInviteDialog(lobby);
        }

        public Task LeaveAsync()
        {
            if (lobby.IsValid())
                SteamMatchmaking.LeaveLobby(lobby);
            lobby = CSteamID.Nil;
            OnlineSessions.InviteFriends = null;
            return Task.CompletedTask;
        }

        // ---------------------------------------------------------------- Transport

        private void UseSteamTransport(ulong connectTo)
        {
            var manager = NetworkManager.Singleton;
            var steam = manager.GetComponent<SteamNetworkingSocketsTransport>();
            if (steam == null)
                steam = manager.gameObject.AddComponent<SteamNetworkingSocketsTransport>();
            if (manager.NetworkConfig.NetworkTransport != steam)
                localTransport = manager.NetworkConfig.NetworkTransport;
            steam.ConnectToSteamID = connectTo;
            manager.NetworkConfig.NetworkTransport = steam;
            manager.OnServerStopped -= OnStopped;
            manager.OnClientStopped -= OnStopped;
            manager.OnServerStopped += OnStopped;
            manager.OnClientStopped += OnStopped;
        }

        /// <summary>Back to the local transport (solo, LAN) once the Steam game is over.</summary>
        private void OnStopped(bool wasHost)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null)
                return;
            manager.OnServerStopped -= OnStopped;
            manager.OnClientStopped -= OnStopped;
            if (localTransport != null)
                manager.NetworkConfig.NetworkTransport = localTransport;
            if (lobby.IsValid())
                _ = LeaveAsync();
        }

        // ---------------------------------------------------------------- Helpers

        private static Task<T> Call<T>(CallResult<T> callResult, SteamAPICall_t handle)
        {
            var completion = new TaskCompletionSource<T>();
            callResult.Set(handle, (result, ioFailure) =>
            {
                if (ioFailure)
                    completion.TrySetException(new InvalidOperationException("Steam ne répond pas."));
                else
                    completion.TrySetResult(result);
            });
            return completion.Task;
        }

        public static string Encode(ulong value)
        {
            if (value == 0)
                return "0";
            var chars = new System.Text.StringBuilder();
            while (value > 0)
            {
                chars.Insert(0, Digits[(int)(value % 36)]);
                value /= 36;
            }
            return chars.ToString();
        }

        private static bool TryDecode(string code, out ulong value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(code))
                return false;
            foreach (char c in code.Trim().ToUpperInvariant())
            {
                int digit = Digits.IndexOf(c);
                if (digit < 0)
                    return false;
                value = value * 36 + (ulong)digit;
            }
            return value != 0;
        }
    }
}
