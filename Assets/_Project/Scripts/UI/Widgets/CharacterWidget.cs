using RiskOfMagic.Characters;
using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    public class CharacterWidget : HudWidget
    {
        [SerializeField] private UnityEngine.UI.Image portrait;
        [SerializeField] private TMP_Text nameLabel;

        protected override void OnBind()
        {
            Player.CharacterChanged += Refresh;
            Refresh(Player.Character);
        }

        protected override void OnUnbind() => Player.CharacterChanged -= Refresh;

        private void Refresh(CharacterDefinition character)
        {
            if (nameLabel != null)
                nameLabel.text = character != null ? character.displayName : string.Empty;
            if (portrait == null)
                return;

            var sprite = character != null && character.portrait != null ? character.portrait : theme != null ? theme.defaultPortrait : null;
            portrait.sprite = sprite;
            // Without art, show the character color as a placeholder.
            portrait.color = sprite != null ? Color.white : character != null ? character.bodyColor : Color.gray;
        }
    }
}
