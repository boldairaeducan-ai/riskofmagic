using RiskOfMagic.Core;
using RiskOfMagic.Player;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace RiskOfMagic.Networking
{
    /// <summary>Prototype-only connection panel (Host / Client / Server) and character picker.</summary>
    [RequireComponent(typeof(NetworkManager))]
    public class NetworkDebugPanel : MonoBehaviour
    {
        [SerializeField] private string address = "127.0.0.1";
        [SerializeField] private ushort port = 7777;

        private NetworkManager manager;

        private void Awake() => manager = GetComponent<NetworkManager>();

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 260, 300), GUI.skin.box);

            if (!manager.IsClient && !manager.IsServer)
                DrawConnect();
            else
                DrawStatus();

            GUILayout.EndArea();
        }

        private void DrawConnect()
        {
            DrawCharacterPicker();

            GUILayout.Label("Adresse");
            address = GUILayout.TextField(address);
            GUILayout.Label("Port");
            if (ushort.TryParse(GUILayout.TextField(port.ToString()), out var parsed))
                port = parsed;

            if (GUILayout.Button("Host")) { ApplyTransport(); manager.StartHost(); }
            if (GUILayout.Button("Client")) { ApplyTransport(); manager.StartClient(); }
            if (GUILayout.Button("Server")) { ApplyTransport(); manager.StartServer(); }
        }

        private void DrawStatus()
        {
            string mode = manager.IsHost ? "Host" : manager.IsServer ? "Server" : "Client";
            GUILayout.Label(manager.IsServer ? $"{mode} - clients : {manager.ConnectedClientsIds.Count}" : mode);
            GUILayout.Label("Échap : libérer le curseur");

            if (PlayerAvatar.Local != null && !PlayerAvatar.Local.Look.IsCursorLocked)
                DrawCharacterPicker();

            if (GUILayout.Button("Déconnexion"))
                manager.Shutdown();
        }

        private void DrawCharacterPicker()
        {
            var catalog = GameCatalog.Instance;
            if (catalog == null)
                return;

            GUILayout.Label("Personnage");
            var characters = catalog.Characters;
            for (int i = 0; i < characters.Count; i++)
            {
                bool selected = PlayerAvatar.PreferredCharacterIndex == i;
                if (GUILayout.Toggle(selected, characters[i].displayName, GUI.skin.button) && !selected)
                {
                    PlayerAvatar.PreferredCharacterIndex = i;
                    if (PlayerAvatar.Local != null)
                        PlayerAvatar.Local.RequestCharacter(i);
                }
            }
        }

        private void ApplyTransport()
        {
            if (manager.NetworkConfig.NetworkTransport is UnityTransport transport)
                transport.SetConnectionData(address, port);
        }
    }
}
