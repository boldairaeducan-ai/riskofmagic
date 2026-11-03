using UnityEngine;

namespace RiskOfMagic.Equipment
{
    /// <summary>
    /// Active item (one slot, key F). Activation runs on the server with the owner's aim.
    /// Derive and implement Activate; return false to not consume the cooldown.
    /// </summary>
    public abstract class EquipmentDefinition : ScriptableObject
    {
        public string displayName = "Équipement";
        [TextArea] public string description;
        public Sprite icon;
        [Min(0.5f)] public float cooldown = 30f;

        /// <summary>Server only.</summary>
        public abstract bool Activate(PlayerEquipment user, Vector3 aimOrigin, Vector3 aimDirection);

        public static readonly Color DisplayColor = new(1f, 0.55f, 0.25f);
    }
}
