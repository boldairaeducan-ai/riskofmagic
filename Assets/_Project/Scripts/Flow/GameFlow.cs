using System.Threading.Tasks;
using RiskOfMagic.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RiskOfMagic.Flow
{
    /// <summary>Scene names and transitions between the main menu and the game.</summary>
    public static class GameFlow
    {
        public const string MainMenuScene = "MainMenu";
        public const string GameScene = "Prototype";

        /// <summary>Shown once by the main menu (e.g. "connection lost").</summary>
        public static string PendingMessage { get; set; }

        /// <summary>True while we are disconnecting on purpose (no "connection lost" message).</summary>
        public static bool IsLeaving { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            PendingMessage = null;
            IsLeaving = false;
        }

        /// <summary>Server only: load the game scene for every connected player.</summary>
        public static void StartGame()
        {
            var manager = NetworkManager.Singleton;
            if (manager != null && manager.IsServer)
                manager.SceneManager.LoadScene(GameScene, LoadSceneMode.Single);
        }

        /// <summary>Leaves the online game, stops Netcode and goes back to the main menu (if it is in the build).</summary>
        public static async void ReturnToMainMenu(string message = null)
        {
            if (IsLeaving)
                return;
            IsLeaving = true;
            PendingMessage = message;
            try
            {
                await LeaveAsync();
                if (Application.CanStreamedLevelBeLoaded(MainMenuScene) && SceneManager.GetActiveScene().name != MainMenuScene)
                    SceneManager.LoadScene(MainMenuScene);
            }
            finally
            {
                IsLeaving = false;
            }
        }

        /// <summary>Leaves the online game and stops Netcode, staying in the current scene.</summary>
        public static async Task LeaveAsync()
        {
            bool wasLeaving = IsLeaving;
            IsLeaving = true;
            try
            {
                if (OnlineSessions.Service != null)
                    await OnlineSessions.Service.LeaveAsync();
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                    NetworkManager.Singleton.Shutdown();
                RunSettings.Reset();
            }
            finally
            {
                IsLeaving = wasLeaving;
            }
        }
    }
}
