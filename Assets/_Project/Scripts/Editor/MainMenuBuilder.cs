using RiskOfMagic.Flow;
using RiskOfMagic.Networking;
using RiskOfMagic.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using H = RiskOfMagic.Editor.HudBuilder;

namespace RiskOfMagic.Editor
{
    /// <summary>
    /// Builds the MainMenu scene (first scene of the build): persistent NetworkManager, lobby prefab and the menu UI.
    /// Called by PrototypeSceneBuilder so both scenes share the same NetworkManager setup.
    /// </summary>
    public static class MainMenuBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string LobbyPrefabPath = "Assets/_Project/Prefabs/LobbyState.prefab";

        public static GameObject BuildLobbyPrefab()
        {
            var root = new GameObject("LobbyState");
            root.AddComponent<NetworkObject>();
            root.AddComponent<LobbyState>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, LobbyPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static void BuildScene(NetworkPrefabsList prefabList, GameObject lobbyPrefab)
        {
            var theme = H.UseTheme();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = new Color(0.06f, 0.05f, 0.08f);
            }

            // Network (persists into the game scene)
            var netGo = new GameObject("NetworkManager");
            var manager = netGo.AddComponent<NetworkManager>();
            var transport = netGo.AddComponent<UnityTransport>();
            netGo.AddComponent<PersistentNetworkManager>();
            manager.NetworkConfig ??= new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = null; // avatars are spawned by the game scene (GameBootstrap)
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);

            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            // UI
            var canvasGo = new GameObject("MainMenu", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            H.Text(H.Rect("Title", root, H.TopCenter, H.TopCenter, new Vector2(0f, -60f), new Vector2(1200f, 110f)), null,
                "RISK OF MAGIC", 84f, HudFont.Title, HudColor.Accent, TextAlignmentOptions.Center);

            // Home
            var home = H.Rect("Home", root, H.Center, H.Center, new Vector2(0f, -40f), new Vector2(460f, 656f));
            H.Themed(home, HudSprite.Panel, HudColor.Panel);
            H.VerticalLayout(home.gameObject, 24, 14);
            var solo = H.Button(home, "SoloButton", "Solo", 60f);
            var host = H.Button(home, "HostButton", "Créer une partie en ligne", 60f);
            H.LayoutHeight(H.Text(home, "JoinHeader", "Rejoindre avec un code", 20f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.Left, stretch: false).rectTransform, 28f);
            var codeField = H.InputField(home, "JoinCode", "ABC123");
            codeField.text = string.Empty;
            codeField.characterLimit = 12;
            var join = H.Button(home, "JoinButton", "Rejoindre", 56f);
            var journal = H.Button(home, "JournalButton", "Journal", 52f);
            var options = H.Button(home, "OptionsButton", "Options", 52f);
            var quit = H.Button(home, "QuitButton", "Quitter", 52f);
            var status = H.Text(H.Rect("Status", root, H.Center, H.Center, new Vector2(0f, -340f), new Vector2(1000f, 60f)), null,
                string.Empty, 22f, HudFont.Body, HudColor.Danger, TextAlignmentOptions.Center);

            // Lobby
            var lobby = H.Stretch("Lobby", root);
            var code = H.Text(H.Rect("Code", lobby, H.TopCenter, H.TopCenter, new Vector2(-60f, -180f), new Vector2(700f, 44f)), null,
                "Code de la partie", 30f, HudFont.Body, HudColor.Text, TextAlignmentOptions.Center);
            var copy = H.Button(lobby, "CopyCodeButton", "Copier", 0f);
            var copyRect = (RectTransform)copy.transform;
            copyRect.anchorMin = copyRect.anchorMax = copyRect.pivot = H.TopCenter;
            copyRect.anchoredPosition = new Vector2(360f, -180f);
            copyRect.sizeDelta = new Vector2(140f, 44f);

            // Left column: players, difficulty (locked card shows its challenge).
            var playersPanel = Panel(lobby, "Players", H.Center, H.Center, new Vector2(-710f, 145f), new Vector2(420f, 290f), "Joueurs");
            var players = H.Text(H.Rect("List", playersPanel, H.TopLeft, H.TopLeft, new Vector2(16f, -64f), new Vector2(388f, 216f)), null,
                "En attente du salon...", 19f, HudFont.Body, HudColor.Text, TextAlignmentOptions.TopLeft);

            var difficultyPanel = Panel(lobby, "Difficulty", H.Center, H.Center, new Vector2(-710f, -200f), new Vector2(420f, 380f), "Difficulté");
            var normal = Card(difficultyPanel, "NormalCard", new Vector2(380f, 96f), 64f);
            Anchor(normal, new Vector2(0f, -60f));
            var hard = Card(difficultyPanel, "HardCard", new Vector2(380f, 96f), 64f);
            Anchor(hard, new Vector2(0f, -166f));
            var difficultyInfo = H.Text(H.Rect("Description", difficultyPanel, H.TopLeft, H.TopLeft, new Vector2(20f, -272f), new Vector2(380f, 100f)), null,
                string.Empty, 16f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.TopLeft);

            // Center: 3D preview of the selected character (model or placeholder).
            var previewPanel = Panel(lobby, "Preview", H.Center, H.Center, new Vector2(-210f, -40f), new Vector2(520f, 660f), "Aperçu");
            var previewRect = H.Rect("Model", previewPanel, H.TopCenter, H.TopCenter, new Vector2(0f, -60f), new Vector2(460f, 480f));
            previewRect.gameObject.AddComponent<UnityEngine.UI.RawImage>().raycastTarget = false;
            var preview = previewRect.gameObject.AddComponent<CharacterPreview>();
            var characterInfo = H.Text(H.Rect("Description", previewPanel, H.TopLeft, H.TopLeft, new Vector2(24f, -548f), new Vector2(472f, 104f)), null,
                string.Empty, 17f, HudFont.Body, HudColor.Text, TextAlignmentOptions.TopLeft);

            // Right: character cards, then the skill grid (base | alternative per slot).
            var selectionPanel = Panel(lobby, "Selection", H.Center, H.Center, new Vector2(500f, -40f), new Vector2(840f, 660f), "Personnage");
            var characterRow = H.Rect("Characters", selectionPanel, H.TopCenter, H.TopCenter, new Vector2(0f, -60f), new Vector2(800f, 130f));
            var row = characterRow.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.spacing = 8f;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            var characterTemplate = Card(characterRow, "CharacterTemplate", new Vector2(194f, 130f), 64f);

            H.Text(H.Rect("SkillsHeader", selectionPanel, H.TopLeft, H.TopLeft, new Vector2(20f, -200f), new Vector2(800f, 34f)), null,
                "Compétences  <size=70%><alpha=#A0>(de base | alternative)</size>", 24f, HudFont.Title, HudColor.Accent, TextAlignmentOptions.BottomLeft);
            var skillGrid = H.Rect("Skills", selectionPanel, H.TopCenter, H.TopCenter, new Vector2(0f, -240f), new Vector2(800f, 408f));
            var grid = skillGrid.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.cellSize = new Vector2(396f, 96f);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            var skillTemplate = Card(skillGrid, "SkillTemplate", new Vector2(396f, 96f), 56f);

            var main = H.Button(lobby, "MainButton", "Prêt", 0f);
            Place(main, H.BottomCenter, new Vector2(0f, 60f), new Vector2(380f, 72f));
            var mainLabel = main.GetComponentInChildren<TextMeshProUGUI>();
            mainLabel.fontSize = 28f;
            var leave = H.Button(lobby, "LeaveButton", "Quitter le salon", 0f);
            Place(leave, H.BottomLeft, new Vector2(60f, 60f), new Vector2(260f, 56f));
            lobby.gameObject.SetActive(false);

            var optionsMenu = BuildOptions(root);
            var journalMenu = BuildJournal(root, theme);

            var controller = canvasGo.AddComponent<MainMenuController>();
            H.Props(controller, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("lobbyPrefab").objectReferenceValue = lobbyPrefab.GetComponent<LobbyState>();
                so.FindProperty("homeScreen").objectReferenceValue = home.gameObject;
                so.FindProperty("soloButton").objectReferenceValue = solo;
                so.FindProperty("hostButton").objectReferenceValue = host;
                so.FindProperty("joinCodeField").objectReferenceValue = codeField;
                so.FindProperty("joinButton").objectReferenceValue = join;
                so.FindProperty("quitButton").objectReferenceValue = quit;
                so.FindProperty("optionsButton").objectReferenceValue = options;
                so.FindProperty("optionsMenu").objectReferenceValue = optionsMenu;
                so.FindProperty("journalButton").objectReferenceValue = journal;
                so.FindProperty("journalMenu").objectReferenceValue = journalMenu;
                so.FindProperty("skillCardTemplate").objectReferenceValue = skillTemplate;
                so.FindProperty("characterPreview").objectReferenceValue = preview;
                so.FindProperty("statusLabel").objectReferenceValue = status;
                so.FindProperty("lobbyScreen").objectReferenceValue = lobby.gameObject;
                so.FindProperty("codeLabel").objectReferenceValue = code;
                so.FindProperty("copyCodeButton").objectReferenceValue = copy;
                so.FindProperty("playersLabel").objectReferenceValue = players;
                so.FindProperty("characterCardTemplate").objectReferenceValue = characterTemplate;
                so.FindProperty("characterDescription").objectReferenceValue = characterInfo;
                so.FindProperty("normalCard").objectReferenceValue = normal;
                so.FindProperty("hardCard").objectReferenceValue = hard;
                so.FindProperty("difficultyDescription").objectReferenceValue = difficultyInfo;
                so.FindProperty("mainButton").objectReferenceValue = main;
                so.FindProperty("mainButtonLabel").objectReferenceValue = mainLabel;
                so.FindProperty("leaveButton").objectReferenceValue = leave;
            });

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        /// <summary>Lobby card: button frame, icon (+ letter fallback), title, body, selection highlight, lock overlay.</summary>
        private static LobbyCard Card(RectTransform parent, string name, Vector2 size, float iconSize)
        {
            var rect = H.Rect(name, parent, H.Center, H.Center, Vector2.zero, size);
            var element = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            element.preferredWidth = size.x;
            element.preferredHeight = size.y;
            var image = H.Themed(rect, HudSprite.SlotFrame, HudColor.Button, raycast: true);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;

            var selected = H.Stretch("Selected", rect);
            H.Themed(selected, HudSprite.Frame, HudColor.ButtonSelected);
            selected.gameObject.SetActive(false);

            var icon = H.Rect("Icon", rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(iconSize, iconSize));
            var iconImage = H.PlainImage(icon);
            var letter = H.Text(icon, "Letter", string.Empty, iconSize * 0.55f, HudFont.Title, HudColor.Text, TextAlignmentOptions.Center);

            float textX = iconSize + 20f;
            var title = H.Text(H.Rect("Title", rect, H.TopLeft, H.TopLeft, new Vector2(textX, -6f), new Vector2(size.x - textX - 8f, 30f)), null,
                "Titre", 19f, HudFont.Title, HudColor.Text, TextAlignmentOptions.TopLeft);
            var body = H.Text(H.Rect("Body", rect, H.TopLeft, H.TopLeft, new Vector2(textX, -34f), new Vector2(size.x - textX - 8f, size.y - 40f)), null,
                string.Empty, 14f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.TopLeft);

            var lockRect = H.Stretch("Lock", rect);
            H.Themed(lockRect, null, HudColor.Overlay);
            var lockLabel = H.Text(lockRect, "Label", "VERROUILLÉ", 15f, HudFont.Body, HudColor.Text, TextAlignmentOptions.Center);
            lockRect.gameObject.SetActive(false);

            var card = rect.gameObject.AddComponent<LobbyCard>();
            H.Props(card, so =>
            {
                so.FindProperty("button").objectReferenceValue = button;
                so.FindProperty("icon").objectReferenceValue = iconImage;
                so.FindProperty("iconLetter").objectReferenceValue = letter;
                so.FindProperty("titleLabel").objectReferenceValue = title;
                so.FindProperty("bodyLabel").objectReferenceValue = body;
                so.FindProperty("selectedFrame").objectReferenceValue = selected.gameObject;
                so.FindProperty("lockOverlay").objectReferenceValue = lockRect.gameObject;
                so.FindProperty("lockLabel").objectReferenceValue = lockLabel;
            });
            return card;
        }

