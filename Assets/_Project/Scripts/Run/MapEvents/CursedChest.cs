using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using RiskOfMagic.Items;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Run.MapEvents
{
    /// <summary>
    /// Coffre maudit: paid with max health (Withered status until the end of the stage, stacking), drops a Rare+ item.
    /// Values: MapEventsConfig.cursedChest.
    /// </summary>
    public class CursedChest : MapEvent
    {
        [SerializeField] private GameObject closedVisual;
        [SerializeField] private Vector3 dropOffset = new(0f, 0f, 1.8f);

        private readonly NetworkVariable<bool> opened = new();

        private CursedChestSettings Settings => Config.cursedChest;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            opened.OnValueChanged += OnOpenedChanged;
            ApplyVisuals();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            opened.OnValueChanged -= OnOpenedChanged;
        }

        public override bool CanInteract(PlayerAvatar player) => player.IsAlive && !opened.Value;

        public override string GetPrompt(PlayerAvatar player) => opened.Value
            ? string.Empty
            : $"[E] Coffre maudit : coûte {Settings.maxHealthCostPercent}% de PV max jusqu'à la fin de l'étape";

        public override void Interact(PlayerAvatar player)
        {
            if (!IsServer || opened.Value || player.Status == null)
                return;
            opened.Value = true;
            player.Status.Apply(StatusType.Withered, 0f, Settings.maxHealthCostPercent, 95);

            var item = GameCatalog.Instance != null ? GameCatalog.Instance.RollItem(Settings.reward.Roll()) : null;
            if (item != null)
                ItemPickup.SpawnItem(item, transform.TransformPoint(dropOffset));
        }

        private void OnOpenedChanged(bool previous, bool current) => ApplyVisuals();

        private void ApplyVisuals()
        {
            if (closedVisual != null)
                closedVisual.SetActive(!opened.Value);
        }
    }
}
