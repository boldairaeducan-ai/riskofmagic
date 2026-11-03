using RiskOfMagic.Player;

namespace RiskOfMagic.Interaction
{
    /// <summary>Interaction performed by holding the key (revive...). The server tracks who is holding.</summary>
    public interface IHoldInteractable : IInteractable
    {
        /// <summary>Server only.</summary>
        void SetHolding(PlayerAvatar player, bool holding);
    }
}
