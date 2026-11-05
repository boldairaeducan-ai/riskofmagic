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
            var home = H.Rect("Home", root, H.Center, H.Center, new Vector2(0f, -40f), new Vector2(460f, 590f));
            H.Themed(home, HudSprite.Panel, HudColor.Panel);
            H.VerticalLayout(home.gameObject, 24, 14);
            var solo = H.Button(home, "SoloButton", "Solo", 60f);
            var host = H.Button(home, "HostButton", "Créer une partie en ligne", 60f);
            H.LayoutHeight(H.Text(home, "JoinHeader", "Rejoindre avec un code", 20f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.Left, stretch: false).rectTransform, 28f);
            var codeField = H.InputField(home, "JoinCode", "ABC123");
            codeField.text = string.Empty;
            codeField.characterLimit = 12;
            var join = H.Button(home, "JoinButton", "Rejoindre", 56f);
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

            var playersPanel = Panel(lobby, "Players", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(60f, -10f), new Vector2(520f, 560f), "Joueurs");
            var players = H.Text(H.Rect("List", playersPanel, H.TopLeft, H.TopLeft, new Vector2(20f, -70f), new Vector2(480f, 470f)), null,
                "En attente du salon...", 22f, HudFont.Body, HudColor.Text, TextAlignmentOptions.TopLeft);

            var characterPanel = Panel(lobby, "Character", H.Center, H.Center, new Vector2(0f, -10f), new Vector2(700f, 560f), "Personnage");
            var characterRow = H.Rect("Characters", characterPanel, H.TopCenter, H.TopCenter, new Vector2(0f, -70f), new Vector2(660f, 56f));
            var row = characterRow.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.spacing = 8f;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            var characterTemplate = H.Button(characterRow, "CharacterTemplate", "Personnage", 0f);
            var characterInfo = H.Text(H.Rect("Description", characterPanel, H.TopLeft, H.TopLeft, new Vector2(20f, -140f), new Vector2(660f, 400f)), null,
                string.Empty, 19f, HudFont.Body, HudColor.Text, TextAlignmentOptions.TopLeft);

            var difficultyPanel = Panel(lobby, "Difficulty", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-60f, -10f), new Vector2(520f, 560f), "Difficulté");
            var normal = H.Button(difficultyPanel, "NormalButton", "Normal", 0f);
            Place(normal, H.TopCenter, new Vector2(-120f, -80f), new Vector2(220f, 56f));
            var hard = H.Button(difficultyPanel, "HardButton", "Difficile", 0f);
            Place(hard, H.TopCenter, new Vector2(120f, -80f), new Vector2(220f, 56f));
            var difficultyInfo = H.Text(H.Rect("Description", difficultyPanel, H.TopLeft, H.TopLeft, new Vector2(20f, -160f), new Vector2(480f, 380f)), null,
                string.Empty, 20f, HudFont.Body, HudColor.Text, TextAlignmentOptions.TopLeft);

            var main = H.Button(lobby, "MainButton", "Prêt", 0f);
            Place(main, H.BottomCenter, new Vector2(0f, 60f), new Vector2(380f, 72f));
            var mainLabel = main.GetComponentInChildren<TextMeshProUGUI>();
            mainLabel.fontSize = 28f;
            var leave = H.Button(lobby, "LeaveButton", "Quitter le salon", 0f);
            Place(leave, H.BottomLeft, new Vector2(60f, 60f), new Vector2(260f, 56f));
            lobby.gameObject.SetActive(false);

            var optionsMenu = BuildOptions(root);

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
                so.FindProperty("statusLabel").objectReferenceValue = status;
                so.FindProperty("lobbyScreen").objectReferenceValue = lobby.gameObject;
                so.FindProperty("codeLabel").objectReferenceValue = code;
                so.FindProperty("copyCodeButton").objectReferenceValue = copy;
                so.FindProperty("playersLabel").objectReferenceValue = players;
                so.FindProperty("characterButtonTemplate").objectReferenceValue = characterTemplate;
                so.FindProperty("characterDescription").objectReferenceValue = characterInfo;
                so.FindProperty("normalButton").objectReferenceValue = normal;
                so.FindProperty("hardButton").objectReferenceValue = hard;
                so.FindProperty("difficultyDescription").objectReferenceValue = difficultyInfo;
                so.FindProperty("mainButton").objectReferenceValue = main;
                so.FindProperty("mainButtonLabel").objectReferenceValue = mainLabel;
                so.FindProperty("leaveButton").objectReferenceValue = leave;
            });

            EditorSceneManager.SaveScene(scene, ScenePath);
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
