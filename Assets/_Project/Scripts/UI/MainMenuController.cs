using System;
using System.Collections.Generic;
using System.Text;
using RiskOfMagic.Core;
using RiskOfMagic.Flow;
using RiskOfMagic.Networking;
using RiskOfMagic.Run;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Main menu: Solo / Create online game (join code) / Join by code, then the shared lobby
    /// (character, ready, difficulty for the host, start).
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private HudTheme theme;
        [SerializeField] private LobbyState lobbyPrefab;
        [SerializeField, Range(1, 4)] private int maxPlayers = 4;

        [Header("Home")]
        [SerializeField] private GameObject homeScreen;
        [SerializeField] private UnityEngine.UI.Button soloButton;
        [SerializeField] private UnityEngine.UI.Button hostButton;
        [SerializeField] private TMP_InputField joinCodeField;
        [SerializeField] private UnityEngine.UI.Button joinButton;
        [SerializeField] private UnityEngine.UI.Button optionsButton;
        [SerializeField] private UnityEngine.UI.Button quitButton;
        [SerializeField] private OptionsMenu optionsMenu;
        [SerializeField] private TMP_Text statusLabel;

        [Header("Lobby")]
        [SerializeField] private GameObject lobbyScreen;
        [SerializeField] private TMP_Text codeLabel;
        [SerializeField] private UnityEngine.UI.Button copyCodeButton;
        [SerializeField] private TMP_Text playersLabel;
        [SerializeField] private UnityEngine.UI.Button characterButtonTemplate;
        [SerializeField] private TMP_Text characterDescription;
        [SerializeField] private UnityEngine.UI.Button normalButton;
        [SerializeField] private UnityEngine.UI.Button hardButton;
        [SerializeField] private TMP_Text difficultyDescription;
        [SerializeField] private UnityEngine.UI.Button mainButton;
        [SerializeField] private TMP_Text mainButtonLabel;
        [SerializeField] private UnityEngine.UI.Button leaveButton;

        private readonly List<UnityEngine.UI.Button> characterButtons = new();
        private LobbyState subscribedLobby;
        private bool busy;

        private static readonly string NormalDescription = "La difficulté augmente avec le temps et les étapes.";
        private static readonly string HardDescription =
            "Montée en difficulté 50% plus rapide, plus d'ennemis, détection plus large, variété d'ennemis débloquée plus tôt.";

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            AddListener(soloButton, StartSolo);
            AddListener(hostButton, () => _ = HostOnline());
            AddListener(joinButton, () => _ = JoinOnline());
            AddListener(quitButton, Application.Quit);
            AddListener(optionsButton, OpenOptions);
            if (optionsMenu != null)
                optionsMenu.gameObject.SetActive(false);
            AddListener(copyCodeButton, () => GUIUtility.systemCopyBuffer = OnlineSessions.Service?.JoinCode ?? string.Empty);
            AddListener(normalButton, () => SetDifficulty(DifficultyLevel.Normal));
            AddListener(hardButton, () => SetDifficulty(DifficultyLevel.Hard));
            AddListener(mainButton, OnMainButton);
            AddListener(leaveButton, () => _ = Leave());

            BuildCharacterButtons();
            SetStatus(GameFlow.PendingMessage ?? (OnlineSessions.IsAvailable ? string.Empty
                : "Jeu en ligne indisponible : installez le package Multiplayer Services."));
            GameFlow.PendingMessage = null;
            if (hostButton != null)
                hostButton.interactable = OnlineSessions.IsAvailable;
            if (joinButton != null)
                joinButton.interactable = OnlineSessions.IsAvailable;

            ShowLobby(NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);
        }

        private void OnDestroy() => Subscribe(null);

        // ---------------------------------------------------------------- Home

        private void StartSolo()
        {
            var manager = NetworkManager.Singleton;
            if (busy || manager == null || manager.IsListening)
                return;
            if (manager.StartHost())
            {
                GameFlow.LaunchedFromMenu = true;
                OnHosting();
            }
            else
                SetStatus("Impossible de démarrer la partie solo.");
        }

        private async System.Threading.Tasks.Task HostOnline()
        {
            if (busy || OnlineSessions.Service == null)
                return;
            SetBusy(true, "Création de la partie en ligne...");
            try
            {
                await OnlineSessions.Service.HostAsync(maxPlayers);
                GameFlow.LaunchedFromMenu = true;
                OnHosting();
                SetStatus(string.Empty);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus($"Échec de la création : {exception.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async System.Threading.Tasks.Task JoinOnline()
        {
            string code = joinCodeField != null ? joinCodeField.text.Trim() : string.Empty;
            if (busy || OnlineSessions.Service == null || string.IsNullOrEmpty(code))
                return;
            SetBusy(true, "Connexion...");
            try
            {
                await OnlineSessions.Service.JoinAsync(code);
                GameFlow.LaunchedFromMenu = true;
                ShowLobby(true);
                SetStatus(string.Empty);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus($"Impossible de rejoindre : {exception.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void OpenOptions()
        {
            if (optionsMenu == null)
                return;
            homeScreen.SetActive(false);
            optionsMenu.Open(() => homeScreen.SetActive(true));
        }

        private void OnHosting()
        {
            if (lobbyPrefab != null && LobbyState.Instance == null)
                Instantiate(lobbyPrefab).NetworkObject.Spawn(true);
            ShowLobby(true);
        }

        private async System.Threading.Tasks.Task Leave()
        {
            await GameFlow.LeaveAsync();
            ShowLobby(false);
        }

        // ---------------------------------------------------------------- Lobby

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
        }

        private void SelectCharacter(int index)
        {
            if (LobbyState.Instance != null)
                LobbyState.Instance.SelectCharacter(index);
        }

        private void SetDifficulty(DifficultyLevel level)
        {
            if (LobbyState.Instance != null)
                LobbyState.Instance.SetDifficulty(level);
        }

        private void OnMainButton()
        {
            var lobby = LobbyState.Instance;
            if (lobby == null)
                return;
            if (lobby.IsServer)
                lobby.TryStartGame();
            else if (lobby.TryGetLocalPlayer(out var me))
                lobby.SetReady(!me.Ready);
        }

        private void Update()
        {
            if (LobbyState.Instance != subscribedLobby)
                Subscribe(LobbyState.Instance);

            // Host left / connection lost while in the lobby.
            bool listening = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            if (!busy && !listening && lobbyScreen != null && lobbyScreen.activeSelf)
            {
                ShowLobby(false);
                SetStatus("La partie a été fermée.");
            }
        }

        private void Subscribe(LobbyState lobby)
        {
            if (subscribedLobby != null)
                subscribedLobby.Changed -= RefreshLobby;
            subscribedLobby = lobby;
            if (subscribedLobby != null)
                subscribedLobby.Changed += RefreshLobby;
            RefreshLobby();
        }

        private void RefreshLobby()
        {
            var lobby = subscribedLobby;
            bool hasLobby = lobby != null && lobby.IsSpawned;
            var catalog = GameCatalog.Instance;

            if (codeLabel != null)
            {
                string code = OnlineSessions.Service?.JoinCode;
                codeLabel.text = string.IsNullOrEmpty(code) ? "Partie locale" : $"Code de la partie : <b>{code}</b>";
            }
            if (copyCodeButton != null)
                copyCodeButton.gameObject.SetActive(!string.IsNullOrEmpty(OnlineSessions.Service?.JoinCode));

            if (!hasLobby)
            {
                if (playersLabel != null)
                    playersLabel.text = "En attente du salon...";
                SetInteractable(mainButton, false);
                return;
            }

            // Players
            var builder = new StringBuilder();
            for (int i = 0; i < lobby.PlayerCount; i++)
            {
                var player = lobby.GetPlayer(i);
                bool isHost = player.ClientId == NetworkManager.ServerClientId;
                bool isMe = player.ClientId == NetworkManager.Singleton.LocalClientId;
                string character = catalog != null && catalog.GetCharacter(player.Character) != null ? catalog.GetCharacter(player.Character).displayName : "?";
                string state = isHost ? "<color=#E8C060>Hôte</color>" : player.Ready ? "<color=#70D070>Prêt</color>" : "<color=#A0A0A0>Pas prêt</color>";
                builder.AppendLine($"Joueur {i + 1}{(isMe ? " (vous)" : string.Empty)} · {character} · {state}");
            }
            if (playersLabel != null)
                playersLabel.text = builder.ToString();

            // Character
            bool hasMe = lobby.TryGetLocalPlayer(out var me);
            for (int i = 0; i < characterButtons.Count; i++)
                Tint(characterButtons[i], hasMe && me.Character == i);
            if (characterDescription != null && catalog != null && hasMe && catalog.GetCharacter(me.Character) != null)
                characterDescription.text = DescribeCharacter(me.Character);

            // Difficulty
            bool isServer = lobby.IsServer;
            Tint(normalButton, lobby.Difficulty == DifficultyLevel.Normal);
            Tint(hardButton, lobby.Difficulty == DifficultyLevel.Hard);
            SetInteractable(normalButton, isServer);
            SetInteractable(hardButton, isServer);
            if (difficultyDescription != null)
                difficultyDescription.text = (lobby.Difficulty == DifficultyLevel.Hard ? HardDescription : NormalDescription)
                                             + (isServer ? string.Empty : "\n<size=80%><i>Choisie par l'hôte.</i></size>");

            // Start / ready
            if (isServer)
            {
                SetInteractable(mainButton, lobby.AllGuestsReady);
                if (mainButtonLabel != null)
                    mainButtonLabel.text = lobby.AllGuestsReady ? "Lancer la partie" : "En attente des joueurs...";
            }
            else
            {
                SetInteractable(mainButton, hasMe);
                if (mainButtonLabel != null)
                    mainButtonLabel.text = hasMe && me.Ready ? "Annuler « Prêt »" : "Prêt";
            }
        }

        private static string DescribeCharacter(int index)
        {
            var character = GameCatalog.Instance.GetCharacter(index);
            var builder = new StringBuilder();
            builder.AppendLine($"<b>{character.displayName}</b> · {character.maxHealth:0} PV");
            builder.AppendLine($"<i>{character.description}</i>");
            string[] keys = { "Clic G", "Clic D", "Maj", "R" };
            for (int slot = 0; slot < 4; slot++)
            {
                var skill = character.GetSkill((SkillSlot)slot);
                if (skill != null)
                    builder.AppendLine($"<b>{keys[slot]}</b> {skill.displayName} : {skill.description}");
            }
            return builder.ToString();
        }

        // ---------------------------------------------------------------- Helpers

        private void ShowLobby(bool lobby)
        {
            if (homeScreen != null)
                homeScreen.SetActive(!lobby);
            if (lobbyScreen != null)
                lobbyScreen.SetActive(lobby);
            RefreshLobby();
        }

        private void SetBusy(bool value, string message = null)
        {
            busy = value;
            if (message != null)
                SetStatus(message);
            foreach (var button in new[] { soloButton, hostButton, joinButton })
                if (button != null)
                    button.interactable = !value && (button == soloButton || OnlineSessions.IsAvailable);
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
                statusLabel.text = message ?? string.Empty;
        }

        private void Tint(UnityEngine.UI.Button button, bool selected)
        {
            if (button != null && theme != null && button.targetGraphic != null)
                button.targetGraphic.color = selected ? theme.buttonSelectedColor : theme.buttonColor;
        }

        private static void SetInteractable(UnityEngine.UI.Button button, bool value)
        {
            if (button != null)
                button.interactable = value;
        }

        private static void AddListener(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }
    }
}
