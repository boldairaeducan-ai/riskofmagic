using RiskOfMagic.Interaction;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Run
{
    /// <summary>
    /// Appears next to the teleporter once the final stage's event is over: ends the run with a victory.
    /// The teleporter itself continues to the next loop.
    /// </summary>
    public class RunEndSeal : NetworkBehaviour, IInteractable
    {
        public bool CanInteract(PlayerAvatar player) => player.IsAlive && StageManager.Instance != null && !StageManager.Instance.IsGameOver;

        public string GetPrompt(PlayerAvatar player) => "[E] Sceller la victoire (fin de la partie)";

        public void Interact(PlayerAvatar player)
        {
            if (IsServer && StageManager.Instance != null)
                StageManager.Instance.EndRun(won: true);
        }
    }
}
