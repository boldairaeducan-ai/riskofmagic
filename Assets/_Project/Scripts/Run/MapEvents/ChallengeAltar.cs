using System.Collections.Generic;
using RiskOfMagic.Core;
using RiskOfMagic.Enemies;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Run.MapEvents
{
    /// <summary>
    /// Autel de défi: free; summons enemies hunting right away, bought with a credit budget that scales with the
    /// difficulty coefficient (elites of one affix first, normal enemies with the leftover), in two waves.
    /// All dead → one item per player.
    /// Values: MapEventsConfig.challengeAltar.
    /// </summary>
    public class ChallengeAltar : MapEvent
    {
        public enum AltarState : byte { Idle, Active, Done }

        [SerializeField] private Renderer[] glowRenderers = new Renderer[0];
        [SerializeField] private Color idleColor = new(1f, 0.3f, 0.2f);
        [SerializeField] private Color activeColor = new(1f, 0.75f, 0.2f);
        [SerializeField] private Color doneColor = new(0.3f, 0.3f, 0.3f);

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly NetworkVariable<AltarState> state = new();
        private readonly NetworkVariable<int> remaining = new();
        private readonly List<EnemyActor> wave = new();
        private readonly List<(SpawnCard card, bool elite)> pending = new();
        private RiskOfMagic.Enemies.Elites.EliteAffix waveAffix;
        private int firstWaveSize;
        private MaterialPropertyBlock block;

        private ChallengeSettings Settings => Config.challengeAltar;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            state.OnValueChanged += OnStateChanged;
            ApplyVisuals();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            state.OnValueChanged -= OnStateChanged;
        }

        public override bool CanInteract(PlayerAvatar player) => player.IsAlive && state.Value == AltarState.Idle;

        public override string GetPrompt(PlayerAvatar player) =>
            state.Value == AltarState.Idle ? "[E] Autel de défi : affronter des élites" : string.Empty;

        public override string GetObjective(PlayerAvatar player) =>
            state.Value == AltarState.Active ? $"Défi : {remaining.Value} ennemi(s) restant(s)" : string.Empty;

        public override void Interact(PlayerAvatar player)
        {
            if (!IsServer || state.Value != AltarState.Idle)
                return;
            var settings = Settings;
            waveAffix = GameCatalog.Instance != null ? GameCatalog.Instance.RollElite() : null;
            BuyEnemies(settings);
            int firstCount = Mathf.Clamp(Mathf.CeilToInt(pending.Count * settings.firstWaveShare), 1, Mathf.Max(1, pending.Count));
            wave.Clear();
            SpawnPending(firstCount);
            firstWaveSize = wave.Count;
            remaining.Value = wave.Count + pending.Count;
            state.Value = remaining.Value > 0 ? AltarState.Active : AltarState.Done;
            if (remaining.Value == 0)
                GiveItemToEveryone(settings.reward);
        }

        /// <summary>Elites while the budget allows (at least one), then normal enemies with what is left.</summary>
        private void BuyEnemies(ChallengeSettings settings)
        {
            pending.Clear();
            var director = CombatDirector.Instance;
            if (director == null)
                return;
            float budget = settings.creditsPerCoefficient * RunDifficulty.CurrentCoefficient;
            bool elitesAffordable = true;
            int failures = 0;
            while (pending.Count < settings.maxEnemies && failures < 6)
            {
                var card = director.PickUnlockedCard();
                if (card == null)
                    break;
                float eliteCost = card.cost * settings.eliteCostMultiplier;
                if (elitesAffordable && (budget >= eliteCost || pending.Count == 0))
                {
                    pending.Add((card, waveAffix != null));
                    budget -= eliteCost;
                    continue;
                }
                if (budget >= card.cost)
                {
                    elitesAffordable = false; // elites first, then fill
                    pending.Add((card, false));
                    budget -= card.cost;
                    continue;
                }
                elitesAffordable = false;
                failures++; // too expensive: try another card
            }
        }

        private void SpawnPending(int count)
        {
            var settings = Settings;
            for (int i = 0; i < count && pending.Count > 0; i++)
            {
                var (card, elite) = pending[0];
                pending.RemoveAt(0);
                var enemy = SpawnEnemy(settings.spawnDistance, elite ? waveAffix : null, card);
                if (enemy != null)
                    wave.Add(enemy);
            }
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || state.Value != AltarState.Active)
                return;
            wave.RemoveAll(e => e == null || !e.IsSpawned || !e.Health.IsAlive);
            if (pending.Count > 0 && wave.Count <= firstWaveSize / 2)
                SpawnPending(pending.Count); // second wave
            int left = wave.Count + pending.Count;
            if (remaining.Value != left)
                remaining.Value = left;
            if (left > 0)
                return;
            GiveItemToEveryone(Settings.reward);
            state.Value = AltarState.Done;
        }

        private void OnStateChanged(AltarState previous, AltarState current)
        {
            ApplyVisuals();
            if (current == AltarState.Done)
                RaiseCompleted(MapEventType.ChallengeAltar);
        }

        private void ApplyVisuals()
        {
            Color color = state.Value switch
            {
                AltarState.Active => activeColor,
                AltarState.Done => doneColor,
                _ => idleColor
            };
            block ??= new MaterialPropertyBlock();
            foreach (var r in glowRenderers)
            {
                if (r == null)
                    continue;
                r.GetPropertyBlock(block);
                block.SetColor(ColorId, color);
                block.SetColor(BaseColorId, color);
                r.SetPropertyBlock(block);
            }
        }
    }
}
