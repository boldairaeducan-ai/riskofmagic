using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Interaction
{
    [RequireComponent(typeof(PlayerAvatar))]
    public class PlayerInteractor : NetworkBehaviour
    {
        [SerializeField, Min(0.5f)] private float range = 4f;
        [SerializeField] private LayerMask mask = Physics.DefaultRaycastLayers;
        [SerializeField, Tooltip("Extra server-side distance tolerance (latency).")] private float serverRangeTolerance = 2f;

        private PlayerAvatar avatar;
        private IHoldInteractable holding;

        /// <summary>Owner only.</summary>
        public IInteractable Focus { get; private set; }
        public string FocusPrompt => Focus != null ? Focus.GetPrompt(avatar) : string.Empty;

        private void Awake() => avatar = GetComponent<PlayerAvatar>();

        private void Update()
        {
            if (!IsSpawned || !IsOwner)
                return;

            Focus = null;
            if (avatar.ControlEnabled)
            {
                Ray ray = avatar.Look.AimRay;
                if (Physics.Raycast(ray, out var hit, range, mask, QueryTriggerInteraction.Collide))
                    Focus = hit.collider.GetComponentInParent<IInteractable>();
            }

            // Hold interactions (revive): tell the server when we start/stop holding on a target.
            var wanted = Focus is IHoldInteractable hold && avatar.Input.InteractHeld && hold.CanInteract(avatar) ? hold : null;
            if (wanted != holding)
            {
                if (holding != null && holding.NetworkObject != null)
                    HoldRpc(holding.NetworkObject, false);
                if (wanted != null)
                    HoldRpc(wanted.NetworkObject, true);
                holding = wanted;
            }

            if (Focus != null && Focus is not IHoldInteractable && avatar.Input.InteractPressed && Focus.CanInteract(avatar))
                InteractRpc(Focus.NetworkObject);
        }

        [Rpc(SendTo.Server)]
        private void HoldRpc(NetworkObjectReference target, bool isHolding)
        {
            if (!target.TryGet(out NetworkObject networkObject))
                return;
            var interactable = networkObject.GetComponentInChildren<IHoldInteractable>();
            if (interactable == null)
                return;
            float maxDistance = range + serverRangeTolerance;
            bool inRange = (networkObject.transform.position - transform.position).sqrMagnitude <= maxDistance * maxDistance;
            interactable.SetHolding(avatar, isHolding && inRange && avatar.IsAlive);
        }

        [Rpc(SendTo.Server)]
        private void InteractRpc(NetworkObjectReference target)
        {
            if (!avatar.IsAlive || !target.TryGet(out NetworkObject networkObject))
                return;
            var interactable = networkObject.GetComponentInChildren<IInteractable>();
            if (interactable == null)
                return;

            float maxDistance = range + serverRangeTolerance;
            if ((networkObject.transform.position - transform.position).sqrMagnitude > maxDistance * maxDistance)
                return;
            if (interactable.CanInteract(avatar))
                interactable.Interact(avatar);
        }
    }
}
