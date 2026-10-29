using RiskOfMagic.Player;
using Unity.Netcode;

namespace RiskOfMagic.Interaction
{
    /// <summary>Implemented by NetworkBehaviours (the NetworkObject is used to target RPCs).</summary>
    public interface IInteractable
    {
        NetworkObject NetworkObject { get; }
        bool CanInteract(PlayerAvatar player);
        /// <summary>Empty string = hidden.</summary>
        string GetPrompt(PlayerAvatar player);
        /// <summary>Server only.</summary>
        void Interact(PlayerAvatar player);
    }
}
