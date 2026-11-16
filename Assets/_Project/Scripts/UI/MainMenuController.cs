using System;
using System.Collections.Generic;
using System.Text;
using RiskOfMagic.Core;
using RiskOfMagic.Flow;
using RiskOfMagic.Meta;
using RiskOfMagic.Networking;
using RiskOfMagic.Run;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Main menu (UI Toolkit, UI/Menus/MainMenu.uxml): Solo / Create online game (join code) / Join by code, journal,
    /// options, then the shared lobby (character, skills, ready, difficulty for the host, start).
    /// Layout and look live in the UXML / USS; this class only finds named elements and fills them with game data.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private LobbyState lobbyPrefab;
        [SerializeField, Range(1, 4)] private int maxPlayers = 4;
        [SerializeField, Tooltip("Empty = Resources/MenuUI.")] private MenuUiAssets assets;

        private static readonly string NormalDescription = "La difficulté augmente avec le temps et les étapes.";
        private static readonly string HardDescription =
            "Montée en difficulté 50% plus rapide, plus d'ennemis, détection plus large, variété d'ennemis débloquée plus tôt.";

        // Home
        private VisualElement home;
        private Button soloButton, multiplayerButton, hostButton, joinButton, optionsButton, journalButton, quitButton;
        private VisualElement multiplayerPanel;
        private TextField joinCodeField;
        private Label statusLabel, profileLabel;

        // Lobby
        private VisualElement lobbyScreen;
        private Label codeLabel, playersLabel, characterDescription, difficultyDescription;
        private Button copyCodeButton, inviteButton, mainButton, leaveButton;
        private VisualElement characterGrid, skillGrid, difficultyCards, previewElement;
        private LobbyCardView normalCard, hardCard;
        private CharacterPreview characterPreview;

        private OptionsScreen options;
        private JournalScreen journal;

        private readonly List<LobbyCardView> characterCards = new();
        private readonly List<LobbyCardView> skillCards = new(); // index = slot * 2 + (alternative ? 1 : 0)
        private bool sentInitialLoadout;
        private LobbyState subscribedLobby;
        private bool busy;
        private bool built;

        private void Start()
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            if (assets == null)
                assets = MenuUiAssets.Instance;
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null || assets == null)
            {
                Debug.LogError("[Risk of Magic] Main menu: UIDocument or Resources/MenuUI missing. Run Tools → Risk of Magic → Build Prototype Scene.");
                return;
            }
            Bind(root);
            built = true;

            OnlineSessions.JoinRequested += OnJoinRequested;
            SetStatus(GameFlow.PendingMessage ?? (OnlineSessions.IsAvailable ? string.Empty
                : "Jeu en ligne indisponible : lancez Steam (ou installez Multiplayer Services)."));
            GameFlow.PendingMessage = null;
            hostButton?.SetEnabled(OnlineSessions.IsAvailable);
            joinButton?.SetEnabled(OnlineSessions.IsAvailable);

            ShowLobby(NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);
            if (!string.IsNullOrEmpty(OnlineSessions.PendingJoinCode))
                OnJoinRequested(); // started from a Steam invite
        }

        private void OnDestroy()
        {
            Subscribe(null);
            OnlineSessions.JoinRequested -= OnJoinRequested;
            options?.Dispose();
        }

        // ---------------------------------------------------------------- Binding

        private void Bind(VisualElement root)
        {
            ApplyArt(root);
            MenuUi.SetText(root.Q<Label>("versionLabel"), $"v{Application.version}");

            home = MenuUi.Require<VisualElement>(root, "home");
            soloButton = Listen(root, "soloButton", StartSolo);
            multiplayerButton = Listen(root, "multiplayerButton", ToggleMultiplayer);
            journalButton = Listen(root, "journalButton", OpenJournal);
            optionsButton = Listen(root, "optionsButton", OpenOptions);
            quitButton = Listen(root, "quitButton", Application.Quit);
            multiplayerPanel = MenuUi.Require<VisualElement>(root, "multiplayerPanel");
            hostButton = Listen(root, "hostButton", () => _ = HostOnline());
            joinButton = Listen(root, "joinButton", () => _ = JoinOnline());
            joinCodeField = MenuUi.Require<TextField>(root, "joinCodeField");
            statusLabel = MenuUi.Require<Label>(root, "statusLabel");
            profileLabel = MenuUi.Require<Label>(root, "profileLabel");

            lobbyScreen = MenuUi.Require<VisualElement>(root, "lobby");
            codeLabel = MenuUi.Require<Label>(root, "codeLabel");
            copyCodeButton = Listen(root, "copyCodeButton", () => GUIUtility.systemCopyBuffer = OnlineSessions.Service?.JoinCode ?? string.Empty);
            inviteButton = Listen(root, "inviteButton", () => OnlineSessions.InviteFriends?.Invoke());
            playersLabel = MenuUi.Require<Label>(root, "playersLabel");
            characterDescription = MenuUi.Require<Label>(root, "characterDescription");
            difficultyDescription = MenuUi.Require<Label>(root, "difficultyDescription");
            characterGrid = MenuUi.Require<VisualElement>(root, "characterGrid");
            skillGrid = MenuUi.Require<VisualElement>(root, "skillGrid");
            difficultyCards = MenuUi.Require<VisualElement>(root, "difficultyCards");
            previewElement = MenuUi.Require<VisualElement>(root, "characterPreview");
            mainButton = Listen(root, "mainButton", OnMainButton);
            leaveButton = Listen(root, "leaveButton", () => _ = Leave());

            options = new OptionsScreen(MenuUi.Require<VisualElement>(root, "options"), assets);
            journal = new JournalScreen(MenuUi.Require<VisualElement>(root, "journal"));

            normalCard = NewCard(difficultyCards, "card--difficulty", () => SetDifficulty(DifficultyLevel.Normal));
            hardCard = NewCard(difficultyCards, "card--difficulty", () => SetDifficulty(DifficultyLevel.Hard));
            BuildCharacterCards();
            BuildSkillCards();

            var previewGo = new GameObject("CharacterPreview");
            previewGo.transform.SetParent(transform, false);
            characterPreview = previewGo.AddComponent<CharacterPreview>();
            if (previewElement != null && characterPreview.Texture != null)
                previewElement.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(characterPreview.Texture));

            MenuSounds.Register(root);
        }

        private Button Listen(VisualElement root, string name, Action action)
        {
            var button = MenuUi.Require<Button>(root, name);
            if (button != null)
                button.clicked += action;
            return button;
        }

        private LobbyCardView NewCard(VisualElement parent, string modifier, Action onClick)
        {
            var element = MenuUi.Clone(assets.lobbyCard, parent);
            if (element == null)
                return null;
            var card = new LobbyCardView(element, modifier);
            if (card.Root != null)
                card.Root.clicked += onClick;
            return card;
        }

        /// <summary>Steam invite accepted (overlay or launch argument): join right away from the home screen.</summary>
        private void OnJoinRequested()
        {
            string code = OnlineSessions.PendingJoinCode;
            if (string.IsNullOrEmpty(code) || busy || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening))
                return;
            OnlineSessions.PendingJoinCode = null;
            if (joinCodeField != null)
                joinCodeField.value = code;
            _ = JoinOnline();
        }

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
            SetBusy(true, $"Création de la partie en ligne ({OnlineSessions.Service.BackendName})...");
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
                SetStatus($"Échec de la création : {OnlineError(exception)}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async System.Threading.Tasks.Task JoinOnline()
        {
            string code = joinCodeField != null ? (joinCodeField.value ?? string.Empty).Trim() : string.Empty;
            if (busy || OnlineSessions.Service == null || string.IsNullOrEmpty(code))
                return;
            SetBusy(true, $"Connexion ({OnlineSessions.Service.BackendName})...");
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
                SetStatus($"Impossible de rejoindre : {OnlineError(exception)}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        /// <summary>Readable reason for the usual online failures (the full exception stays in the log).</summary>
        private static string OnlineError(Exception exception)
        {
            string text = exception.ToString();
            if (text.Contains("UnityProjectNotLinked") || text.Contains("ICloudProjectId"))
                return "le projet n'est pas relié à Unity Cloud (Project Settings → Services, puis refaire la build).";
            if (text.Contains("Authentication") && text.Contains("Request"))
                return "connexion aux services Unity impossible (réseau ou pare-feu).";
            if (text.Contains("Relay") || text.Contains("Lobby"))
                return "service Relay/Lobby indisponible (vérifier qu'ils sont activés sur le dashboard Unity).";
            if (text.Contains("not found") || text.Contains("NotFound"))
                return "aucune partie avec ce code.";
            return exception.Message;
        }

        private void OpenOptions()
        {
            ShowHome(false);
            options.Open(() => ShowHome(true, optionsButton));
        }

        private void OpenJournal()
        {
            ShowHome(false);
            journal.Open(() => ShowHome(true, journalButton));
        }

        private void ToggleMultiplayer()
        {
            if (multiplayerPanel == null)
                return;
            bool open = !MenuUi.IsShown(multiplayerPanel);
            MenuUi.SetShown(multiplayerPanel, open, animate: true);
            VisualElement focus = !open ? multiplayerButton : hostButton != null && hostButton.enabledSelf ? hostButton : joinCodeField;
            MenuUi.Focus(focus);
        }

        private void ShowHome(bool visible, Button focus = null)
        {
            MenuUi.SetShown(home, visible, animate: true);
            if (!visible)
                return;
            MenuUi.Hide(multiplayerPanel);
            RefreshHome();
            MenuUi.Focus(focus ?? soloButton);
        }

        /// <summary>Profile summary.</summary>
        private void RefreshHome()
        {
            long runs = MetaProgression.GetStat(ChallengeStat.Runs);
            long wins = MetaProgression.GetStat(ChallengeStat.Victories);
            int done = MetaProgression.Profile.completedChallenges.Count;
            int total = MetaProgression.Challenges.Count;
            float best = MetaProgression.Profile.bestVictoryTime;
            string text = runs <= 0
                ? "Première aventure ? Bonne chance."
                : $"{runs} partie{(runs > 1 ? "s" : string.Empty)}   ·   {wins} victoire{(wins > 1 ? "s" : string.Empty)}   ·   défis {done}/{total}";
            if (best > 0f)
                text += $"   ·   record {(int)(best / 60f)}:{(int)(best % 60f):00}";
            MenuUi.SetText(profileLabel, text);
        }

        /// <summary>Optional art from the ArtLibrary: full-screen background, logo replacing the title.</summary>
        private static void ApplyArt(VisualElement root)
        {
            var library = ArtLibrary.Instance;
            var background = root.Q("background");
            var backgroundSprite = library != null ? library.menuBackground : null;
            MenuUi.SetImage(background, backgroundSprite);
            MenuUi.SetShown(background, backgroundSprite != null);

            var logoSprite = library != null ? library.logo : null;
            var logo = root.Q("logo");
            MenuUi.SetImage(logo, logoSprite);
            MenuUi.SetShown(logo, logoSprite != null);
            MenuUi.SetShown(root.Q("gameTitle"), logoSprite == null);
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

        private void BuildCharacterCards()
        {
            var catalog = GameCatalog.Instance;
            if (catalog == null)
                return;
            for (int i = 0; i < catalog.Characters.Count; i++)
            {
                int index = i;
                var card = NewCard(characterGrid, "card--character", () => SelectCharacter(index));
                if (card != null)
                    characterCards.Add(card);
            }
        }

        private void BuildSkillCards()
        {
            for (int slot = 0; slot < 4; slot++)
                for (int variant = 0; variant < 2; variant++)
                {
                    int s = slot;
                    bool alternative = variant == 1;
                    var card = NewCard(skillGrid, "card--skill", () => SelectSkill(s, alternative));
                    if (card != null)
                        skillCards.Add(card);
                }
        }

        /// <summary>"VERROUILLÉ + challenge + progress" for a locked reward, null when available.</summary>
        private static string LockText(ChallengeDefinition challenge)
        {
            if (challenge == null || MetaProgression.IsCompleted(challenge))
                return null;
            return $"<b>VERROUILLÉ</b>\n<size=80%>{challenge.description}\n{MetaProgression.Progress(challenge):N0} / {challenge.target:N0}</size>";
        }

        private static string LockText(UnityEngine.Object reward) => LockText(MetaProgression.UnlockedBy(reward));

        private void SelectCharacter(int index)
        {
            var character = GameCatalog.Instance != null ? GameCatalog.Instance.GetCharacter(index) : null;
            if (LobbyState.Instance == null || character == null || !MetaProgression.IsUnlocked(character))
                return;
            LobbyState.Instance.SelectCharacter(index);
            LobbyState.Instance.SetLoadout(MetaProgression.GetLoadout(character));
        }

        private void SelectSkill(int slot, bool alternative)
        {
            var lobby = LobbyState.Instance;
            if (lobby == null || !lobby.TryGetLocalPlayer(out var me))
                return;
            var character = GameCatalog.Instance.GetCharacter(me.Character);
            var skill = character != null ? (alternative ? character.GetAlternative((SkillSlot)slot) : character.GetSkill((SkillSlot)slot)) : null;
            if (skill == null || !MetaProgression.IsUnlocked(skill))
                return;
            byte bit = (byte)(1 << slot);
            byte mask = alternative ? (byte)(me.Loadout | bit) : (byte)(me.Loadout & ~bit);
            if (mask == me.Loadout)
                return;
            MetaProgression.SetLoadout(character, mask);
            lobby.SetLoadout(mask);
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
            if (!built)
                return;
            if (LobbyState.Instance != subscribedLobby)
                Subscribe(LobbyState.Instance);

            bool screenOpen = options.IsOpen || journal.IsOpen;
            options.Tick(handleBack: true);
            if (!screenOpen)
                HandleBack();
            else if (journal.IsOpen && MenuUi.BackPressed())
                journal.Close();

            // Host left / connection lost while in the lobby.
            bool listening = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            if (!busy && !listening && MenuUi.IsShown(lobbyScreen))
            {
                ShowLobby(false);
                SetStatus("La partie a été fermée.");
            }
        }

        /// <summary>Esc / B on the home screen: folds the multiplayer panel.</summary>
        private void HandleBack()
        {
            if (MenuUi.BackPressed() && MenuUi.IsShown(multiplayerPanel) && MenuUi.IsShown(home))
                ToggleMultiplayer();
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
            if (!built)
                return;
            var lobby = subscribedLobby;
            bool hasLobby = lobby != null && lobby.IsSpawned;
            var catalog = GameCatalog.Instance;

            string code = OnlineSessions.Service?.JoinCode;
            MenuUi.SetText(codeLabel, string.IsNullOrEmpty(code) ? "Partie locale" : $"Code de la partie : <b>{code}</b>");
            MenuUi.SetShown(copyCodeButton, !string.IsNullOrEmpty(code));
            MenuUi.SetShown(inviteButton, OnlineSessions.InviteFriends != null);

            if (!hasLobby)
            {
                MenuUi.SetText(playersLabel, "En attente du salon...");
                mainButton?.SetEnabled(false);
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
            MenuUi.SetText(playersLabel, builder.ToString());

            // Character
            bool hasMe = lobby.TryGetLocalPlayer(out var me);
            if (hasMe && !sentInitialLoadout && catalog != null && catalog.GetCharacter(me.Character) != null)
            {
                sentInitialLoadout = true;
                int last = PlayerPrefs.GetInt(LobbyState.LastCharacterKey, me.Character);
                var lastCharacter = catalog.GetCharacter(last);
                if (last != me.Character && lastCharacter != null && MetaProgression.IsUnlocked(lastCharacter))
                    SelectCharacter(last); // also sends its loadout
                else
                    lobby.SetLoadout(MetaProgression.GetLoadout(catalog.GetCharacter(me.Character)));
            }
            // On the host the selection RPC runs immediately (and refreshes re-entrantly): read the state again
            // so this pass doesn't overwrite the UI with the previous character.
            hasMe = lobby.TryGetLocalPlayer(out me);
            var selected = hasMe && catalog != null ? catalog.GetCharacter(me.Character) : null;
            for (int i = 0; i < characterCards.Count && catalog != null; i++)
            {
                var character = catalog.GetCharacter(i);
                if (character != null)
                    characterCards[i].Set(character.displayName, $"{character.maxHealth:0} PV", character.portrait, character.bodyColor,
                        hasMe && me.Character == i, LockText(character));
            }
            if (characterPreview != null)
                characterPreview.Show(selected);
            MenuUi.SetText(characterDescription, selected != null
                ? $"<size=130%><b>{selected.displayName}</b></size>  ·  {selected.maxHealth:0} PV\n<i>{selected.description}</i>"
                : string.Empty);
            RefreshSkillCards(selected, hasMe ? me.Loadout : (byte)0);

            // Difficulty
            bool isServer = lobby.IsServer;
            bool hardUnlocked = MetaProgression.IsHardModeUnlocked;
            if (normalCard != null)
            {
                normalCard.Set("Normal", "Pour découvrir le jeu.", null, new Color(0.35f, 0.6f, 0.35f), lobby.Difficulty == DifficultyLevel.Normal, null);
                normalCard.Root?.SetEnabled(isServer);
            }
            if (hardCard != null)
            {
                hardCard.Set("Difficile", "Pour les vétérans.", null, new Color(0.75f, 0.25f, 0.2f), lobby.Difficulty == DifficultyLevel.Hard,
                    hardUnlocked ? null : LockText(MetaProgression.HardModeChallenge));
                hardCard.Root?.SetEnabled(isServer && hardUnlocked);
            }
            MenuUi.SetText(difficultyDescription, (lobby.Difficulty == DifficultyLevel.Hard ? HardDescription : NormalDescription)
                                                  + (isServer ? string.Empty : "\n<i>Choisie par l'hôte.</i>")
                                                  + (isServer && !hardUnlocked ? "\n<i>Difficile : débloqué après une première victoire.</i>" : string.Empty));

            // Start / ready
            if (isServer)
            {
                mainButton?.SetEnabled(lobby.AllGuestsReady);
                MenuUi.SetText(mainButton, lobby.AllGuestsReady ? "Lancer la partie" : "En attente des joueurs...");
            }
            else
            {
                mainButton?.SetEnabled(hasMe);
                MenuUi.SetText(mainButton, hasMe && me.Ready ? "Annuler « Prêt »" : "Prêt");
            }
        }

        private void RefreshSkillCards(RiskOfMagic.Characters.CharacterDefinition character, byte loadout)
        {
            for (int slot = 0; slot < 4 && skillCards.Count >= 8; slot++)
            {
                var baseCard = skillCards[slot * 2];
                var altCard = skillCards[slot * 2 + 1];
                var baseSkill = character != null ? character.GetSkill((SkillSlot)slot) : null;
                var altSkill = character != null ? character.GetAlternative((SkillSlot)slot) : null;
                bool usingAlt = altSkill != null && (loadout & (1 << slot)) != 0 && MetaProgression.IsUnlocked(altSkill);
                var neutral = new Color(0.35f, 0.32f, 0.4f);
                string key = RiskOfMagic.Player.GameInput.SkillKey(slot);

                baseCard.SetVisible(baseSkill != null);
                if (baseSkill != null)
                    baseCard.Set($"<size=75%>{key}</size>  {baseSkill.displayName}", baseSkill.description, baseSkill.icon, neutral, !usingAlt, null);

                altCard.SetVisible(altSkill != null);
                if (altSkill != null)
                    altCard.Set($"<size=75%>{key} · alt.</size>  {altSkill.displayName}", altSkill.description, altSkill.icon, neutral, usingAlt,
                        LockText(altSkill));
            }
        }

        // ---------------------------------------------------------------- Helpers

        private void ShowLobby(bool lobby)
        {
            if (!lobby)
                sentInitialLoadout = false;
            if (options.IsOpen)
                options.Close();
            if (journal.IsOpen)
                journal.Close();
            ShowHome(!lobby);
            MenuUi.SetShown(lobbyScreen, lobby, animate: true);
            if (characterPreview != null)
                characterPreview.gameObject.SetActive(lobby); // renders only while visible
            if (lobby)
                MenuUi.Focus(mainButton);
            RefreshLobby();
        }

        private void SetBusy(bool value, string message = null)
        {
            busy = value;
            if (message != null)
                SetStatus(message);
            soloButton?.SetEnabled(!value);
            multiplayerButton?.SetEnabled(!value);
            hostButton?.SetEnabled(!value && OnlineSessions.IsAvailable);
            joinButton?.SetEnabled(!value && OnlineSessions.IsAvailable);
        }

        private void SetStatus(string message) => MenuUi.SetText(statusLabel, message);
    }
}
