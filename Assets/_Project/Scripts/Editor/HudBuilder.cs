using System.IO;
using RiskOfMagic.Core;
using RiskOfMagic.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace RiskOfMagic.Editor
{
    /// <summary>
    /// Builds Assets/_Project/UI/HUD.prefab (uGUI + TextMeshPro) and the HudTheme asset.
    /// The theme is never overwritten: it is where real art is plugged in.
    /// The prefab is only rebuilt on explicit request, so layout tweaks made by hand are kept.
    /// </summary>
    public static class HudBuilder
    {
        public const string UiFolder = "Assets/_Project/UI";
        public const string ThemePath = UiFolder + "/HudTheme.asset";
        public const string PrefabPath = UiFolder + "/HUD.prefab";

        private static HudTheme theme;

        [MenuItem("Tools/Risk of Magic/Rebuild HUD Prefab")]
        private static void RebuildFromMenu()
        {
            if (!EnsureTextMeshPro())
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
                !EditorUtility.DisplayDialog("Rebuild HUD", "Overwrite HUD.prefab? Manual layout changes will be lost (the HudTheme is kept).", "Rebuild", "Cancel"))
                return;
            Build();
        }

        /// <summary>False while TMP Essentials are being imported (run the tool again afterwards).</summary>
        public static bool EnsureTextMeshPro()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null)
                return true;
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            Debug.LogWarning("[Risk of Magic] Importing TextMeshPro Essentials. Run the tool again once the import is done.");
            return false;
        }

        public static GameObject GetOrBuild()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return existing != null ? existing : Build();
        }

        public static GameObject Build()
        {
            Directory.CreateDirectory(UiFolder);
            AssetDatabase.Refresh();
            theme = LoadOrCreateTheme();

            var root = new GameObject("HUD", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var controller = root.AddComponent<HudController>();
            var rootRect = (RectTransform)root.transform;

            var worldLayer = Stretch("WorldLayer", rootRect);
            BuildPickupLabels(worldLayer);
            BuildDamageNumbers(worldLayer);

            var gameplay = Stretch("Gameplay", rootRect);
            var gameplayGroup = gameplay.gameObject.AddComponent<CanvasGroup>();
            gameplayGroup.interactable = false;
            gameplayGroup.blocksRaycasts = false;
            Props(controller, so => so.FindProperty("gameplayGroup").objectReferenceValue = gameplayGroup);

            BuildCrosshair(gameplay);
            BuildChargeBar(gameplay);
            BuildInteractionPrompt(gameplay);
            BuildCharacterPanel(gameplay);
            BuildSkillBar(gameplay);
            BuildInventory(gameplay);
            BuildRunInfo(gameplay);
            BuildNotification(gameplay);

            BuildDeathOverlay(rootRect);
            BuildConnectionMenu(rootRect);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[Risk of Magic] HUD prefab built at {PrefabPath}. Plug your art in {ThemePath}.");
            return prefab;
        }

        private static HudTheme LoadOrCreateTheme()
        {
            var existing = AssetDatabase.LoadAssetAtPath<HudTheme>(ThemePath);
            if (existing != null)
                return existing;

            var created = ScriptableObject.CreateInstance<HudTheme>();
            // Unity built-in placeholders: replace them with real art in the asset.
            created.panel = BuiltinSprite("UI/Skin/Background.psd");
            created.frame = BuiltinSprite("UI/Skin/UISprite.psd");
            created.slotFrame = BuiltinSprite("UI/Skin/UISprite.psd");
            created.button = BuiltinSprite("UI/Skin/UISprite.psd");
            created.inputField = BuiltinSprite("UI/Skin/InputFieldBackground.psd");
            created.crosshair = BuiltinSprite("UI/Skin/Knob.psd");
            AssetDatabase.CreateAsset(created, ThemePath);
            return created;
        }

        private static Sprite BuiltinSprite(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        // ---------------------------------------------------------------- Sections

        private static void BuildCrosshair(RectTransform parent)
        {
            var crosshair = Rect("Crosshair", parent, Center, Center, Vector2.zero, new Vector2(14f, 14f));
            Themed(crosshair, HudSprite.Crosshair, HudColor.Text);
        }

        private static void BuildChargeBar(RectTransform parent)
        {
            var bar = Rect("ChargeBar", parent, Center, Center, new Vector2(0f, -48f), new Vector2(220f, 10f));
            var group = bar.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            Themed(bar, HudSprite.BarBackground, HudColor.BarBackground);
            var fill = Stretch("Fill", bar);
            Themed(fill, HudSprite.BarFill, HudColor.Charge);
            var widget = bar.gameObject.AddComponent<ChargeBarWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("group").objectReferenceValue = group;
                so.FindProperty("fill").objectReferenceValue = fill;
            });
        }

        private static void BuildInteractionPrompt(RectTransform parent)
        {
            var prompt = Rect("InteractionPrompt", parent, Center, Center, new Vector2(0f, -110f), new Vector2(720f, 46f));
            var group = prompt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            Themed(prompt, HudSprite.Panel, HudColor.Panel);
            var label = Text(prompt, "Label", "[E] Interagir", 24f, HudFont.Body, HudColor.Text, TextAlignmentOptions.Center);
            var widget = prompt.gameObject.AddComponent<InteractionPromptWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("group").objectReferenceValue = group;
                so.FindProperty("label").objectReferenceValue = label;
            });
        }

        private static void BuildCharacterPanel(RectTransform parent)
        {
            var panel = Rect("CharacterPanel", parent, BottomLeft, BottomLeft, new Vector2(30f, 30f), new Vector2(490f, 140f));
            Themed(panel, HudSprite.Panel, HudColor.Panel);

            var portraitFrame = Rect("PortraitFrame", panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(116f, 116f));
            Themed(portraitFrame, HudSprite.Frame, HudColor.Frame);
            var portrait = Stretch("Portrait", portraitFrame, 5f);
            var portraitImage = PlainImage(portrait);

            var nameLabel = Text(Rect("Name", panel, TopLeft, TopLeft, new Vector2(142f, -8f), new Vector2(330f, 34f)), null,
                "Personnage", 26f, HudFont.Title, HudColor.Accent, TextAlignmentOptions.Left);
            var character = panel.gameObject.AddComponent<CharacterWidget>();
            Props(character, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("portrait").objectReferenceValue = portraitImage;
                so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            });

            // Health
            var health = Rect("HealthBar", panel, BottomLeft, BottomLeft, new Vector2(142f, 50f), new Vector2(330f, 32f));
            Themed(health, HudSprite.BarBackground, HudColor.BarBackground);
            var trail = Stretch("Trail", health);
            Themed(trail, HudSprite.BarFill, HudColor.HealthTrail);
            var fill = Stretch("Fill", health);
            var fillImage = Themed(fill, HudSprite.BarFill, HudColor.Health, applyColor: false);
            fillImage.color = theme.healthColor;
            var healthLabel = Text(health, "Label", "100 / 100", 20f, HudFont.Numbers, HudColor.Text, TextAlignmentOptions.Center);
            var healthWidget = health.gameObject.AddComponent<HealthBarWidget>();
            Props(healthWidget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("fill").objectReferenceValue = fill;
                so.FindProperty("fillImage").objectReferenceValue = fillImage;
                so.FindProperty("trail").objectReferenceValue = trail;
                so.FindProperty("label").objectReferenceValue = healthLabel;
            });

            // Gold
            var gold = Rect("Gold", panel, BottomLeft, BottomLeft, new Vector2(142f, 12f), new Vector2(330f, 30f));
            var goldIcon = Rect("Icon", gold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            Themed(goldIcon, HudSprite.GoldIcon, HudColor.Gold);
            var goldLabel = Text(Rect("Amount", gold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(290f, 30f)), null,
                "0", 24f, HudFont.Numbers, HudColor.Gold, TextAlignmentOptions.Left);
            var goldWidget = gold.gameObject.AddComponent<GoldWidget>();
            Props(goldWidget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("label").objectReferenceValue = goldLabel;
            });
        }

        private static void BuildSkillBar(RectTransform parent)
        {
            const float size = 100f;
            const float spacing = 12f;
            string[] keys = { "Clic G", "Clic D", "Maj", "R" };

            var bar = Rect("SkillBar", parent, BottomCenter, BottomCenter, new Vector2(0f, 40f), new Vector2(size * 4f + spacing * 3f, size));
            var slots = new SkillSlotWidget[4];
            for (int i = 0; i < 4; i++)
            {
                float x = -1.5f * (size + spacing) + i * (size + spacing);
                var slot = Rect($"Slot_{(SkillSlot)i}", bar, Center, Center, new Vector2(x, 0f), new Vector2(size, size));
                Themed(slot, HudSprite.SlotFrame, HudColor.Frame);
                var icon = PlainImage(Stretch("Icon", slot, 6f));
                var placeholder = Text(Stretch("Name", slot, 8f), null, "Compétence", 16f, HudFont.Body, HudColor.Text, TextAlignmentOptions.Center);
                var overlay = Stretch("CooldownOverlay", slot, 6f);
                Themed(overlay, HudSprite.CooldownOverlay, HudColor.CooldownOverlay);
                var cooldown = Text(slot, "Cooldown", string.Empty, 36f, HudFont.Numbers, HudColor.Text, TextAlignmentOptions.Center);
                var stock = Text(Rect("Stock", slot, BottomRight, BottomRight, new Vector2(-6f, 4f), new Vector2(40f, 26f)), null,
                    string.Empty, 22f, HudFont.Numbers, HudColor.Accent, TextAlignmentOptions.BottomRight);
                var key = Text(Rect("Key", slot, BottomCenter, TopCenter, new Vector2(0f, -4f), new Vector2(size, 24f)), null,
                    keys[i], 18f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.Center);

                var widget = slot.gameObject.AddComponent<SkillSlotWidget>();
                int index = i;
                Props(widget, so =>
                {
                    so.FindProperty("theme").objectReferenceValue = theme;
                    so.FindProperty("slot").enumValueIndex = index;
                    so.FindProperty("icon").objectReferenceValue = icon;
                    so.FindProperty("placeholderName").objectReferenceValue = placeholder;
                    so.FindProperty("cooldownOverlay").objectReferenceValue = overlay;
                    so.FindProperty("cooldownLabel").objectReferenceValue = cooldown;
                    so.FindProperty("stockLabel").objectReferenceValue = stock;
                    so.FindProperty("keyLabel").objectReferenceValue = key;
                    so.FindProperty("keyText").stringValue = keys[index];
                });
                slots[i] = widget;
            }

            var barWidget = bar.gameObject.AddComponent<SkillBarWidget>();
            Props(barWidget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                var array = so.FindProperty("slots");
                array.arraySize = slots.Length;
                for (int i = 0; i < slots.Length; i++)
                    array.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            });
        }

        private static void BuildInventory(RectTransform parent)
        {
            const int columns = 8;
            const float cell = 58f;
            const float spacing = 6f;

            var inventory = Rect("Inventory", parent, BottomRight, BottomRight, new Vector2(-30f, 30f), new Vector2(columns * cell + (columns - 1) * spacing, 320f));
            var grid = inventory.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.cellSize = new Vector2(cell, cell);
            grid.spacing = new Vector2(spacing, spacing);
            grid.startCorner = UnityEngine.UI.GridLayoutGroup.Corner.LowerRight;
            grid.startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.LowerRight;
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;

            var template = Rect("ItemSlotTemplate", inventory, Center, Center, Vector2.zero, new Vector2(cell, cell));
            var frame = Themed(template, HudSprite.SlotFrame, HudColor.Frame, applyColor: false);
            var icon = PlainImage(Stretch("Icon", template, 4f));
            var placeholder = Text(Stretch("Name", template, 3f), null, "Objet", 11f, HudFont.Body, HudColor.Text, TextAlignmentOptions.Center);
            var count = Text(Rect("Count", template, BottomRight, BottomRight, new Vector2(-3f, 1f), new Vector2(40f, 20f)), null,
                string.Empty, 16f, HudFont.Numbers, HudColor.Text, TextAlignmentOptions.BottomRight);
            var slot = template.gameObject.AddComponent<ItemSlotWidget>();
            Props(slot, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("frame").objectReferenceValue = frame;
                so.FindProperty("icon").objectReferenceValue = icon;
                so.FindProperty("placeholderName").objectReferenceValue = placeholder;
                so.FindProperty("countLabel").objectReferenceValue = count;
            });
            template.gameObject.SetActive(false);

            var widget = inventory.gameObject.AddComponent<InventoryWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("slotTemplate").objectReferenceValue = slot;
                so.FindProperty("container").objectReferenceValue = inventory;
            });
        }

        private static void BuildRunInfo(RectTransform parent)
        {
            var panel = Rect("RunInfo", parent, TopRight, TopRight, new Vector2(-30f, -30f), new Vector2(380f, 90f));
            Themed(panel, HudSprite.Panel, HudColor.Panel);
            var timer = Text(Rect("Timer", panel, TopCenter, TopCenter, new Vector2(0f, -6f), new Vector2(360f, 46f)), null,
                "00:00", 40f, HudFont.Numbers, HudColor.Text, TextAlignmentOptions.Center);
            var details = Text(Rect("Details", panel, BottomCenter, BottomCenter, new Vector2(0f, 8f), new Vector2(360f, 28f)), null,
                "Normal", 18f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.Center);
            var widget = panel.gameObject.AddComponent<RunInfoWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("timerLabel").objectReferenceValue = timer;
                so.FindProperty("detailsLabel").objectReferenceValue = details;
            });
        }

        private static void BuildNotification(RectTransform parent)
        {
            var panel = Rect("ItemNotification", parent, TopCenter, TopCenter, new Vector2(0f, -120f), new Vector2(660f, 112f));
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            Themed(panel, HudSprite.Panel, HudColor.Panel);

            var frame = Rect("IconFrame", panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(90f, 90f));
            var frameImage = Themed(frame, HudSprite.SlotFrame, HudColor.Frame, applyColor: false);
            var icon = PlainImage(Stretch("Icon", frame, 5f));
            var title = Text(Rect("Title", panel, TopLeft, TopLeft, new Vector2(116f, -10f), new Vector2(530f, 36f)), null,
                "Objet", 28f, HudFont.Title, HudColor.Text, TextAlignmentOptions.Left);
            var description = Text(Rect("Description", panel, TopLeft, TopLeft, new Vector2(116f, -48f), new Vector2(530f, 56f)), null,
                "Description", 19f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.TopLeft);

            var widget = panel.gameObject.AddComponent<ItemNotificationWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("group").objectReferenceValue = group;
                so.FindProperty("frame").objectReferenceValue = frameImage;
                so.FindProperty("icon").objectReferenceValue = icon;
                so.FindProperty("titleLabel").objectReferenceValue = title;
                so.FindProperty("descriptionLabel").objectReferenceValue = description;
            });
        }

        private static void BuildDamageNumbers(RectTransform parent)
        {
            var layer = Stretch("DamageNumbers", parent);
            var template = Text(Rect("Template", layer, Center, Center, Vector2.zero, new Vector2(300f, 70f)), null,
                "0", 34f, HudFont.Numbers, HudColor.Text, TextAlignmentOptions.Center);
            template.fontStyle = FontStyles.Bold;
            var widget = layer.gameObject.AddComponent<DamageNumbersWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("template").objectReferenceValue = template;
            });
        }

        private static void BuildPickupLabels(RectTransform parent)
        {
            var layer = Stretch("PickupLabels", parent);
            var template = Text(Rect("Template", layer, Center, Center, Vector2.zero, new Vector2(400f, 40f)), null,
                "Objet", 22f, HudFont.Body, HudColor.Text, TextAlignmentOptions.Center);
            template.fontStyle = FontStyles.Bold;
            var widget = layer.gameObject.AddComponent<PickupLabelsWidget>();
            Props(widget, so => so.FindProperty("template").objectReferenceValue = template);
        }

        private static void BuildDeathOverlay(RectTransform parent)
        {
            var overlay = Stretch("DeathOverlay", parent);
            var group = overlay.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            Themed(overlay, null, HudColor.Overlay);
            Text(overlay, "Label", "VOUS ÊTES MORT", 72f, HudFont.Title, HudColor.Danger, TextAlignmentOptions.Center);
            var widget = overlay.gameObject.AddComponent<DeathOverlayWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("group").objectReferenceValue = group;
            });
        }

        private static void BuildConnectionMenu(RectTransform parent)
        {
            var menu = Rect("ConnectionMenu", parent, TopLeft, TopLeft, new Vector2(30f, -30f), new Vector2(400f, 640f));
            var group = menu.gameObject.AddComponent<CanvasGroup>();
            Themed(menu, HudSprite.Panel, HudColor.Panel);
            VerticalLayout(menu.gameObject, 18, 10);

            LayoutHeight(Text(menu, "Title", "RISK OF MAGIC", 32f, HudFont.Title, HudColor.Accent, TextAlignmentOptions.Center, stretch: false).rectTransform, 48f);
            LayoutHeight(Text(menu, "CharacterHeader", "Personnage", 20f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.Left, stretch: false).rectTransform, 26f);

            var characters = Rect("Characters", menu, Center, Center, Vector2.zero, new Vector2(0f, 48f));
            LayoutHeight(characters, 48f);
            var row = characters.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.spacing = 8f;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            var characterTemplate = Button(characters, "CharacterTemplate", "Personnage", 0f);

            // Connect section
            var connect = Rect("ConnectSection", menu, Center, Center, Vector2.zero, new Vector2(0f, 300f));
            VerticalLayout(connect.gameObject, 0, 8);
            LayoutHeight(Text(connect, "AddressLabel", "Adresse", 18f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.Left, stretch: false).rectTransform, 24f);
            var address = InputField(connect, "Address", "127.0.0.1");
            LayoutHeight(Text(connect, "PortLabel", "Port", 18f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.Left, stretch: false).rectTransform, 24f);
            var port = InputField(connect, "Port", "7777");
            var host = Button(connect, "HostButton", "Héberger", 48f);
            var client = Button(connect, "ClientButton", "Rejoindre", 48f);
            var server = Button(connect, "ServerButton", "Serveur dédié", 48f);

            // Session section
            var session = Rect("SessionSection", menu, Center, Center, Vector2.zero, new Vector2(0f, 200f));
            VerticalLayout(session.gameObject, 0, 8);
            var status = Text(session, "Status", "Connecté", 20f, HudFont.Body, HudColor.Text, TextAlignmentOptions.Left, stretch: false);
            LayoutHeight(status.rectTransform, 60f);
            var forceWave = Button(session, "ForceWaveButton", "Forcer une vague", 48f);
            var disconnect = Button(session, "DisconnectButton", "Déconnexion", 48f);
            session.gameObject.SetActive(false);

            var widget = menu.gameObject.AddComponent<ConnectionMenu>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("group").objectReferenceValue = group;
                so.FindProperty("connectSection").objectReferenceValue = connect.gameObject;
                so.FindProperty("addressField").objectReferenceValue = address;
                so.FindProperty("portField").objectReferenceValue = port;
                so.FindProperty("hostButton").objectReferenceValue = host;
                so.FindProperty("clientButton").objectReferenceValue = client;
                so.FindProperty("serverButton").objectReferenceValue = server;
                so.FindProperty("sessionSection").objectReferenceValue = session.gameObject;
                so.FindProperty("statusLabel").objectReferenceValue = status;
                so.FindProperty("forceWaveButton").objectReferenceValue = forceWave;
                so.FindProperty("disconnectButton").objectReferenceValue = disconnect;
                so.FindProperty("characterButtonTemplate").objectReferenceValue = characterTemplate;
            });
        }

        // ---------------------------------------------------------------- Helpers

        private static readonly Vector2 Center = new(0.5f, 0.5f);
        private static readonly Vector2 TopLeft = new(0f, 1f);
        private static readonly Vector2 TopCenter = new(0.5f, 1f);
        private static readonly Vector2 TopRight = new(1f, 1f);
        private static readonly Vector2 BottomLeft = new(0f, 0f);
        private static readonly Vector2 BottomCenter = new(0.5f, 0f);
        private static readonly Vector2 BottomRight = new(1f, 0f);

        private static RectTransform Rect(string name, RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Stretch(string name, RectTransform parent, float inset = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Center;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        private static UnityEngine.UI.Image Themed(RectTransform rect, HudSprite? sprite, HudColor color, bool applyColor = true, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = raycast;
            var themed = rect.gameObject.AddComponent<ThemedImage>();
            Props(themed, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("applySprite").boolValue = sprite.HasValue;
                so.FindProperty("sprite").enumValueIndex = (int)(sprite ?? HudSprite.Panel);
                so.FindProperty("applyColor").boolValue = applyColor;
                so.FindProperty("color").enumValueIndex = (int)color;
            });
            themed.Apply();
            return image;
        }

        /// <summary>Image filled at runtime with game data (skill/item icons, portraits).</summary>
        private static UnityEngine.UI.Image PlainImage(RectTransform rect)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.color = new Color(1f, 1f, 1f, 0.08f);
            return image;
        }

        /// <summary>Adds a themed TMP label. With name == null, the label goes on <paramref name="parent"/> itself.</summary>
        private static TextMeshProUGUI Text(RectTransform parent, string name, string value, float size, HudFont font, HudColor color,
            TextAlignmentOptions alignment, bool stretch = true)
        {
            var rect = name == null ? parent : stretch ? Stretch(name, parent) : Rect(name, parent, Center, Center, Vector2.zero, new Vector2(0f, size + 8f));
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.raycastTarget = false;
            var themed = rect.gameObject.AddComponent<ThemedText>();
            Props(themed, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("font").enumValueIndex = (int)font;
                so.FindProperty("color").enumValueIndex = (int)color;
            });
            themed.Apply();
            return text;
        }

        private static UnityEngine.UI.Button Button(RectTransform parent, string name, string label, float height)
        {
            var rect = Rect(name, parent, Center, Center, Vector2.zero, new Vector2(0f, height));
            if (height > 0f)
                LayoutHeight(rect, height);
            var image = Themed(rect, HudSprite.Button, HudColor.Button, raycast: true);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            Text(rect, "Label", label, 22f, HudFont.Body, HudColor.Text, TextAlignmentOptions.Center);
            return button;
        }

        private static TMP_InputField InputField(RectTransform parent, string name, string value)
        {
            var rect = Rect(name, parent, Center, Center, Vector2.zero, new Vector2(0f, 42f));
            LayoutHeight(rect, 42f);
            var image = Themed(rect, HudSprite.InputField, HudColor.BarBackground, raycast: true);
            var area = Stretch("TextArea", rect, 8f);
            area.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var placeholder = Text(area, "Placeholder", value, 20f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.MidlineLeft);
            placeholder.fontStyle = FontStyles.Italic;
            var text = Text(area, "Text", string.Empty, 20f, HudFont.Body, HudColor.Text, TextAlignmentOptions.MidlineLeft);

            var field = rect.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = image;
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.text = value;
            return field;
        }

        private static void VerticalLayout(GameObject go, int padding, float spacing)
        {
            var layout = go.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
        }

        private static void LayoutHeight(RectTransform rect, float height)
        {
            var element = rect.GetComponent<UnityEngine.UI.LayoutElement>();
            if (element == null)
                element = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
        }

        private static void Props(Object target, System.Action<SerializedObject> edit)
        {
            var so = new SerializedObject(target);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
