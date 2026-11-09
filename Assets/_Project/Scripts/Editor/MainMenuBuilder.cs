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

            // Optional art (ArtLibrary): full-screen background, logo replacing the home title.
            var background = H.Stretch("Background", root).gameObject.AddComponent<UnityEngine.UI.Image>();
            background.raycastTarget = false;
            background.gameObject.SetActive(false);

            var screenTitle = H.Text(H.Rect("Title", root, H.TopCenter, H.TopCenter, new Vector2(0f, -60f), new Vector2(1200f, 110f)), null,
                "RISK OF MAGIC", 84f, HudFont.Title, HudColor.Accent, TextAlignmentOptions.Center);
            screenTitle.gameObject.SetActive(false);

            // Home: last played character on the right, title + text-only vertical menu on a dark band on the left.
            var home = H.Stretch("Home", root);
            home.gameObject.AddComponent<CanvasGroup>();
            home.gameObject.AddComponent<FadeInOnEnable>();

            var heroRect = H.Rect("Hero", home, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-60f, 0f), new Vector2(840f, 1050f));
            heroRect.gameObject.AddComponent<UnityEngine.UI.RawImage>().raycastTarget = false;
            var hero = heroRect.gameObject.AddComponent<CharacterPreview>();
            H.Props(hero, so =>
            {
                so.FindProperty("transparentBackground").boolValue = true;
                so.FindProperty("stagePosition").vector3Value = new Vector3(60f, -500f, 0f);
                so.FindProperty("resolution").vector2IntValue = new Vector2Int(840, 1050);
                so.FindProperty("rotationSpeed").floatValue = 12f;
            });
            var heroLabel = H.Text(H.Rect("HeroName", home, H.BottomRight, H.BottomRight, new Vector2(-100f, 90f), new Vector2(720f, 130f)), null,
                string.Empty, 44f, HudFont.Title, HudColor.Text, TextAlignmentOptions.BottomRight);

            var band = H.Rect("Band", home, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(700f, 0f));
            band.anchorMin = Vector2.zero;
            band.anchorMax = new Vector2(0f, 1f);
            band.sizeDelta = new Vector2(700f, 0f);
            H.Themed(band, null, HudColor.Panel);
            var fade = H.Rect("Fade", band, new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(120f, 0f));
            fade.anchorMin = new Vector2(1f, 0f);
            fade.anchorMax = Vector2.one;
            fade.sizeDelta = new Vector2(120f, 0f);
            H.Themed(fade, null, HudColor.Panel);
            fade.gameObject.AddComponent<CanvasGroup>().alpha = 0.45f;

            var gameTitle = H.Text(H.Rect("GameTitle", home, H.TopLeft, H.TopLeft, new Vector2(110f, -110f), new Vector2(1000f, 110f)), null,
                "RISK OF MAGIC", 92f, HudFont.Title, HudColor.Accent, TextAlignmentOptions.MidlineLeft);
            var logo = H.Rect("Logo", home, H.TopLeft, H.TopLeft, new Vector2(110f, -40f), new Vector2(620f, 180f)).gameObject.AddComponent<UnityEngine.UI.Image>();
            logo.raycastTarget = false;
            logo.preserveAspect = true;
            logo.gameObject.SetActive(false);
            H.Text(H.Rect("Tagline", home, H.TopLeft, H.TopLeft, new Vector2(116f, -218f), new Vector2(900f, 36f)), null,
                "Roguelite coopératif  ·  1 à 4 joueurs", 22f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.MidlineLeft);

            var list = H.Rect("Menu", home, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(92f, -30f), new Vector2(540f, 5f * 72f));
            H.VerticalLayout(list.gameObject, 0, 6f);
            var solo = MenuEntry(list, theme, "SoloButton", "Jouer en solo", 40f, 66f);
            var multiplayer = MenuEntry(list, theme, "MultiplayerButton", "Multijoueur", 40f, 66f);
            var journal = MenuEntry(list, theme, "JournalButton", "Journal", 40f, 66f);
            var options = MenuEntry(list, theme, "OptionsButton", "Options", 40f, 66f);
            var quit = MenuEntry(list, theme, "QuitButton", "Quitter", 40f, 66f);

            // Multiplayer: unfolds next to the list.
            var online = H.Rect("Multiplayer", home, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(640f, 40f), new Vector2(440f, 300f));
            H.Themed(online, HudSprite.Panel, HudColor.Panel, raycast: true);
            online.gameObject.AddComponent<CanvasGroup>();
            H.Props(online.gameObject.AddComponent<FadeInOnEnable>(), so => so.FindProperty("offset").vector2Value = new Vector2(-16f, 0f));
            H.VerticalLayout(online.gameObject, 22, 10f);
            var host = MenuEntry(online, theme, "HostButton", "Créer une partie en ligne", 28f, 50f);
            H.LayoutHeight(H.Text(online, "JoinHeader", "Rejoindre avec un code", 18f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.BottomLeft, stretch: false).rectTransform, 30f);
            var codeField = H.InputField(online, "JoinCode", "Code de la partie");
            codeField.text = string.Empty;
            codeField.characterLimit = 12;
            var join = MenuEntry(online, theme, "JoinButton", "Rejoindre", 28f, 50f);
            online.gameObject.SetActive(false);

            var status = H.Text(H.Rect("Status", home, H.BottomLeft, H.BottomLeft, new Vector2(110f, 112f), new Vector2(1000f, 34f)), null,
                string.Empty, 20f, HudFont.Body, HudColor.Danger, TextAlignmentOptions.MidlineLeft);
            var profile = H.Text(H.Rect("Profile", home, H.BottomLeft, H.BottomLeft, new Vector2(110f, 64f), new Vector2(1100f, 32f)), null,
                string.Empty, 19f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.MidlineLeft);
            var version = H.Text(H.Rect("Version", home, H.BottomRight, H.BottomRight, new Vector2(-32f, 24f), new Vector2(300f, 28f)), null,
                string.Empty, 15f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.MidlineRight);

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
            // 6 characters: grid of 3 columns x 2 rows.
            var characterRow = H.Rect("Characters", selectionPanel, H.TopCenter, H.TopCenter, new Vector2(0f, -60f), new Vector2(800f, 130f));
            var characterGrid = characterRow.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            characterGrid.cellSize = new Vector2(261f, 61f);
            characterGrid.spacing = new Vector2(8f, 8f);
            characterGrid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            characterGrid.constraintCount = 3;
            var characterTemplate = Card(characterRow, "CharacterTemplate", new Vector2(261f, 61f), 48f);

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
                so.FindProperty("screenTitle").objectReferenceValue = screenTitle.gameObject;
                so.FindProperty("soloButton").objectReferenceValue = solo;
                so.FindProperty("multiplayerButton").objectReferenceValue = multiplayer;
                so.FindProperty("multiplayerPanel").objectReferenceValue = online.gameObject;
                so.FindProperty("profileLabel").objectReferenceValue = profile;
                so.FindProperty("versionLabel").objectReferenceValue = version;
                so.FindProperty("heroPreview").objectReferenceValue = hero;
                so.FindProperty("heroLabel").objectReferenceValue = heroLabel;
                so.FindProperty("backgroundImage").objectReferenceValue = background;
                so.FindProperty("logoImage").objectReferenceValue = logo;
                so.FindProperty("gameTitle").objectReferenceValue = gameTitle;
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

        /// <summary>
        /// Options screen (main menu and pause menu): tabs, a scrolling page per tab and the row templates
        /// (choice, slider, switch) cloned at runtime by OptionsMenu.
        /// </summary>
        internal static OptionsMenu BuildOptions(RectTransform root)
        {
            var theme = H.UseTheme();
            var panel = H.Rect("Options", root, H.Center, H.Center, Vector2.zero, new Vector2(1120f, 820f));
            H.Themed(panel, HudSprite.Panel, HudColor.Panel, raycast: true);
            panel.gameObject.AddComponent<CanvasGroup>();
            H.Props(panel.gameObject.AddComponent<FadeInOnEnable>(), so => so.FindProperty("offset").vector2Value = new Vector2(0f, -18f));

            H.Text(H.Rect("Title", panel, H.TopLeft, H.TopLeft, new Vector2(48f, -30f), new Vector2(600f, 60f)), null,
                "Options", 42f, HudFont.Title, HudColor.Text, TextAlignmentOptions.MidlineLeft);
            H.Text(H.Rect("Shortcuts", panel, H.TopRight, H.TopRight, new Vector2(-48f, -44f), new Vector2(560f, 32f)), null,
                "Tab / LB · RB : onglet   ·   Échap : retour", 16f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.MidlineRight);

            var tabBar = H.Rect("Tabs", panel, H.TopCenter, H.TopCenter, new Vector2(0f, -100f), new Vector2(1040f, 52f));
            var tabLayout = tabBar.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            tabLayout.spacing = 4f;
            tabLayout.childControlWidth = tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = tabLayout.childForceExpandHeight = true;
            var tabTemplate = MenuEntry(tabBar, theme, "TabTemplate", "Onglet", 24f, 52f, underline: true).GetComponent<MenuButton>();
            Line(panel, -156f);

            var viewport = H.Rect("Viewport", panel, H.TopCenter, H.TopCenter, new Vector2(0f, -168f), new Vector2(1040f, 470f));
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.clear; // catches the scroll wheel
            var page = H.Rect("PageTemplate", viewport, H.TopCenter, H.TopCenter, Vector2.zero, new Vector2(0f, 470f));
            page.anchorMin = new Vector2(0f, 1f);
            page.anchorMax = Vector2.one;
            page.sizeDelta = new Vector2(0f, 470f);
            var pageLayout = page.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            pageLayout.spacing = 4f;
            pageLayout.childControlWidth = pageLayout.childControlHeight = true;
            pageLayout.childForceExpandWidth = true;
            pageLayout.childForceExpandHeight = false;
            page.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.content = page;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;

            // Row templates (kept outside the page template so pages don't clone them).
            var templates = H.Rect("Templates", panel, H.Center, H.Center, Vector2.zero, new Vector2(1040f, 56f));
            templates.gameObject.SetActive(false);
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // Choice: label  < value >
            var choice = Row(templates, theme, "ChoiceTemplate", out var choiceLabel);
            var previous = H.Button(choice, "Previous", "<", 0f);
            Place(previous, new Vector2(1f, 0.5f), new Vector2(-340f, 0f), new Vector2(44f, 40f));
            var value = H.Text(H.Rect("Value", choice, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-62f, 0f), new Vector2(272f, 40f)), null,
                "Valeur", 21f, HudFont.Body, HudColor.Accent, TextAlignmentOptions.Center);
            var next = H.Button(choice, "Next", ">", 0f);
            Place(next, new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(44f, 40f));
            var choiceRow = choice.gameObject.AddComponent<OptionRow>();
            H.Props(choiceRow, so =>
            {
                so.FindProperty("label").objectReferenceValue = choiceLabel;
                so.FindProperty("valueLabel").objectReferenceValue = value;
                so.FindProperty("previousButton").objectReferenceValue = previous;
                so.FindProperty("nextButton").objectReferenceValue = next;
            });

            // Slider: label  ━━━●━━  value
            var sliderRowRect = Row(templates, theme, "SliderTemplate", out var sliderLabel);
            var sliderRect = H.Rect("Slider", sliderRowRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-112f, 0f), new Vector2(300f, 28f));
            var track = H.Rect("Background", sliderRect, H.Center, H.Center, Vector2.zero, new Vector2(0f, 6f));
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.sizeDelta = new Vector2(0f, 6f);
            H.Themed(track, null, HudColor.BarBackground, raycast: true);
            var fillArea = H.Stretch("FillArea", track);
            var fill = H.Stretch("Fill", fillArea);
            H.Themed(fill, null, HudColor.Accent);
            var handleArea = H.Stretch("HandleArea", sliderRect);
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);
            var handle = H.Rect("Handle", handleArea, H.Center, H.Center, Vector2.zero, new Vector2(22f, 22f));
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.sizeDelta = new Vector2(22f, -6f); // the slider drives the x anchors; height = area - 6
            var handleImage = H.Themed(handle, null, HudColor.Text, raycast: true);
            handleImage.sprite = knob;
            var slider = sliderRect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            var colors = slider.colors;
            colors.highlightedColor = colors.selectedColor = theme.accentColor;
            colors.pressedColor = theme.accentColor * 0.85f;
            slider.colors = colors;
            var sliderValue = H.Text(H.Rect("Value", sliderRowRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(88f, 40f)), null,
                "100%", 21f, HudFont.Body, HudColor.Accent, TextAlignmentOptions.MidlineRight);
            var sliderRow = sliderRowRect.gameObject.AddComponent<OptionSliderRow>();
            H.Props(sliderRow, so =>
            {
                so.FindProperty("label").objectReferenceValue = sliderLabel;
                so.FindProperty("valueLabel").objectReferenceValue = sliderValue;
                so.FindProperty("slider").objectReferenceValue = slider;
            });

            // Switch: label  Activé (●  )
            var toggleRowRect = Row(templates, theme, "ToggleTemplate", out var toggleLabel);
            var switchRect = H.Rect("Switch", toggleRowRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(60f, 30f));
            var switchImage = switchRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            switchImage.color = theme.barBackgroundColor;
            var switchButton = switchRect.gameObject.AddComponent<UnityEngine.UI.Button>();
            switchButton.targetGraphic = switchImage;
            switchButton.transition = UnityEngine.UI.Selectable.Transition.None;
            var switchKnob = H.Rect("Knob", switchRect, H.Center, H.Center, Vector2.zero, new Vector2(24f, 24f));
            H.Themed(switchKnob, null, HudColor.Text).sprite = knob;
            var state = H.Text(H.Rect("State", toggleRowRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-90f, 0f), new Vector2(180f, 40f)), null,
                "Activé", 18f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.MidlineRight);
            var toggleRow = toggleRowRect.gameObject.AddComponent<OptionToggleRow>();
            H.Props(toggleRow, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("label").objectReferenceValue = toggleLabel;
                so.FindProperty("stateLabel").objectReferenceValue = state;
                so.FindProperty("button").objectReferenceValue = switchButton;
                so.FindProperty("track").objectReferenceValue = switchImage;
                so.FindProperty("knob").objectReferenceValue = switchKnob;
                so.FindProperty("knobTravel").floatValue = 30f;
            });

            Line(panel, -650f);
            var hint = H.Text(H.Rect("Hint", panel, H.TopLeft, H.TopLeft, new Vector2(48f, -660f), new Vector2(1024f, 60f)), null,
                string.Empty, 18f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.TopLeft);

            var reset = MenuEntry(panel, theme, "ResetButton", "Réinitialiser", 24f, 52f);
            Place(reset, H.BottomLeft, new Vector2(36f, 26f), new Vector2(460f, 52f));
            var back = MenuEntry(panel, theme, "BackButton", "Retour", 24f, 52f);
            Place(back, H.BottomRight, new Vector2(-36f, 26f), new Vector2(220f, 52f));

            var menu = panel.gameObject.AddComponent<OptionsMenu>();
            H.Props(menu, so =>
            {
                so.FindProperty("tabTemplate").objectReferenceValue = tabTemplate;
                so.FindProperty("scroll").objectReferenceValue = scroll;
                so.FindProperty("pageTemplate").objectReferenceValue = page;
                so.FindProperty("choiceTemplate").objectReferenceValue = choiceRow;
                so.FindProperty("sliderTemplate").objectReferenceValue = sliderRow;
                so.FindProperty("toggleTemplate").objectReferenceValue = toggleRow;
                so.FindProperty("hintLabel").objectReferenceValue = hint;
                so.FindProperty("resetButton").objectReferenceValue = reset;
                so.FindProperty("resetLabel").objectReferenceValue = reset.GetComponent<MenuButton>().Text;
                so.FindProperty("backButton").objectReferenceValue = back;
            });
            panel.gameObject.SetActive(false);
            return menu;
        }

        /// <summary>Option row: hover highlight + label on the left; the control is added by the caller on the right.</summary>
        private static RectTransform Row(RectTransform parent, HudTheme theme, string name, out TextMeshProUGUI label)
        {
            var row = H.Rect(name, parent, H.Center, H.Center, Vector2.zero, new Vector2(1040f, 56f));
            H.LayoutHeight(row, 56f);
            var highlight = H.Stretch("Highlight", row).gameObject.AddComponent<UnityEngine.UI.Image>();
            var color = theme.textColor;
            color.a = 0f;
            highlight.color = color;
            highlight.raycastTarget = true; // hover anywhere on the row
            label = H.Text(H.Rect("Label", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(520f, 40f)), null,
                "Option", 22f, HudFont.Body, HudColor.Text, TextAlignmentOptions.MidlineLeft);
            var hint = row.gameObject.AddComponent<OptionHint>();
            H.Props(hint, so => so.FindProperty("highlight").objectReferenceValue = highlight);
            return row;
        }

        private static void Line(RectTransform panel, float y)
        {
            var line = H.Rect("Separator", panel, H.TopCenter, H.TopCenter, new Vector2(0f, y), new Vector2(1040f, 1f));
            H.Themed(line, null, HudColor.Frame);
        }

        /// <summary>
        /// Text-only menu entry (MenuButton): invisible hit area, label that slides on focus, accent bar on the left
        /// (or underline for tabs).
        /// </summary>
        internal static UnityEngine.UI.Button MenuEntry(RectTransform parent, HudTheme theme, string name, string label, float fontSize, float height,
            bool underline = false)
        {
            var rect = H.Rect(name, parent, H.Center, H.Center, Vector2.zero, new Vector2(0f, height));
            H.LayoutHeight(rect, height);
            var hit = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = hit;
            button.transition = UnityEngine.UI.Selectable.Transition.None;

            RectTransform bar;
            if (underline)
            {
                bar = H.Rect("Accent", rect, H.BottomCenter, H.BottomCenter, Vector2.zero, new Vector2(0f, 3f));
                bar.anchorMin = Vector2.zero;
                bar.anchorMax = new Vector2(1f, 0f);
                bar.sizeDelta = new Vector2(0f, 3f);
            }
            else
                bar = H.Rect("Accent", rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, height * 0.6f));
            H.Themed(bar, null, HudColor.Accent);

            var labelRect = H.Stretch("Label", rect);
            if (!underline)
                labelRect.offsetMin = new Vector2(22f, 0f);
            var text = H.Text(labelRect, null, label, fontSize, HudFont.Title, HudColor.Text,
                underline ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft);

            var entry = rect.gameObject.AddComponent<MenuButton>();
            H.Props(entry, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("label").objectReferenceValue = labelRect;
                so.FindProperty("text").objectReferenceValue = text;
                so.FindProperty("accentBar").objectReferenceValue = bar;
                so.FindProperty("slide").floatValue = underline ? 0f : 14f;
            });
            return button;
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
