using System;
using RiskOfMagic.Networking;
using Steamworks;
using UnityEngine;

namespace RiskOfMagic.Steam
{
    /// <summary>
    /// Starts the Steam API (Steam must be running; steam_appid.txt next to the executable outside of Steam),
    /// pumps its callbacks, registers the Steam online backend, and forwards invites / "+connect_lobby".
    /// </summary>
    public sealed class SteamBootstrap : MonoBehaviour
    {
        public static bool IsReady { get; private set; }

        private Callback<GameLobbyJoinRequested_t> joinRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            IsReady = false;
            if (!Packsize.Test() || !DllCheck.Test())
            {
                Debug.LogWarning("[Steam] Steamworks.NET native library mismatch: Steam disabled.");
                return;
            }
            bool ok;
            try
            {
                ok = SteamAPI.Init();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Steam] {exception.Message}");
                ok = false;
            }
            if (!ok)
            {
                Debug.LogWarning("[Steam] SteamAPI.Init failed (is Steam running? steam_appid.txt present?). Steam online play disabled.");
                return;
            }

            IsReady = true;
            SteamNetworkingUtils.InitRelayNetworkAccess();
            var go = new GameObject("SteamBootstrap");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<SteamBootstrap>();
            OnlineSessions.Register(new SteamSessionService(), 10);
            Debug.Log($"[Steam] Ready as {SteamFriends.GetPersonaName()}.");

            // Started from a Steam invite: "+connect_lobby <id>".
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out ulong lobby))
                    OnlineSessions.PendingJoinCode = SteamSessionService.Encode(lobby);
        }

        private void Awake()
        {
            // Invite accepted from the Steam overlay while the game runs.
            joinRequested = Callback<GameLobbyJoinRequested_t>.Create(request =>
                OnlineSessions.RequestJoin(SteamSessionService.Encode(request.m_steamIDLobby.m_SteamID)));
        }

        private void Update()
        {
            if (IsReady)
                SteamAPI.RunCallbacks();
        }

        private void OnApplicationQuit()
        {
            if (!IsReady)
                return;
            IsReady = false;
            SteamAPI.Shutdown();
        }
    }
}
