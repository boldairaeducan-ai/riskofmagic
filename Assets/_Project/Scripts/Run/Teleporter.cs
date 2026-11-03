using System;
using System.Collections.Generic;
using RiskOfMagic.Enemies;
using RiskOfMagic.Core;
using RiskOfMagic.Interaction;
using RiskOfMagic.Items;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RiskOfMagic.Run
{
    public enum TeleporterState : byte
    {
        Idle,
        Charging,
        Charged,
        Ready
    }

    /// <summary>
    /// RoR-style teleporter event: activating it spawns the boss and boosts the director; the charge only
    /// progresses while a living player stands in the radius. Charged + bosses dead → the director returns to
    /// normal (remaining enemies stay alive) and the teleporter can be used to go to the next stage.
    /// </summary>
    public class Teleporter : NetworkBehaviour, IInteractable
    {
        [Header("Charge")]
        [SerializeField, Min(1f)] private float chargeDuration = 90f;
        [SerializeField, Min(1f)] private float radius = 30f;

        [Header("Boss")]
        [SerializeField] private List<SpawnCard> bossCards = new();
        [SerializeField, Min(1)] private int bossCount = 1;
        [SerializeField] private Vector2 bossSpawnDistance = new(12f, 22f);

        [Header("Boss reward (one item per player)")]
        [SerializeField] private RarityWeights bossRewardWeights = new(0f, 65f, 28f, 7f);
        [SerializeField, Range(0f, 1f), Tooltip("Chance to get a Boss item instead.")] private float bossItemChance = 0.1f;

        [Header("Event")]
        [SerializeField, Min(1f)] private float eventCreditMultiplier = 1.75f;

        [Header("Visuals")]
        [SerializeField] private LineRenderer radiusRing;
        [SerializeField, Min(8)] private int ringSegments = 96;
        [SerializeField] private Color idleColor = new(0.6f, 0.3f, 1f);
        [SerializeField] private Color chargingColor = new(1f, 0.3f, 0.3f);
        [SerializeField] private Color readyColor = new(0.3f, 1f, 0.5f);
        [SerializeField] private Renderer[] glowRenderers = Array.Empty<Renderer>();

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly NetworkVariable<TeleporterState> state = new();
        private readonly NetworkVariable<float> charge = new();
        private readonly List<EnemyActor> bosses = new();
        private bool bossRewardGiven;
        private MaterialPropertyBlock propertyBlock;

        public static Teleporter Instance { get; private set; }

        /// <summary>Every peer.</summary>
        public event Action<TeleporterState> StateChanged;

        public TeleporterState State => state.Value;
        public float Charge01 => charge.Value;
        public float Radius => radius;

        public bool IsInside(Vector3 position)
        {
            Vector3 offset = position - transform.position;
            return Mathf.Abs(offset.y) <= radius && new Vector2(offset.x, offset.z).sqrMagnitude <= radius * radius;
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            state.OnValueChanged += OnStateChanged;
            BuildRing();
            ApplyVisuals();
        }

        public override void OnNetworkDespawn()
        {
            state.OnValueChanged -= OnStateChanged;
            if (Instance == this)
                Instance = null;
            if (IsServer && CombatDirector.Instance != null)
                CombatDirector.Instance.EventCreditMultiplier = 1f;
        }

        // ---------------------------------------------------------------- Interaction

        public bool CanInteract(PlayerAvatar player)
            => player.IsAlive && (state.Value == TeleporterState.Idle || state.Value == TeleporterState.Ready);

        public string GetPrompt(PlayerAvatar player) => state.Value switch
        {
            TeleporterState.Idle => "[E] Activer le téléporteur",
            TeleporterState.Ready => "[E] Passer à l'étape suivante",
            _ => string.Empty
        };

        public void Interact(PlayerAvatar player)
        {
            if (!IsServer)
                return;
            if (state.Value == TeleporterState.Idle)
                StartEvent();
            else if (state.Value == TeleporterState.Ready && StageManager.Instance != null)
                StageManager.Instance.AdvanceStage();
        }

        // ---------------------------------------------------------------- Event (server)

        private void StartEvent()
        {
            state.Value = TeleporterState.Charging;
            charge.Value = 0f;
            if (CombatDirector.Instance != null)
                CombatDirector.Instance.EventCreditMultiplier = eventCreditMultiplier;
            SpawnBosses();
        }

        private void SpawnBosses()
        {
            bosses.Clear();
            bossRewardGiven = false;
            var director = CombatDirector.Instance;
            if (director == null || bossCards.Count == 0)
            {
                Debug.LogWarning("[Teleporter] No director or boss cards: the event runs without a boss.", this);
                return;
            }

            int level = RunDifficulty.CurrentEnemyLevel;
            for (int i = 0; i < bossCount; i++)
            {
                var card = bossCards[Random.Range(0, bossCards.Count)];
                if (card == null || card.prefab == null || !TryFindBossPosition(out var position))
                    continue;

                Vector3 facing = Vector3.ProjectOnPlane(transform.position - position, Vector3.up);
                var boss = director.Spawn(card, position, facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing) : Quaternion.identity, level);
                bosses.Add(boss);
                if (boss.TryGetComponent(out EnemyBrain brain))
                    foreach (var player in PlayerAvatar.All)
                        brain.Alert(player, false);
            }
        }

        private bool TryFindBossPosition(out Vector3 position)
        {
            for (int i = 0; i < 20; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                Vector3 candidate = transform.position + new Vector3(dir.x, 0f, dir.y) * Random.Range(bossSpawnDistance.x, bossSpawnDistance.y);
                if (NavMesh.SamplePosition(candidate, out var hit, 4f, NavMesh.AllAreas))
                {
                    position = hit.position;
                    return true;
                }
            }
            position = default;
            return false;
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned)
                return;

            if (state.Value == TeleporterState.Charging)
            {
                if (AnyLivingPlayerInside())
                    charge.Value = Mathf.Min(1f, charge.Value + Time.deltaTime / chargeDuration);
                if (charge.Value >= 1f)
                    state.Value = TeleporterState.Charged;
            }

            bool eventRunning = state.Value == TeleporterState.Charging || state.Value == TeleporterState.Charged;
            if (eventRunning && bosses.Count > 0 && !bossRewardGiven && !AnyBossAlive())
                GiveBossRewards();

            if (state.Value == TeleporterState.Charged && !AnyBossAlive())
                Complete();
        }

        private void Complete()
        {
            state.Value = TeleporterState.Ready;
            if (CombatDirector.Instance != null)
                CombatDirector.Instance.EventCreditMultiplier = 1f;
        }

        private void GiveBossRewards()
        {
            bossRewardGiven = true;
            var catalog = GameCatalog.Instance;
            foreach (var player in PlayerAvatar.All)
            {
                var rarity = Random.value < bossItemChance ? ItemRarity.Boss : bossRewardWeights.Roll();
                var item = catalog.RollItem(rarity);
                if (item != null)
                    player.Inventory.AddItem(item);
            }
        }

        public bool AnyLivingPlayerInside()
        {
            foreach (var player in PlayerAvatar.All)
                if (player.IsStanding && IsInside(player.transform.position))
                    return true;
            return false;
        }

        private bool AnyBossAlive()
        {
            foreach (var boss in bosses)
                if (boss != null && boss.IsSpawned && boss.Health.IsAlive)
                    return true;
            return false;
        }

        // ---------------------------------------------------------------- Visuals (every peer)

        private void OnStateChanged(TeleporterState previous, TeleporterState current)
        {
            ApplyVisuals();
            StateChanged?.Invoke(current);
        }

        private void BuildRing()
        {
            if (radiusRing == null)
                return;
            radiusRing.useWorldSpace = false;
            radiusRing.loop = true;
            radiusRing.positionCount = ringSegments;
            for (int i = 0; i < ringSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / ringSegments;
                radiusRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.15f, Mathf.Sin(angle) * radius));
            }
        }

        private void ApplyVisuals()
        {
            var color = state.Value switch
            {
                TeleporterState.Charging or TeleporterState.Charged => chargingColor,
                TeleporterState.Ready => readyColor,
                _ => idleColor
            };

            if (radiusRing != null)
            {
                radiusRing.enabled = state.Value == TeleporterState.Charging || state.Value == TeleporterState.Charged;
                radiusRing.startColor = radiusRing.endColor = color;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            foreach (var glow in glowRenderers)
            {
                if (glow == null)
                    continue;
                glow.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(ColorId, color);
                propertyBlock.SetColor(BaseColorId, color);
                glow.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
