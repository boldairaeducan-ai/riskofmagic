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
        [SerializeField, Min(1), Tooltip("From this stage, one extra boss.")] private int extraBossFromStage = 4;
        [SerializeField, Min(1), Tooltip("From this stage, bosses can be elite.")] private int eliteBossFromStage = 3;
        [SerializeField, Range(0f, 1f)] private float eliteBossChance = 0.5f;

        [Header("Final stage (StageManager.IsFinalStage)")]
        [SerializeField, Tooltip("Final bosses (avatars), one picked at random.")] private List<SpawnCard> finalBossCards = new();
        [SerializeField, Tooltip("Spawned next to the teleporter after the final event: ends the run with a victory.")]
        private RunEndSeal runEndSealPrefab;
        [SerializeField] private Vector3 sealOffset = new(6f, 0f, 0f);
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
        private RunEndSeal seal;

        private static bool IsFinalStage => StageManager.Instance != null && StageManager.Instance.IsFinalStage;

        // Last boss of the previous event: never picked twice in a row (when there is an alternative).
        private static SpawnCard lastBossCard;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => lastBossCard = null;
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
            if (IsServer && seal != null && seal.IsSpawned)
                seal.NetworkObject.Despawn();
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
            TeleporterState.Ready => IsFinalStage ? "[E] Continuer : nouvelle boucle (difficulté conservée)" : "[E] Passer à l'étape suivante",
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
            bool final = IsFinalStage && finalBossCards.Count > 0;
            var pool = final ? finalBossCards : bossCards;
            if (director == null || pool.Count == 0)
            {
                Debug.LogWarning("[Teleporter] No director or boss cards: the event runs without a boss.", this);
                return;
            }

            int level = RunDifficulty.CurrentEnemyLevel;
            int stage = StageManager.Instance != null ? StageManager.Instance.StageNumber : 1;
            int count = final ? 1 : bossCount + (stage >= extraBossFromStage ? 1 : 0);
            var picked = new List<SpawnCard>();
            for (int i = 0; i < count; i++)
            {
                var card = PickBossCard(pool, stage, picked);
                if (card == null || !TryFindBossPosition(out var position))
                    continue;
                picked.Add(card);
                lastBossCard = card;

                Vector3 facing = Vector3.ProjectOnPlane(transform.position - position, Vector3.up);
                var elite = !final && stage >= eliteBossFromStage && Random.value < eliteBossChance && GameCatalog.Instance != null
                    ? GameCatalog.Instance.RollElite()
                    : null;
                var boss = director.Spawn(card, position, facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing) : Quaternion.identity, level, elite);
                bosses.Add(boss);
                if (boss.TryGetComponent(out EnemyBrain brain))
                    foreach (var player in PlayerAvatar.All)
                        brain.Alert(player, false);
            }
        }

        /// <summary>Weighted pick among cards unlocked at this stage, avoiding the previous event's boss and the bosses
        /// already picked for this event whenever another card is available.</summary>
        private SpawnCard PickBossCard(List<SpawnCard> pool, int stage, List<SpawnCard> alreadyPicked)
        {
            var candidates = new List<SpawnCard>();
            foreach (var card in pool)
                if (card != null && card.prefab != null && card.minStage <= stage)
                    candidates.Add(card);
            if (candidates.Count == 0)
                return null;

            var fresh = candidates.FindAll(c => c != lastBossCard && !alreadyPicked.Contains(c));
            if (fresh.Count == 0)
                fresh = candidates.FindAll(c => !alreadyPicked.Contains(c));
            if (fresh.Count == 0)
                fresh = candidates;

            float total = 0f;
            foreach (var card in fresh)
                total += Mathf.Max(0.01f, card.weight);
            float roll = Random.value * total;
            foreach (var card in fresh)
            {
                roll -= Mathf.Max(0.01f, card.weight);
                if (roll <= 0f)
                    return card;
            }
            return fresh[fresh.Count - 1];
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
            if (IsFinalStage && runEndSealPrefab != null && seal == null)
            {
                Vector3 position = transform.position + sealOffset;
                if (NavMesh.SamplePosition(position, out var hit, 4f, NavMesh.AllAreas))
                    position = hit.position;
                seal = Instantiate(runEndSealPrefab, position, Quaternion.identity);
                seal.NetworkObject.Spawn(true);
            }
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
