using RiskOfMagic.Player;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace RiskOfMagic.Networking
{
    /// <summary>Prototype-only connection panel (Host / Client / Server) plus local HP readout.</summary>
    [RequireComponent(typeof(NetworkManager))]
    public class NetworkDebugPanel : MonoBehaviour
    {
        [SerializeField] private string address = "127.0.0.1";
        [SerializeField] private ushort port = 7777;

        private NetworkManager manager;

        private void Awake() => manager = GetComponent<NetworkManager>();

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 260, 220), GUI.skin.box);

            if (!manager.IsClient && !manager.IsServer)
                DrawConnect();
            else
                DrawStatus();

            GUILayout.EndArea();
        }

        private void DrawConnect()
        {
            GUILayout.Label("Address");
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
            GUILayout.Label(manager.IsServer ? $"{mode} - clients: {manager.ConnectedClientsIds.Count}" : mode);

            var local = PlayerAvatar.Local;
            if (local != null)
                GUILayout.Label($"HP: {local.Health.Current:0} / {local.Health.Max:0}");
            GUILayout.Label("Esc: toggle cursor");

            if (GUILayout.Button("Disconnect"))
                manager.Shutdown();
        }

        private void ApplyTransport()
        {
            if (manager.NetworkConfig.NetworkTransport is UnityTransport transport)
                transport.SetConnectionData(address, port);
        }
    }
}
