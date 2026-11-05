using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Run.MapEvents
{
    /// <summary>
    /// Tombe à purifier: once activated, the purification only progresses while a standing player is inside the zone;
    /// monsters keep coming around it. Purified: healing zone for a while + one item per player.
    /// Values: MapEventsConfig.purificationTomb.
    /// </summary>
    public class PurificationTomb : MapEvent
    {
        public enum TombState : byte { Idle, Purifying, Healing, Done }

        [SerializeField] private LineRenderer zoneRing;
        [SerializeField, Tooltip("Shown while healing, scaled to the heal radius.")] private GameObject healVisual;
        [SerializeField] private Color idleColor = new(0.55f, 0.35f, 0.75f);
        [SerializeField] private Color purifyingColor = new(1f, 0.85f, 0.35f);
        [SerializeField] private Color healingColor = new(0.35f, 1f, 0.5f);
        [SerializeField, Min(0.1f)] private float healTick = 0.5f;

        private readonly NetworkVariable<TombState> state = new();
        private readonly NetworkVariable<float> progress = new();
        private float nextSummon;
        private float healUntil;
        private float nextHeal;

        private TombSettings Settings => Config.purificationTomb;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            state.OnValueChanged += OnStateChanged;
            DrawRing(zoneRing, Settings.radius);
            ApplyVisuals();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            state.OnValueChanged -= OnStateChanged;
        }

        public override bool CanInteract(PlayerAvatar player) => player.IsAlive && state.Value == TombState.Idle;

        public override string GetPrompt(PlayerAvatar player) =>
            state.Value == TombState.Idle ? "[E] Purifier la tombe (restez dans la zone, des monstres viendront)" : string.Empty;

        public override void Interact(PlayerAvatar player)
        {
            if (!IsServer || state.Value != TombState.Idle)
                return;
            progress.Value = 0f;
            nextSummon = Time.time + 1.5f;
            state.Value = TombState.Purifying;
        }

        public override string GetObjective(PlayerAvatar player)
        {
            switch (state.Value)
            {
                case TombState.Purifying:
                    string text = $"Purification de la tombe : {Mathf.FloorToInt(progress.Value * 100f)}%";
                    return IsInside(player.transform.position, Settings.radius) ? text : text + "  <size=80%>(rejoignez la zone)</size>";
                case TombState.Healing:
                    return "Tombe purifiée : zone de soins active";
                default:
                    return string.Empty;
            }
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned)
                return;
            var settings = Settings;
            if (state.Value == TombState.Purifying)
                UpdatePurifying(settings);
            else if (state.Value == TombState.Healing)
                UpdateHealing(settings);
        }

        private void UpdatePurifying(TombSettings settings)
        {
            if (AnyStandingPlayerInside(settings.radius))
                progress.Value = Mathf.Min(1f, progress.Value + Time.deltaTime / Mathf.Max(1f, settings.purifyDuration));

            if (Time.time >= nextSummon)
            {
                nextSummon = Time.time + settings.summonInterval;
                int count = Random.Range(settings.summonCount.x, settings.summonCount.y + 1);
                for (int i = 0; i < count; i++)
                    SpawnEnemy(settings.summonDistance);
            }

            if (progress.Value >= 1f)
            {
                GiveItemToEveryone(settings.reward);
                healUntil = Time.time + settings.healDuration;
                nextHeal = 0f;
                state.Value = settings.healDuration > 0f ? TombState.Healing : TombState.Done;
            }
        }

        private void UpdateHealing(TombSettings settings)
        {
            if (Time.time >= healUntil)
            {
                state.Value = TombState.Done;
                return;
            }
            if (Time.time < nextHeal)
                return;
            nextHeal = Time.time + healTick;
            foreach (var player in PlayerAvatar.All)
                if (player != null && player.IsStanding && IsInside(player.transform.position, settings.healRadius))
                    player.Health.Heal(player.Health.Max * settings.healPerSecond * healTick);
        }

        private bool AnyStandingPlayerInside(float radius)
        {
            foreach (var player in PlayerAvatar.All)
                if (player != null && player.IsStanding && IsInside(player.transform.position, radius))
                    return true;
            return false;
        }

        private void OnStateChanged(TombState previous, TombState current) => ApplyVisuals();

        private void ApplyVisuals()
        {
            var current = state.Value;
            if (zoneRing != null)
            {
                zoneRing.enabled = current != TombState.Done;
                Color color = current switch
                {
                    TombState.Purifying => purifyingColor,
                    TombState.Healing => healingColor,
                    _ => idleColor
                };
                zoneRing.startColor = zoneRing.endColor = color;
                if (current == TombState.Healing)
                    DrawRing(zoneRing, Settings.healRadius);
            }
            if (healVisual != null)
            {
                healVisual.SetActive(current == TombState.Healing);
                float diameter = Settings.healRadius * 2f;
                healVisual.transform.localScale = new Vector3(diameter, healVisual.transform.localScale.y, diameter);
            }
        }
    }
}
