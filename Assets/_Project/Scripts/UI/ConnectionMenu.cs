using System.Collections.Generic;
using RiskOfMagic.Core;
using RiskOfMagic.Player;
using RiskOfMagic.Run;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Connection + character selection panel. Visible while disconnected, or connected with the cursor
    /// unlocked (Esc). Character buttons are cloned from an inactive template.
    /// </summary>
    public class ConnectionMenu : MonoBehaviour
    {
        [SerializeField] private HudTheme theme;
        [SerializeField] private CanvasGroup group;

        [Header("Connection")]
        [SerializeField] private GameObject connectSection;
        [SerializeField] private TMP_InputField addressField;
        [SerializeField] private TMP_InputField portField;
        [SerializeField] private UnityEngine.UI.Button hostButton;
        [SerializeField] private UnityEngine.UI.Button clientButton;
        [SerializeField] private UnityEngine.UI.Button serverButton;

        [Header("Session")]
        [SerializeField] private GameObject sessionSection;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private UnityEngine.UI.Button forceWaveButton;
        [SerializeField] private UnityEngine.UI.Button disconnectButton;

        [Header("Characters")]
        [SerializeField] private UnityEngine.UI.Button characterButtonTemplate;

        [SerializeField] private string defaultAddress = "127.0.0.1";
        [SerializeField] private ushort defaultPort = 7777;

        private readonly List<UnityEngine.UI.Button> characterButtons = new();

        private void Start()
        {
            if (addressField != null)
                addressField.text = defaultAddress;
            if (portField != null)
                portField.text = defaultPort.ToString();

            AddListener(hostButton, () => Connect(m => m.StartHost()));
            AddListener(clientButton, () => Connect(m => m.StartClient()));
            AddListener(serverButton, () => Connect(m => m.StartServer()));
            AddListener(disconnectButton, Disconnect);
            AddListener(forceWaveButton, ForceWave);

            BuildCharacterButtons();
        }

        private static void AddListener(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        private static void Disconnect()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.Shutdown();
        }

        private void BuildCharacterButtons()
        {
            var catalog = GameCatalog.Instance;
            if (catalog == null || characterButtonTemplate == null)
                return;

            characterButtonTemplate.gameObject.SetActive(false);
            for (int i = 0; i < catalog.Characters.Count; i++)
            {
                int index = i;
                var button = Instantiate(characterButtonTemplate, characterButtonTemplate.transform.parent);
                button.gameObject.SetActive(true);
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = catalog.Characters[i].displayName;
                button.onClick.AddListener(() => SelectCharacter(index));
                characterButtons.Add(button);
            }
            RefreshCharacterButtons();
        }

        private void SelectCharacter(int index)
        {
            PlayerAvatar.PreferredCharacterIndex = index;
            if (PlayerAvatar.Local != null)
                PlayerAvatar.Local.RequestCharacter(index);
            RefreshCharacterButtons();
        }

        private void RefreshCharacterButtons()
        {
            if (theme == null)
                return;
            for (int i = 0; i < characterButtons.Count; i++)
            {
                var image = characterButtons[i].targetGraphic;
                if (image != null)
                    image.color = i == PlayerAvatar.PreferredCharacterIndex ? theme.buttonSelectedColor : theme.buttonColor;
            }
        }

        private void Connect(System.Action<NetworkManager> start)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || manager.IsListening)
                return;
            if (manager.NetworkConfig.NetworkTransport is UnityTransport transport)
            {
                string address = addressField != null && !string.IsNullOrWhiteSpace(addressField.text) ? addressField.text.Trim() : defaultAddress;
                ushort port = portField != null && ushort.TryParse(portField.text, out var parsed) ? parsed : defaultPort;
                transport.SetConnectionData(address, port);
            }
            start(manager);
        }

        private void ForceWave()
        {
            var director = FindFirstObjectByType<CombatDirector>();
            if (director != null)
                director.ForceWave();
        }

        private void Update()
        {
            var manager = NetworkManager.Singleton;
            bool connected = manager != null && manager.IsListening;
            bool cursorFree = Cursor.lockState != CursorLockMode.Locked;
            bool visible = !connected || cursorFree;

            if (group != null)
            {
                group.alpha = visible ? 1f : 0f;
                group.interactable = visible;
                group.blocksRaycasts = visible;
            }
            if (!visible)
                return;

            if (connectSection != null)
                connectSection.SetActive(!connected);
            if (sessionSection != null)
                sessionSection.SetActive(connected);
            if (forceWaveButton != null)
                forceWaveButton.gameObject.SetActive(connected && manager.IsServer);
            if (connected && statusLabel != null)
            {
                string mode = manager.IsHost ? "Hôte" : manager.IsServer ? "Serveur" : "Client";
                statusLabel.text = manager.IsServer ? $"{mode} · {manager.ConnectedClientsIds.Count} joueur(s)\nÉchap : reprendre" : $"{mode}\nÉchap : reprendre";
            }
        }
    }
}
