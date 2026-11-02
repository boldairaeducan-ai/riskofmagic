using System;
using RiskOfMagic.Combat;
using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    public enum HudSprite : byte
    {
        Panel, Frame, BarBackground, BarFill, SlotFrame, CooldownOverlay, Crosshair, Button, InputField,
        GoldIcon, DefaultSkillIcon, DefaultItemIcon, DefaultPortrait
    }

    public enum HudColor : byte
    {
        Panel, Frame, Text, TextMuted, Accent, BarBackground, Health, HealthLow, HealthTrail, Gold,
        CooldownOverlay, Charge, Button, ButtonSelected, Danger, Icon, Overlay
    }

    public enum HudFont : byte
    {
        Body, Title, Numbers
    }

    /// <summary>
    /// Single place to plug real UI art: fonts, sprites (9-sliced when they have borders) and colors.
    /// Empty sprite slots render as plain colored rectangles; empty fonts fall back to the TMP default.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/UI/Hud Theme", fileName = "HudTheme")]
    public class HudTheme : ScriptableObject
    {
        [Header("Fonts")]
        public TMP_FontAsset bodyFont;
        public TMP_FontAsset titleFont;
        public TMP_FontAsset numberFont;

        [Header("Sprites")]
        public Sprite panel;
        public Sprite frame;
        public Sprite barBackground;
        public Sprite barFill;
        public Sprite slotFrame;
        public Sprite cooldownOverlay;
        public Sprite crosshair;
        public Sprite button;
        public Sprite inputField;
        public Sprite goldIcon;
        public Sprite defaultSkillIcon;
        public Sprite defaultItemIcon;
        public Sprite defaultPortrait;

        [Header("Colors")]
        public Color panelColor = new(0.05f, 0.05f, 0.08f, 0.75f);
        public Color frameColor = new(0.75f, 0.65f, 0.45f, 1f);
        public Color textColor = new(0.95f, 0.93f, 0.88f, 1f);
        public Color textMutedColor = new(0.7f, 0.68f, 0.62f, 1f);
        public Color accentColor = new(0.95f, 0.75f, 0.3f, 1f);
        public Color barBackgroundColor = new(0f, 0f, 0f, 0.6f);
        public Color healthColor = new(0.35f, 0.8f, 0.35f, 1f);
        public Color healthLowColor = new(0.9f, 0.25f, 0.2f, 1f);
        public Color healthTrailColor = new(1f, 0.9f, 0.6f, 0.8f);
        public Color goldColor = new(1f, 0.8f, 0.25f, 1f);
        public Color cooldownOverlayColor = new(0f, 0f, 0f, 0.7f);
        public Color chargeColor = new(0.55f, 0.85f, 1f, 1f);
        public Color buttonColor = new(0.2f, 0.18f, 0.15f, 0.95f);
        public Color buttonSelectedColor = new(0.55f, 0.4f, 0.15f, 1f);
        public Color dangerColor = new(0.85f, 0.15f, 0.15f, 1f);
        public Color iconColor = Color.white;
        public Color overlayColor = new(0.3f, 0f, 0f, 0.45f);

        [Header("Damage numbers")]
        public Color damagePhysical = Color.white;
        public Color damageFire = new(1f, 0.55f, 0.2f);
        public Color damageFrost = new(0.55f, 0.9f, 1f);
        public Color damageLightning = new(0.75f, 0.85f, 1f);
        public Color damageArcane = new(0.8f, 0.5f, 1f);
        public Color damagePoison = new(0.5f, 1f, 0.4f);
        public Color damageCritical = new(1f, 0.85f, 0.2f);
        public Color damageTaken = new(1f, 0.25f, 0.25f);
        [Min(1f)] public float damageFontSize = 34f;
        [Min(1f)] public float criticalFontSize = 46f;

#if UNITY_EDITOR
        /// <summary>Editor only: lets themed elements refresh live while tweaking the asset.</summary>
        public static event Action Changed;

        private void OnValidate() => Changed?.Invoke();
#endif

        public Sprite GetSprite(HudSprite slot) => slot switch
        {
            HudSprite.Panel => panel,
            HudSprite.Frame => frame,
            HudSprite.BarBackground => barBackground,
            HudSprite.BarFill => barFill,
            HudSprite.SlotFrame => slotFrame,
            HudSprite.CooldownOverlay => cooldownOverlay,
            HudSprite.Crosshair => crosshair,
            HudSprite.Button => button,
            HudSprite.InputField => inputField,
            HudSprite.GoldIcon => goldIcon,
            HudSprite.DefaultSkillIcon => defaultSkillIcon,
            HudSprite.DefaultItemIcon => defaultItemIcon,
            HudSprite.DefaultPortrait => defaultPortrait,
            _ => null
        };

        public Color GetColor(HudColor slot) => slot switch
        {
            HudColor.Panel => panelColor,
            HudColor.Frame => frameColor,
            HudColor.Text => textColor,
            HudColor.TextMuted => textMutedColor,
            HudColor.Accent => accentColor,
            HudColor.BarBackground => barBackgroundColor,
            HudColor.Health => healthColor,
            HudColor.HealthLow => healthLowColor,
            HudColor.HealthTrail => healthTrailColor,
            HudColor.Gold => goldColor,
            HudColor.CooldownOverlay => cooldownOverlayColor,
            HudColor.Charge => chargeColor,
            HudColor.Button => buttonColor,
            HudColor.ButtonSelected => buttonSelectedColor,
            HudColor.Danger => dangerColor,
            HudColor.Icon => iconColor,
            HudColor.Overlay => overlayColor,
            _ => Color.magenta
        };

        public TMP_FontAsset GetFont(HudFont slot) => slot switch
        {
            HudFont.Title => titleFont != null ? titleFont : bodyFont,
            HudFont.Numbers => numberFont != null ? numberFont : bodyFont,
            _ => bodyFont
        };

        public Color GetDamageColor(in DamageInfo info, bool takenByLocalPlayer)
        {
            if (takenByLocalPlayer)
                return damageTaken;
            if (info.IsCritical)
                return damageCritical;
            return info.Type switch
            {
                DamageType.Fire => damageFire,
                DamageType.Frost => damageFrost,
                DamageType.Lightning => damageLightning,
                DamageType.Arcane => damageArcane,
                DamageType.Poison => damagePoison,
                _ => damagePhysical
            };
        }
    }
}
