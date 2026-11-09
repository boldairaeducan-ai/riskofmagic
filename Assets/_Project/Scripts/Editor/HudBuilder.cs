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
            BuildLockOn(worldLayer);
            BuildEnemyStatusIcons(worldLayer);

            var gameplay = Stretch("Gameplay", rootRect);
            var gameplayGroup = gameplay.gameObject.AddComponent<CanvasGroup>();
            gameplayGroup.interactable = false;
            gameplayGroup.blocksRaycasts = false;
            Props(controller, so => so.FindProperty("gameplayGroup").objectReferenceValue = gameplayGroup);

            BuildShieldOverlay(gameplay);
            BuildCrosshair(gameplay);
            BuildHitMarker(gameplay);
            BuildDamageDirection(gameplay);
            BuildChargeBar(gameplay);
            BuildInteractionPrompt(gameplay);
            BuildCharacterPanel(gameplay);
            BuildStatusBar(gameplay);
            BuildSkillBar(gameplay);
            BuildEquipmentSlot(gameplay);
            BuildInventory(gameplay);
            BuildRunInfo(gameplay);
            BuildObjective(gameplay);
            BuildBossBar(gameplay);
            BuildNotification(gameplay);

            BuildDownedOverlay(rootRect);
            BuildDeathOverlay(rootRect);
            BuildGameOver(rootRect);
            BuildChoiceMenu(rootRect);
            BuildMerchantMenu(rootRect);
            BuildPauseMenu(rootRect);
            BuildConnectionMenu(rootRect);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[Risk of Magic] HUD prefab built at {PrefabPath}. Plug your art in {ThemePath}.");
            return prefab;
        }

        /// <summary>Loads the theme for other UI builders (main menu) using these helpers.</summary>
        internal static HudTheme UseTheme() => theme = LoadOrCreateTheme();

        private static HudTheme LoadOrCreateTheme()
        {
            var existing = AssetDatabase.LoadAssetAtPath<HudTheme>(ThemePath);
            if (existing != null)
            {
                // Slots added after the theme was created: give them a placeholder, never override user art.
                if (existing.lockOnMarker == null)
                {
                    existing.lockOnMarker = BuiltinSprite("UI/Skin/Knob.psd");
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }

            var created = ScriptableObject.CreateInstance<HudTheme>();
            // Unity built-in placeholders: replace them with real art in the asset.
            created.panel = BuiltinSprite("UI/Skin/Background.psd");
            created.frame = BuiltinSprite("UI/Skin/UISprite.psd");
            created.slotFrame = BuiltinSprite("UI/Skin/UISprite.psd");
            created.button = BuiltinSprite("UI/Skin/UISprite.psd");
            created.inputField = BuiltinSprite("UI/Skin/InputFieldBackground.psd");
            created.crosshair = BuiltinSprite("UI/Skin/Knob.psd");
            created.lockOnMarker = BuiltinSprite("UI/Skin/Knob.psd");
            AssetDatabase.CreateAsset(created, ThemePath);
            return created;
        }

        private static Sprite BuiltinSprite(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        // ---------------------------------------------------------------- Sections

        private static void BuildCrosshair(RectTransform parent)
        {
            var root = Stretch("CrosshairRoot", parent);
            var crosshair = Rect("Crosshair", root, Center, Center, Vector2.zero, new Vector2(14f, 14f));
            var image = Themed(crosshair, HudSprite.Crosshair, HudColor.Text, applyColor: false);
            image.color = theme.textColor;
            var label = Text(Rect("OutOfRange", root, Center, Center, new Vector2(0f, 26f), new Vector2(300f, 26f)), null,
                "Hors de portée", 18f, HudFont.Body, HudColor.Danger, TextAlignmentOptions.Center);
            label.gameObject.SetActive(false);

            var widget = root.gameObject.AddComponent<CrosshairWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("crosshair").objectReferenceValue = image;
                so.FindProperty("outOfRangeLabel").objectReferenceValue = label;
            });
        }

        private static void BuildHitMarker(RectTransform parent)
        {
            var root = Rect("HitMarker", parent, Center, Center, Vector2.zero, new Vector2(40f, 40f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var bars = new UnityEngine.UI.Graphic[4];
            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + 90f * i;
                var arm = Rect($"Arm_{i}", root, Center, Center, Vector2.zero, Vector2.zero);
                arm.localEulerAngles = new Vector3(0f, 0f, angle);
                var bar = Rect("Bar", arm, Center, new Vector2(0.5f, 0f), new Vector2(0f, 9f), new Vector2(3f, 11f));
                var image = bar.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.raycastTarget = false;
                bars[i] = image;
            }

            var widget = root.gameObject.AddComponent<HitMarkerWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("group").objectReferenceValue = group;
                var array = so.FindProperty("bars");
                array.arraySize = bars.Length;
                for (int i = 0; i < bars.Length; i++)
                    array.GetArrayElementAtIndex(i).objectReferenceValue = bars[i];
            });
        }

        private static void BuildDamageDirection(RectTransform parent)
        {
            var layer = Stretch("DamageDirection", parent);
            var template = Rect("IndicatorTemplate", layer, Center, Center, Vector2.zero, Vector2.zero);
            template.gameObject.AddComponent<CanvasGroup>();
            var arc = Rect("Arc", template, Center, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(130f, 14f));
            Themed(arc, HudSprite.BarFill, HudColor.Danger);

            var widget = layer.gameObject.AddComponent<DamageDirectionWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("template").objectReferenceValue = template;
            });
        }

        private static void BuildShieldOverlay(RectTransform parent)
        {
            var overlay = Stretch("ShieldOverlay", parent);
            var group = overlay.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var tint = Themed(overlay, null, HudColor.LockOn, applyColor: false);
            tint.color = new Color(0.6f, 0.35f, 1f, 0.18f);

            var widget = overlay.gameObject.AddComponent<ShieldOverlayWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("group").objectReferenceValue = group;
            });
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

            var nameLabel = Text(Rect("Name", panel, TopLeft, TopLeft, new Vector2(142f, -8f), new Vector2(220f, 34f)), null,
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

            // Experience: thin bar under the health bar, small level label at the top right (discreet).
            var xpBar = Rect("ExperienceBar", panel, BottomLeft, BottomLeft, new Vector2(142f, 44f), new Vector2(330f, 4f));
            Themed(xpBar, HudSprite.BarBackground, HudColor.BarBackground);
            var xpFill = Stretch("Fill", xpBar);
            Themed(xpFill, HudSprite.BarFill, HudColor.Accent);
            var levelLabel = Text(Rect("Level", panel, TopRight, TopRight, new Vector2(-12f, -12f), new Vector2(110f, 26f)), null,
                "Niv. 1", 18f, HudFont.Numbers, HudColor.TextMuted, TextAlignmentOptions.Right);
            var xpWidget = xpBar.gameObject.AddComponent<ExperienceBarWidget>();
            Props(xpWidget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("fill").objectReferenceValue = xpFill;
                so.FindProperty("levelLabel").objectReferenceValue = levelLabel;
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

        /// <summary>Active statuses of the local player, above the character panel (icons from ArtLibrary).</summary>
        private static void BuildStatusBar(RectTransform parent)
        {
            var bar = Rect("StatusBar", parent, BottomLeft, BottomLeft, new Vector2(30f, 180f), new Vector2(490f, 40f));
            var layout = bar.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var slot = Rect("SlotTemplate", bar, Center, Center, Vector2.zero, new Vector2(40f, 40f));
            Themed(slot, HudSprite.SlotFrame, HudColor.Accent);
            PlainImage(Stretch("Icon", slot, 4f));
            Text(slot, "Letter", "?", 20f, HudFont.Title, HudColor.Text, TextAlignmentOptions.Center);
            var timer = Stretch("Timer", slot, 4f);
            timer.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.55f);
            timer.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            var stacks = Text(Rect("Stacks", slot, BottomRight, BottomRight, new Vector2(-2f, 0f), new Vector2(36f, 18f)), null,
                string.Empty, 14f, HudFont.Numbers, HudColor.Text, TextAlignmentOptions.BottomRight);
            stacks.gameObject.name = "Stacks";

            var widget = bar.gameObject.AddComponent<StatusBarWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("slotTemplate").objectReferenceValue = slot;
            });
        }

        private static void BuildEquipmentSlot(RectTransform parent)
        {
            const float size = 100f;
            // Right of the skill bar (4 x 100 + 3 x 12 wide, centered).
            var slot = Rect("EquipmentSlot", parent, BottomCenter, BottomCenter, new Vector2(218f + 30f + size * 0.5f, 40f), new Vector2(size, size));
            var group = slot.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            Themed(slot, HudSprite.SlotFrame, HudColor.Frame);
            var icon = PlainImage(Stretch("Icon", slot, 6f));
            var placeholder = Text(Stretch("Name", slot, 8f), null, "Équipement", 16f, HudFont.Body, HudColor.Text, TextAlignmentOptions.Center);
            var overlay = Stretch("CooldownOverlay", slot, 6f);
            Themed(overlay, HudSprite.CooldownOverlay, HudColor.CooldownOverlay);
            var cooldown = Text(slot, "Cooldown", string.Empty, 36f, HudFont.Numbers, HudColor.Text, TextAlignmentOptions.Center);
            Text(Rect("Key", slot, BottomCenter, TopCenter, new Vector2(0f, -4f), new Vector2(size, 24f)), null,
                "F", 18f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.Center);

            var widget = slot.gameObject.AddComponent<EquipmentSlotWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("group").objectReferenceValue = group;
                so.FindProperty("icon").objectReferenceValue = icon;
                so.FindProperty("placeholderName").objectReferenceValue = placeholder;
                so.FindProperty("cooldownOverlay").objectReferenceValue = overlay;
                so.FindProperty("cooldownLabel").objectReferenceValue = cooldown;
            });
        }

        /// <summary>Card used by the choice and shop menus (laid out by a HorizontalLayoutGroup parent).</summary>
        private static OfferCardWidget OfferCard(RectTransform parent, string name)
        {
            var card = Rect(name, parent, Center, Center, Vector2.zero, new Vector2(230f, 360f));
            var element = card.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            element.preferredWidth = 230f;
            element.preferredHeight = 360f;
            var frame = Themed(card, HudSprite.SlotFrame, HudColor.Frame, applyColor: false);

            var iconRect = Rect("Icon", card, TopCenter, TopCenter, new Vector2(0f, -14f), new Vector2(110f, 110f));
            var icon = PlainImage(iconRect);
            var placeholder = Text(Stretch("Name", iconRect, 4f), null, "Objet", 15f, HudFont.Body, HudColor.Text, TextAlignmentOptions.Center);
            var title = Text(Rect("Title", card, TopCenter, TopCenter, new Vector2(0f, -132f), new Vector2(210f, 34f)), null,
                "Objet", 21f, HudFont.Title, HudColor.Text, TextAlignmentOptions.Center);
            var description = Text(Rect("Description", card, TopCenter, TopCenter, new Vector2(0f, -168f), new Vector2(206f, 120f)), null,
                "Description", 15f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.Top);

            var button = Button(card, "ActionButton", "Choisir", 0f);
            PlaceButton(button, new Vector2(0f, 12f));
            ((RectTransform)button.transform).sizeDelta = new Vector2(200f, 46f);
            var buttonLabel = button.GetComponentInChildren<TextMeshProUGUI>();

            var widget = card.gameObject.AddComponent<OfferCardWidget>();
            Props(widget, so =>
            {
                so.FindProperty("frame").objectReferenceValue = frame;
                so.FindProperty("icon").objectReferenceValue = icon;
                so.FindProperty("placeholderName").objectReferenceValue = placeholder;
                so.FindProperty("titleLabel").objectReferenceValue = title;
                so.FindProperty("descriptionLabel").objectReferenceValue = description;
                so.FindProperty("button").objectReferenceValue = button;
                so.FindProperty("buttonLabel").objectReferenceValue = buttonLabel;
            });
            return widget;
        }

        private static RectTransform CardRow(RectTransform parent, Vector2 position, Vector2 size)
        {
            var row = Rect("Cards", parent, Center, Center, position, size);
            var layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return row;
        }

        private static CanvasGroup MenuRoot(RectTransform parent, string name, Vector2 size, out RectTransform panel)
        {
            var screen = Stretch(name, parent);
            var group = screen.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            panel = Rect("Panel", screen, Center, Center, Vector2.zero, size);
            Themed(panel, HudSprite.Panel, HudColor.Panel);
            return group;
        }

        private static void BuildChoiceMenu(RectTransform parent)
        {
            var group = MenuRoot(parent, "ChoiceMenu", new Vector2(820f, 520f), out var panel);
            Text(Rect("Title", panel, TopCenter, TopCenter, new Vector2(0f, -16f), new Vector2(780f, 44f)), null,
                "Choisissez un objet", 32f, HudFont.Title, HudColor.Accent, TextAlignmentOptions.Center);
            var row = CardRow(panel, new Vector2(0f, 0f), new Vector2(780f, 360f));
            var template = OfferCard(row, "CardTemplate");
            var later = Button(panel, "LaterButton", "Plus tard", 0f);
            PlaceButton(later, new Vector2(0f, 14f));

            var widget = panel.parent.gameObject.AddComponent<ChoiceMenu>();
            Props(widget, so =>
            {
                so.FindProperty("group").objectReferenceValue = group;
                so.FindProperty("cardTemplate").objectReferenceValue = template;
                so.FindProperty("laterButton").objectReferenceValue = later;
            });
        }

        private static void BuildMerchantMenu(RectTransform parent)
        {
            var group = MenuRoot(parent, "MerchantMenu", new Vector2(1340f, 560f), out var panel);
            var title = Text(Rect("Title", panel, TopCenter, TopCenter, new Vector2(0f, -16f), new Vector2(800f, 44f)), null,
                "Marchand", 32f, HudFont.Title, HudColor.Accent, TextAlignmentOptions.Center);
            var gold = Text(Rect("Gold", panel, TopRight, TopRight, new Vector2(-24f, -20f), new Vector2(260f, 36f)), null,
                "Or : 0", 24f, HudFont.Numbers, HudColor.Gold, TextAlignmentOptions.Right);
            var row = CardRow(panel, new Vector2(0f, 10f), new Vector2(1300f, 360f));
            var template = OfferCard(row, "CardTemplate");

            var reroll = Button(panel, "RerollButton", "Relancer", 0f);
            PlaceButton(reroll, new Vector2(-140f, 16f));
            var close = Button(panel, "CloseButton", "Fermer", 0f);
            PlaceButton(close, new Vector2(140f, 16f));

            var widget = panel.parent.gameObject.AddComponent<MerchantMenu>();
            Props(widget, so =>
            {
                so.FindProperty("group").objectReferenceValue = group;
                so.FindProperty("titleLabel").objectReferenceValue = title;
                so.FindProperty("goldLabel").objectReferenceValue = gold;
                so.FindProperty("cardTemplate").objectReferenceValue = template;
                so.FindProperty("rerollButton").objectReferenceValue = reroll;
                so.FindProperty("rerollLabel").objectReferenceValue = reroll.GetComponentInChildren<TextMeshProUGUI>();
                so.FindProperty("closeButton").objectReferenceValue = close;
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

        private static void BuildObjective(RectTransform parent)
        {
            var root = Rect("Objective", parent, TopCenter, TopCenter, new Vector2(0f, -20f), new Vector2(640f, 74f));
            var label = Text(Rect("Label", root, TopCenter, TopCenter, Vector2.zero, new Vector2(640f, 56f)), null,
                "Trouvez et activez le téléporteur", 24f, HudFont.Title, HudColor.Text, TextAlignmentOptions.Top);

            var bar = Rect("ChargeBar", root, BottomCenter, BottomCenter, Vector2.zero, new Vector2(420f, 12f));
            var chargeGroup = bar.gameObject.AddComponent<CanvasGroup>();
            chargeGroup.alpha = 0f;
            Themed(bar, HudSprite.BarBackground, HudColor.BarBackground);
            var fill = Stretch("Fill", bar);
            Themed(fill, HudSprite.BarFill, HudColor.Accent);

            var widget = root.gameObject.AddComponent<ObjectiveWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("label").objectReferenceValue = label;
                so.FindProperty("chargeGroup").objectReferenceValue = chargeGroup;
                so.FindProperty("chargeFill").objectReferenceValue = fill;
            });
        }

        private static void BuildBossBar(RectTransform parent)
        {
            var root = Rect("BossBar", parent, TopCenter, TopCenter, new Vector2(0f, -112f), new Vector2(780f, 70f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var nameLabel = Text(Rect("Name", root, TopCenter, TopCenter, Vector2.zero, new Vector2(780f, 34f)), null,
                "Boss", 26f, HudFont.Title, HudColor.Danger, TextAlignmentOptions.Center);

            var bar = Rect("Bar", root, BottomCenter, BottomCenter, Vector2.zero, new Vector2(780f, 24f));
            Themed(bar, HudSprite.BarBackground, HudColor.BarBackground);
            var trail = Stretch("Trail", bar);
            Themed(trail, HudSprite.BarFill, HudColor.HealthTrail);
            var fill = Stretch("Fill", bar);
            Themed(fill, HudSprite.BarFill, HudColor.Danger);

            var widget = root.gameObject.AddComponent<BossBarWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("group").objectReferenceValue = group;
                so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
                so.FindProperty("fill").objectReferenceValue = fill;
                so.FindProperty("trail").objectReferenceValue = trail;
            });
        }

        private static void BuildNotification(RectTransform parent)
        {
            var panel = Rect("ItemNotification", parent, TopCenter, TopCenter, new Vector2(0f, -210f), new Vector2(660f, 112f));
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

        /// <summary>Elite / damage-over-time icons above enemies (ArtLibrary icons, colored initials otherwise).</summary>
        private static void BuildEnemyStatusIcons(RectTransform parent)
        {
            var layer = Stretch("EnemyStatusIcons", parent);
            var row = Rect("RowTemplate", layer, Center, Center, Vector2.zero, new Vector2(120f, 26f));
            row.pivot = new Vector2(0.5f, 0f);
            var layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            for (int i = 0; i < 4; i++)
            {
                var icon = Rect($"Icon{i}", row, Center, Center, Vector2.zero, new Vector2(24f, 24f));
                var image = icon.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                var letter = Text(icon, "Letter", string.Empty, 15f, HudFont.Title, HudColor.Text, TextAlignmentOptions.Center);
                letter.fontStyle = FontStyles.Bold;
            }
            var widget = layer.gameObject.AddComponent<EnemyStatusIconsWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("rowTemplate").objectReferenceValue = row;
            });
        }

        private static void BuildLockOn(RectTransform parent)
        {
            var layer = Stretch("LockOn", parent);

            var marker = Rect("LockMarkerTemplate", layer, Center, Center, Vector2.zero, new Vector2(46f, 46f));
            Themed(marker, HudSprite.LockOn, HudColor.LockOn);
            Text(Rect("Count", marker, BottomRight, TopLeft, new Vector2(-4f, 4f), new Vector2(60f, 28f)), null,
                string.Empty, 22f, HudFont.Numbers, HudColor.LockOn, TextAlignmentOptions.TopLeft);

            var next = Rect("NextTargetMarker", layer, Center, Center, Vector2.zero, new Vector2(30f, 30f));
            var nextImage = Themed(next, HudSprite.LockOn, HudColor.LockOn, applyColor: false);
            nextImage.color = new Color(1f, 1f, 1f, 0.35f);

            var counter = Text(Rect("Counter", layer, Center, Center, new Vector2(0f, -72f), new Vector2(420f, 30f)), null,
                string.Empty, 20f, HudFont.Body, HudColor.LockOn, TextAlignmentOptions.Center);

            var widget = layer.gameObject.AddComponent<LockOnWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("markerTemplate").objectReferenceValue = marker;
                so.FindProperty("nextTargetMarker").objectReferenceValue = next;
                so.FindProperty("counterLabel").objectReferenceValue = counter;
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

        private static void BuildDownedOverlay(RectTransform parent)
        {
            var overlay = Stretch("DownedOverlay", parent);
            var group = overlay.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var tint = Themed(overlay, null, HudColor.Overlay, applyColor: false);
            tint.color = new Color(0.55f, 0f, 0f, 0.5f);
            var label = Text(Rect("Label", overlay, Center, Center, new Vector2(0f, 120f), new Vector2(900f, 140f)), null,
                "À TERRE", 60f, HudFont.Title, HudColor.Danger, TextAlignmentOptions.Center);

            var revive = Rect("Revive", overlay, Center, Center, new Vector2(0f, 30f), new Vector2(420f, 16f));
            var reviveGroup = revive.gameObject.AddComponent<CanvasGroup>();
            reviveGroup.alpha = 0f;
            Themed(revive, HudSprite.BarBackground, HudColor.BarBackground);
            var fill = Stretch("Fill", revive);
            Themed(fill, HudSprite.BarFill, HudColor.Health);

            var widget = overlay.gameObject.AddComponent<DownedOverlayWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("group").objectReferenceValue = group;
                so.FindProperty("label").objectReferenceValue = label;
                so.FindProperty("reviveGroup").objectReferenceValue = reviveGroup;
                so.FindProperty("reviveFill").objectReferenceValue = fill;
            });
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

        private static void BuildGameOver(RectTransform parent)
        {
            var screen = Stretch("GameOver", parent);
            var group = screen.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            Themed(screen, null, HudColor.Panel);

            var panel = Rect("Panel", screen, Center, Center, Vector2.zero, new Vector2(1100f, 760f));
            Themed(panel, HudSprite.Panel, HudColor.Panel);
            var title = Text(Rect("Title", panel, TopCenter, TopCenter, new Vector2(0f, -24f), new Vector2(1000f, 80f)), null,
                "DÉFAITE", 64f, HudFont.Title, HudColor.Danger, TextAlignmentOptions.Center);
            var stats = Text(Rect("RunStats", panel, TopCenter, TopCenter, new Vector2(0f, -104f), new Vector2(1000f, 100f)), null,
                "Temps : 00:00", 26f, HudFont.Body, HudColor.Text, TextAlignmentOptions.Center);

            // Player rows (cloned at runtime from the disabled template).
            var list = Rect("Players", panel, TopCenter, TopCenter, new Vector2(0f, -212f), new Vector2(1040f, 430f));
            VerticalLayout(list.gameObject, 0, 10f);
            var row = Rect("PlayerRowTemplate", list, Center, Center, Vector2.zero, new Vector2(0f, 100f));
            LayoutHeight(row, 100f);
            Themed(row, HudSprite.Frame, HudColor.Frame);
            Text(Rect("Name", row, TopLeft, TopLeft, new Vector2(16f, -6f), new Vector2(1000f, 30f)), null,
                "Joueur 1 — Guerrier", 24f, HudFont.Title, HudColor.Accent, TextAlignmentOptions.MidlineLeft);
            Text(Rect("Stats", row, TopLeft, TopLeft, new Vector2(16f, -36f), new Vector2(1000f, 26f)), null,
                "Dégâts 0 · Kills 0", 20f, HudFont.Body, HudColor.Text, TextAlignmentOptions.MidlineLeft);
            var items = Rect("Items", row, BottomLeft, BottomLeft, new Vector2(16f, 6f), new Vector2(1000f, 34f));
            var itemsLayout = items.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            itemsLayout.spacing = 4f;
            itemsLayout.childControlWidth = itemsLayout.childControlHeight = false;
            itemsLayout.childForceExpandWidth = itemsLayout.childForceExpandHeight = false;
            itemsLayout.childAlignment = TextAnchor.MiddleLeft;
            var icon = Rect("ItemIconTemplate", items, Center, Center, Vector2.zero, new Vector2(34f, 34f));
            var iconImage = PlainImage(icon);
            iconImage.color = Color.white;
            Text(Rect("Count", icon, BottomRight, BottomRight, Vector2.zero, new Vector2(34f, 16f)), null,
                string.Empty, 12f, HudFont.Numbers, HudColor.Text, TextAlignmentOptions.BottomRight);
            row.gameObject.SetActive(false);

            var restart = Button(panel, "RestartButton", "Rejouer", 0f);
            PlaceButton(restart, new Vector2(-135f, 30f));
            var quit = Button(panel, "QuitButton", "Retour au menu", 0f);
            PlaceButton(quit, new Vector2(135f, 30f));

            var widget = screen.gameObject.AddComponent<GameOverWidget>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("group").objectReferenceValue = group;
                so.FindProperty("titleLabel").objectReferenceValue = title;
                so.FindProperty("statsLabel").objectReferenceValue = stats;
                so.FindProperty("playerRowTemplate").objectReferenceValue = row;
                so.FindProperty("itemIconTemplate").objectReferenceValue = iconImage;
                so.FindProperty("restartButton").objectReferenceValue = restart;
                so.FindProperty("quitButton").objectReferenceValue = quit;
            });
        }

        private static void BuildPauseMenu(RectTransform parent)
        {
            var screen = Stretch("PauseMenu", parent);
            var group = screen.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            Themed(screen, null, HudColor.Overlay);

            var main = Rect("Main", screen, Center, Center, Vector2.zero, new Vector2(460f, 600f));
            Themed(main, HudSprite.Panel, HudColor.Panel);
            Text(Rect("Title", main, TopCenter, TopCenter, new Vector2(0f, -20f), new Vector2(420f, 64f)), null,
                "PAUSE", 54f, HudFont.Title, HudColor.Accent, TextAlignmentOptions.Center);
            var subtitle = Text(Rect("Subtitle", main, TopCenter, TopCenter, new Vector2(0f, -84f), new Vector2(420f, 30f)), null,
                "Jeu en pause", 20f, HudFont.Body, HudColor.TextMuted, TextAlignmentOptions.Center);
            var buttons = Rect("Buttons", main, TopCenter, TopCenter, new Vector2(0f, -130f), new Vector2(380f, 450f));
            VerticalLayout(buttons.gameObject, 0, 12f);
            var resume = Button(buttons, "ResumeButton", "Reprendre", 56f);
            var options = Button(buttons, "OptionsButton", "Options", 56f);
            var abandon = Button(buttons, "AbandonButton", "Abandonner la partie", 56f);
            var menu = Button(buttons, "MenuButton", "Retour au menu", 56f);
            var quit = Button(buttons, "QuitButton", "Quitter le jeu", 56f);
            var wave = Button(buttons, "ForceWaveButton", "[Debug] Forcer une vague", 44f);

            var confirm = Rect("Confirm", screen, Center, Center, Vector2.zero, new Vector2(620f, 280f));
            Themed(confirm, HudSprite.Panel, HudColor.Panel);
            var confirmLabel = Text(Rect("Message", confirm, TopCenter, TopCenter, new Vector2(0f, -24f), new Vector2(580f, 150f)), null,
                "Confirmer ?", 26f, HudFont.Body, HudColor.Text, TextAlignmentOptions.Center);
            var yes = Button(confirm, "YesButton", "Oui", 0f);
            PlaceButton(yes, new Vector2(-135f, 28f));
            var no = Button(confirm, "NoButton", "Non", 0f);
            PlaceButton(no, new Vector2(135f, 28f));
            confirm.gameObject.SetActive(false);

            var optionsMenu = MainMenuBuilder.BuildOptions(screen);

            var widget = screen.gameObject.AddComponent<PauseMenu>();
            Props(widget, so =>
            {
                so.FindProperty("theme").objectReferenceValue = theme;
                so.FindProperty("group").objectReferenceValue = group;
                so.FindProperty("mainPanel").objectReferenceValue = main.gameObject;
                so.FindProperty("subtitleLabel").objectReferenceValue = subtitle;
                so.FindProperty("resumeButton").objectReferenceValue = resume;
                so.FindProperty("optionsButton").objectReferenceValue = options;
                so.FindProperty("abandonButton").objectReferenceValue = abandon;
                so.FindProperty("menuButton").objectReferenceValue = menu;
                so.FindProperty("quitButton").objectReferenceValue = quit;
                so.FindProperty("forceWaveButton").objectReferenceValue = wave;
                so.FindProperty("options").objectReferenceValue = optionsMenu;
                so.FindProperty("confirmPanel").objectReferenceValue = confirm.gameObject;
                so.FindProperty("confirmLabel").objectReferenceValue = confirmLabel;
                so.FindProperty("confirmYesButton").objectReferenceValue = yes;
                so.FindProperty("confirmNoButton").objectReferenceValue = no;
            });
        }

        internal static void PlaceButton(UnityEngine.UI.Button button, Vector2 bottomCenterOffset)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = BottomCenter;
            rect.anchoredPosition = bottomCenterOffset;
            rect.sizeDelta = new Vector2(240f, 58f);
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

        internal static readonly Vector2 Center = new(0.5f, 0.5f);
        internal static readonly Vector2 TopLeft = new(0f, 1f);
        internal static readonly Vector2 TopCenter = new(0.5f, 1f);
        internal static readonly Vector2 TopRight = new(1f, 1f);
        internal static readonly Vector2 BottomLeft = new(0f, 0f);
        internal static readonly Vector2 BottomCenter = new(0.5f, 0f);
        internal static readonly Vector2 BottomRight = new(1f, 0f);

        internal static RectTransform Rect(string name, RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
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

        internal static RectTransform Stretch(string name, RectTransform parent, float inset = 0f)
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

        internal static UnityEngine.UI.Image Themed(RectTransform rect, HudSprite? sprite, HudColor color, bool applyColor = true, bool raycast = false)
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
        internal static UnityEngine.UI.Image PlainImage(RectTransform rect)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.color = new Color(1f, 1f, 1f, 0.08f);
            return image;
        }

        /// <summary>Adds a themed TMP label. With name == null, the label goes on <paramref name="parent"/> itself.</summary>
        internal static TextMeshProUGUI Text(RectTransform parent, string name, string value, float size, HudFont font, HudColor color,
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

        internal static UnityEngine.UI.Button Button(RectTransform parent, string name, string label, float height)
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

        internal static TMP_InputField InputField(RectTransform parent, string name, string value)
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

        internal static void VerticalLayout(GameObject go, int padding, float spacing)
        {
            var layout = go.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
        }

        internal static void LayoutHeight(RectTransform rect, float height)
        {
            var element = rect.GetComponent<UnityEngine.UI.LayoutElement>();
            if (element == null)
                element = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
        }

        internal static void Props(Object target, System.Action<SerializedObject> edit)
        {
            var so = new SerializedObject(target);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
