using RiskOfMagic.Interaction;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Run.Secrets
{
    /// <summary>Rune fragment hidden on a SecretFragment spot. Picking it up counts for the whole team.</summary>
    public class SecretFragment : NetworkBehaviour, IInteractable
    {
        [SerializeField] private Transform visual;
        [SerializeField] private float spinSpeed = 60f;
        [SerializeField] private float bobHeight = 0.15f;

        private Vector3 visualBase;
        private bool taken;

        private void Awake()
        {
            if (visual != null)
                visualBase = visual.localPosition;
        }

        public bool CanInteract(PlayerAvatar player) => !taken && player.IsStanding && SecretAltar.Instance != null;

        public string GetPrompt(PlayerAvatar player) => "[E] Ramasser le fragment de rune";

        public void Interact(PlayerAvatar player)
        {
            if (!IsServer || taken || SecretAltar.Instance == null)
                return;
            taken = true;
            SecretAltar.Instance.AddFragment();
            NetworkObject.Despawn();
        }

        private void Update()
        {
            if (visual == null)
                return;
            visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            visual.localPosition = visualBase + Vector3.up * (Mathf.Sin(Time.time * 2f) * bobHeight);
        }
    }
}
