using System.Collections.Generic;
using RiskOfMagic.Core;
using RiskOfMagic.Enemies;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Run.MapEvents
{
    /// <summary>
    /// Autel de défi: free; summons a wave of elites (one affix) hunting right away. All dead → one item per player.
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
            state.Value == AltarState.Idle ? "[E] Autel de défi : affronter une vague d'élites" : string.Empty;

        public override string GetObjective(PlayerAvatar player) =>
            state.Value == AltarState.Active ? $"Défi : {remaining.Value} élite(s) restante(s)" : string.Empty;

        public override void Interact(PlayerAvatar player)
        {
            if (!IsServer || state.Value != AltarState.Idle)
                return;
            var settings = Settings;
            var affix = GameCatalog.Instance != null ? GameCatalog.Instance.RollElite() : null;
            int count = settings.baseEnemies + settings.enemiesPerPlayer * PlayerAvatar.All.Count;
            wave.Clear();
            for (int i = 0; i < count; i++)
            {
                var enemy = SpawnEnemy(settings.spawnDistance, affix);
                if (enemy != null)
                    wave.Add(enemy);
            }
            remaining.Value = wave.Count;
            state.Value = wave.Count > 0 ? AltarState.Active : AltarState.Done;
            if (wave.Count == 0)
                GiveItemToEveryone(settings.reward);
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || state.Value != AltarState.Active)
                return;
            wave.RemoveAll(e => e == null || !e.IsSpawned || !e.Health.IsAlive);
            if (remaining.Value != wave.Count)
                remaining.Value = wave.Count;
            if (wave.Count > 0)
                return;
            GiveItemToEveryone(Settings.reward);
            state.Value = AltarState.Done;
        }

        private void OnStateChanged(AltarState previous, AltarState current) => ApplyVisuals();

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
