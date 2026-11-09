using System;
using System.Collections.Generic;
using System.Text;
using RiskOfMagic.Core;
using RiskOfMagic.Flow;
using RiskOfMagic.Meta;
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
        [SerializeField, Tooltip("Lobby title (the home and the menus have their own).")] private GameObject screenTitle;
        [SerializeField] private UnityEngine.UI.Button soloButton;
        [SerializeField] private UnityEngine.UI.Button multiplayerButton;
        [SerializeField, Tooltip("Create online / join by code, unfolded by the Multiplayer entry.")] private GameObject multiplayerPanel;
        [SerializeField] private UnityEngine.UI.Button hostButton;
        [SerializeField] private TMP_InputField joinCodeField;
        [SerializeField] private UnityEngine.UI.Button joinButton;
        [SerializeField] private UnityEngine.UI.Button optionsButton;
        [SerializeField] private UnityEngine.UI.Button journalButton;
        [SerializeField] private JournalMenu journalMenu;
        [SerializeField] private UnityEngine.UI.Button quitButton;
        [SerializeField] private OptionsMenu optionsMenu;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text profileLabel;
        [SerializeField] private TMP_Text versionLabel;
        [SerializeField, Tooltip("Last played character, behind the menu.")] private CharacterPreview heroPreview;
        [SerializeField] private TMP_Text heroLabel;

        [Header("Lobby")]
        [SerializeField] private GameObject lobbyScreen;
        [SerializeField] private TMP_Text codeLabel;
        [SerializeField] private UnityEngine.UI.Button copyCodeButton;
        [SerializeField] private TMP_Text playersLabel;
        [SerializeField, Tooltip("Cloned per character.")] private LobbyCard characterCardTemplate;
        [SerializeField] private CharacterPreview characterPreview;
        [SerializeField] private TMP_Text characterDescription;
        [SerializeField, Tooltip("Cloned per slot: base then alternative (grid of 2 columns).")] private LobbyCard skillCardTemplate;
        [SerializeField] private LobbyCard normalCard;
        [SerializeField] private LobbyCard hardCard;
        [SerializeField] private TMP_Text difficultyDescription;
        [SerializeField] private UnityEngine.UI.Button mainButton;
        [SerializeField] private TMP_Text mainButtonLabel;
        [SerializeField] private UnityEngine.UI.Button leaveButton;

        private readonly List<LobbyCard> characterCards = new();
        private readonly List<LobbyCard> skillCards = new(); // index = slot * 2 + (alternative ? 1 : 0)
        private bool sentInitialLoadout;
        private static readonly string[] SlotKeys = { "Clic G", "Clic D", "Maj", "R" };
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
            AddListener(multiplayerButton, ToggleMultiplayer);
            if (multiplayerPanel != null)
                multiplayerPanel.SetActive(false);
            if (versionLabel != null)
                versionLabel.text = $"v{Application.version}";
            AddListener(hostButton, () => _ = HostOnline());
            AddListener(joinButton, () => _ = JoinOnline());
            AddListener(quitButton, Application.Quit);
            AddListener(optionsButton, OpenOptions);
            AddListener(journalButton, OpenJournal);
            if (journalMenu != null)
                journalMenu.gameObject.SetActive(false);
            if (optionsMenu != null)
                optionsMenu.gameObject.SetActive(false);
            AddListener(copyCodeButton, () => GUIUtility.systemCopyBuffer = OnlineSessions.Service?.JoinCode ?? string.Empty);
            if (normalCard != null)
                AddListener(normalCard.Button, () => SetDifficulty(DifficultyLevel.Normal));
            if (hardCard != null)
                AddListener(hardCard.Button, () => SetDifficulty(DifficultyLevel.Hard));
            AddListener(mainButton, OnMainButton);
            AddListener(leaveButton, () => _ = Leave());

            BuildCharacterCards();
            BuildSkillCards();
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
            ShowHome(false);
            optionsMenu.Open(() => ShowHome(true, optionsButton));
        }

        private void OpenJournal()
        {
            if (journalMenu == null)
                return;
            ShowHome(false);
            journalMenu.Open(() => ShowHome(true, journalButton));
        }

        private void ToggleMultiplayer()
        {
            if (multiplayerPanel == null)
                return;
            bool open = !multiplayerPanel.activeSelf;
            multiplayerPanel.SetActive(open);
            var first = hostButton != null && hostButton.interactable ? hostButton.gameObject : null;
            Select(open && first != null ? first : multiplayerButton != null ? multiplayerButton.gameObject : null);
        }

        private void ShowHome(bool visible, UnityEngine.UI.Button focus = null)
        {
            if (homeScreen != null)
                homeScreen.SetActive(visible);
            if (!visible)
                return;
            if (multiplayerPanel != null)
                multiplayerPanel.SetActive(false);
            RefreshHome();
            var target = focus != null ? focus : soloButton;
            Select(target != null ? target.gameObject : null);
        }

        /// <summary>Profile summary, last played character.</summary>
        private void RefreshHome()
        {
            if (profileLabel != null)
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
                profileLabel.text = text;
            }

            var catalog = GameCatalog.Instance;
            var character = catalog != null ? catalog.GetCharacter(PlayerPrefs.GetInt(LobbyState.LastCharacterKey, 0)) : null;
            if (character == null || !MetaProgression.IsUnlocked(character))
                character = catalog != null ? catalog.GetCharacter(0) : null;
            if (heroPreview != null)
                heroPreview.Show(character);
            if (heroLabel != null)
                heroLabel.text = character != null ? $"{character.displayName}\n<size=55%><alpha=#90>{character.description}</size>" : string.Empty;
        }

        private static void Select(GameObject target)
        {
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(target);
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
            if (catalog == null || characterCardTemplate == null)
                return;
            characterCardTemplate.gameObject.SetActive(false);
            for (int i = 0; i < catalog.Characters.Count; i++)
            {
                int index = i;
                var card = Instantiate(characterCardTemplate, characterCardTemplate.transform.parent);
                card.gameObject.SetActive(true);
                AddListener(card.Button, () => SelectCharacter(index));
                characterCards.Add(card);
            }
        }

        private void BuildSkillCards()
        {
            if (skillCardTemplate == null)
                return;
            skillCardTemplate.gameObject.SetActive(false);
            for (int slot = 0; slot < 4; slot++)
                for (int variant = 0; variant < 2; variant++)
                {
                    int s = slot;
                    bool alternative = variant == 1;
                    var card = Instantiate(skillCardTemplate, skillCardTemplate.transform.parent);
                    card.gameObject.SetActive(true);
                    AddListener(card.Button, () => SelectSkill(s, alternative));
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
            if (LobbyState.Instance != subscribedLobby)
                Subscribe(LobbyState.Instance);
            HandleBack();

            // Host left / connection lost while in the lobby.
            bool listening = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            if (!busy && !listening && lobbyScreen != null && lobbyScreen.activeSelf)
            {
                ShowLobby(false);
                SetStatus("La partie a été fermée.");
            }
        }

        /// <summary>Esc / B: folds the multiplayer panel, closes the journal (the options close themselves).</summary>
        private void HandleBack()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (!((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame)))
                return;
            if (journalMenu != null && journalMenu.gameObject.activeSelf)
                journalMenu.Close();
            else if (multiplayerPanel != null && multiplayerPanel.activeSelf && homeScreen != null && homeScreen.activeSelf)
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
            if (characterDescription != null)
                characterDescription.text = selected != null ? $"<size=130%><b>{selected.displayName}</b></size>  ·  {selected.maxHealth:0} PV\n<i>{selected.description}</i>" : string.Empty;
            RefreshSkillCards(selected, hasMe ? me.Loadout : (byte)0);

            // Difficulty
            bool isServer = lobby.IsServer;
            bool hardUnlocked = MetaProgression.IsHardModeUnlocked;
            if (normalCard != null)
            {
                normalCard.Set("Normal", "Pour découvrir le jeu.", null, new Color(0.35f, 0.6f, 0.35f), lobby.Difficulty == DifficultyLevel.Normal, null);
                SetInteractable(normalCard.Button, isServer);
            }
            if (hardCard != null)
            {
                hardCard.Set("Difficile", "Pour les vétérans.", null, new Color(0.75f, 0.25f, 0.2f), lobby.Difficulty == DifficultyLevel.Hard,
                    hardUnlocked ? null : LockText(MetaProgression.HardModeChallenge));
                SetInteractable(hardCard.Button, isServer && hardUnlocked);
            }
            if (difficultyDescription != null)
                difficultyDescription.text = (lobby.Difficulty == DifficultyLevel.Hard ? HardDescription : NormalDescription)
                                             + (isServer ? string.Empty : "\n<size=80%><i>Choisie par l'hôte.</i></size>")
                                             + (isServer && !hardUnlocked ? "\n<size=80%><i>Difficile : débloqué après une première victoire.</i></size>" : string.Empty);

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

                baseCard.gameObject.SetActive(baseSkill != null);
                if (baseSkill != null)
                    baseCard.Set($"<size=75%>{SlotKeys[slot]}</size>  {baseSkill.displayName}", baseSkill.description, baseSkill.icon, neutral, !usingAlt, null);

                altCard.gameObject.SetActive(altSkill != null);
                if (altSkill != null)
                    altCard.Set($"<size=75%>{SlotKeys[slot]} · alt.</size>  {altSkill.displayName}", altSkill.description, altSkill.icon, neutral, usingAlt,
                        LockText(altSkill));
            }
        }

        // ---------------------------------------------------------------- Helpers

        private void ShowLobby(bool lobby)
        {
            if (!lobby)
                sentInitialLoadout = false;
            ShowHome(!lobby);
            if (screenTitle != null)
                screenTitle.SetActive(lobby);
            if (lobbyScreen != null)
                lobbyScreen.SetActive(lobby);
            RefreshLobby();
        }

        private void SetBusy(bool value, string message = null)
        {
            busy = value;
            if (message != null)
                SetStatus(message);
            foreach (var button in new[] { soloButton, multiplayerButton, hostButton, joinButton })
                if (button != null)
                    button.interactable = !value && (button == soloButton || button == multiplayerButton || OnlineSessions.IsAvailable);
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
