using System;
using System.Threading.Tasks;
using RiskOfMagic.Networking;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace RiskOfMagic.Online
{
    /// <summary>
    /// Join-code games with brokered connectivity (no port forwarding). Creating/joining also starts
    /// Netcode as host/client. Requires the project to be linked to a Unity Cloud project.
    /// </summary>
    internal sealed class MultiplayerSessionService : IOnlineSessionService
    {
        private ISession session;

        public string JoinCode => session?.Code;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => OnlineSessions.Service = new MultiplayerSessionService();

        public async Task<string> HostAsync(int maxPlayers)
        {
            await EnsureSignedInAsync();
            var options = new SessionOptions { MaxPlayers = maxPlayers, IsPrivate = true }.WithRelayNetwork();
            session = await MultiplayerService.Instance.CreateSessionAsync(options);
            return session.Code;
        }

        public async Task JoinAsync(string code)
        {
            await EnsureSignedInAsync();
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
        }

        public async Task LeaveAsync()
        {
            var current = session;
            session = null;
            if (current == null)
                return;
            try
            {
                await current.LeaveAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Online] Leave failed: {exception.Message}");
            }
        }

        private static async Task EnsureSignedInAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var options = new InitializationOptions();
#if UNITY_EDITOR
                // Several editor instances (Multiplayer Play Mode) must not share the same anonymous account.
                options.SetProfile("editor" + UnityEngine.Random.Range(0, 1000000));
#endif
                await UnityServices.InitializeAsync(options);
            }
            while (UnityServices.State == ServicesInitializationState.Initializing)
                await Task.Yield();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }
}
