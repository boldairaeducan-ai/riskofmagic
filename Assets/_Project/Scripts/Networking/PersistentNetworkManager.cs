using RiskOfMagic.Flow;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RiskOfMagic.Networking
{
    /// <summary>
    /// Keeps a single NetworkManager alive across scenes (menu → game → menu). A second one, from a scene
    /// that also contains a NetworkManager for direct testing, destroys itself before it initializes.
    /// Also sends clients back to the menu when the connection drops.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [RequireComponent(typeof(NetworkManager))]
    public class PersistentNetworkManager : MonoBehaviour
    {
        private static PersistentNetworkManager instance;
        private NetworkManager manager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            manager = GetComponent<NetworkManager>();
            manager.OnClientStopped += OnClientStopped;
        }

        private void OnDestroy()
        {
            if (instance != this)
                return;
            instance = null;
            if (manager != null)
                manager.OnClientStopped -= OnClientStopped;
        }

        private void OnClientStopped(bool wasHost)
        {
            if (GameFlow.IsLeaving || SceneManager.GetActiveScene().name == GameFlow.MainMenuScene)
                return;
            GameFlow.ReturnToMainMenu(wasHost ? null : "Connexion à l'hôte perdue.");
        }
    }
}