        private static void Anchor(LobbyCard card, Vector2 topCenterOffset)
        {
            var rect = (RectTransform)card.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = H.TopCenter;
            rect.anchoredPosition = topCenterOffset;
        }

        /// <summary>Journal: 4 tabs filling one scrollable rich text (stats, challenges, items, bestiary).</summary>
        private static JournalMenu BuildJournal(RectTransform root, HudTheme theme)
        {
            var panel = Panel(root, "Journal", H.Center, H.Center, new Vector2(0f, -40f), new Vector2(1100f, 880f), "Journal");

            var tabs = H.Rect("Tabs", panel, H.TopCenter, H.TopCenter, new Vector2(0f, -66f), new Vector2(1040f, 52f));
            var tabLayout = tabs.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            tabLayout.spacing = 8f;
            tabLayout.childControlWidth = tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = tabLayout.childForceExpandHeight = true;
            var stats = H.Button(tabs, "StatsTab", "Statistiques", 0f);
            var challenges = H.Button(tabs, "ChallengesTab", "Défis", 0f);
            var items = H.Button(tabs, "ItemsTab", "Objets", 0f);
            var bestiary = H.Button(tabs, "BestiaryTab", "Bestiaire", 0f);

            // Scroll view with a single auto-sized text.
            var viewport = H.Rect("Viewport", panel, H.TopCenter, H.TopCenter, new Vector2(0f, -132f), new Vector2(1040f, 640f));
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            H.Themed(viewport, null, HudColor.BarBackground, raycast: true);
            var contentRect = H.Rect("Content", viewport, H.TopLeft, H.TopLeft, Vector2.zero, new Vector2(1040f, 640f));
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(16f, contentRect.offsetMin.y);
            contentRect.offsetMax = new Vector2(-16f, 0f);
            var text = H.Text(contentRect, null, string.Empty, 20f, HudFont.Body, HudColor.Text, TextAlignmentOptions.TopLeft);
            var fitter = contentRect.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 30f;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;

            var back = H.Button(panel, "BackButton", "Retour", 0f);
            Place(back, H.BottomCenter, new Vector2(0f, 24f), new Vector2(260f, 56f));

            var menu = panel.gameObject.AddComponent<JournalMenu>();
            H.Props(menu, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("statsTab").objectReferenceValue = stats;
                so.FindProperty("challengesTab").objectReferenceValue = challenges;
                so.FindProperty("itemsTab").objectReferenceValue = items;
                so.FindProperty("bestiaryTab").objectReferenceValue = bestiary;
                so.FindProperty("content").objectReferenceValue = text;
                so.FindProperty("scroll").objectReferenceValue = scroll;
                so.FindProperty("backButton").objectReferenceValue = back;
            });
            panel.gameObject.SetActive(false);
            return menu;
        }

        /// <summary>Options screen: rows are created at runtime by OptionsMenu from the two templates.</summary>
        internal static OptionsMenu BuildOptions(RectTransform root)
        {
            var panel = Panel(root, "Options", H.Center, H.Center, new Vector2(0f, -40f), new Vector2(900f, 900f), "Options");

            var content = H.Rect("Content", panel, H.TopCenter, H.TopCenter, new Vector2(0f, -66f), new Vector2(840f, 740f));
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 2f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;

            var header = H.Text(content, "HeaderTemplate", "Section", 24f, HudFont.Title, HudColor.Accent, TextAlignmentOptions.BottomLeft, stretch: false);
            H.LayoutHeight(header.rectTransform, 34f);

            var row = H.Rect("RowTemplate", content, H.Center, H.Center, Vector2.zero, new Vector2(0f, 34f));
            H.LayoutHeight(row, 34f);
            var label = H.Text(H.Rect("Label", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(420f, 38f)), null,
                "Option", 20f, HudFont.Body, HudColor.Text, TextAlignmentOptions.MidlineLeft);
            var previous = H.Button(row, "Previous", "<", 0f);
            Place(previous, new Vector2(1f, 0.5f), new Vector2(-330f, 0f), new Vector2(44f, 34f));
            var value = H.Text(H.Rect("Value", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-58f, 0f), new Vector2(260f, 38f)), null,
                "Valeur", 20f, HudFont.Body, HudColor.Accent, TextAlignmentOptions.Center);
            var next = H.Button(row, "Next", ">", 0f);
            Place(next, new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(44f, 34f));
            var optionRow = row.gameObject.AddComponent<OptionRow>();
            H.Props(optionRow, so =>
            {
                so.FindProperty("label").objectReferenceValue = label;
                so.FindProperty("valueLabel").objectReferenceValue = value;
                so.FindProperty("previousButton").objectReferenceValue = previous;
                so.FindProperty("nextButton").objectReferenceValue = next;
            });

            var reset = H.Button(panel, "ResetButton", "Par défaut", 0f);
            Place(reset, H.BottomCenter, new Vector2(-150f, 24f), new Vector2(260f, 56f));
            var back = H.Button(panel, "BackButton", "Retour", 0f);
            Place(back, H.BottomCenter, new Vector2(150f, 24f), new Vector2(260f, 56f));

            var menu = panel.gameObject.AddComponent<OptionsMenu>();
            H.Props(menu, so =>
            {
                so.FindProperty("content").objectReferenceValue = content;
                so.FindProperty("headerTemplate").objectReferenceValue = header;
                so.FindProperty("rowTemplate").objectReferenceValue = optionRow;
                so.FindProperty("resetButton").objectReferenceValue = reset;
                so.FindProperty("backButton").objectReferenceValue = back;
            });
            panel.gameObject.SetActive(false);
            return menu;
        }

        private static RectTransform Panel(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, string title)
        {
            var panel = H.Rect(name, parent, anchor, pivot, position, size);
            H.Themed(panel, HudSprite.Panel, HudColor.Panel);
            H.Text(H.Rect("Title", panel, H.TopCenter, H.TopCenter, new Vector2(0f, -12f), new Vector2(size.x - 40f, 44f)), null,
                title, 30f, HudFont.Title, HudColor.Accent, TextAlignmentOptions.Center);
            return panel;
        }

        private static void Place(UnityEngine.UI.Button button, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
