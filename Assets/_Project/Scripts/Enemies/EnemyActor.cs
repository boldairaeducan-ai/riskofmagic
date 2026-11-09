using System;
using System.Collections;
using System.Collections.Generic;
using RiskOfMagic.Audio;
using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using RiskOfMagic.Enemies.Elites;
using RiskOfMagic.Run;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    public enum EnemyAnimationCue : byte
    {
        Dodge,
        Leap
    }

    /// <summary>
    /// Root of an enemy: applies its definition and level, handles death and exposes a registry.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyActor : NetworkBehaviour
    {
        private static readonly List<EnemyActor> active = new();

        public static IReadOnlyList<EnemyActor> Active => active;
        /// <summary>Server only. Hook gold, XP, item procs here.</summary>
        public static event Action<EnemyActor, DamageInfo> Killed;
        /// <summary>Every peer: an enemy got frozen (or its freeze extended).</summary>
        public static event Action<EnemyActor> AnyFrozen;

        [SerializeField] private EnemyDefinition definition;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Color frozenTint = new(0.45f, 0.75f, 1f);
        [SerializeField] private Color poisonedTint = new(0.35f, 0.85f, 0.25f);
        [SerializeField] private Color cursedTint = new(0.55f, 0.25f, 0.6f);
        [SerializeField] private Color phasedTint = new(0.9f, 0.9f, 1f);

        private readonly NetworkVariable<int> level = new(1);
        private readonly NetworkVariable<double> frozenUntil = new();
        private readonly NetworkVariable<int> eliteIndex = new(-1);
        private readonly NetworkVariable<double> phasedUntil = new();
        private EliteAffix pendingElite;
        private readonly NetworkVariable<bool> illusion = new();
        private bool pendingIllusion;
        private float illusionMaxHealth;

        /// <summary>Damage dealt by illusions (ChaosAvatar split).</summary>
        public const float IllusionDamageMultiplier = 0.4f;
        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;
        private Color? currentTint;
        private Color flashColor = Color.white;
        private float flashUntil;
        private float flashDuration;
        private bool wasFlashing;
        [SerializeField, Tooltip("Extra brightness at the peak of a hit flash (HDR, feeds the bloom).")] private float flashOverbright = 1.5f;
        private Collider[] colliders;
        private int pendingLevel = 1;

        public EnemyDefinition Definition => definition;

        /// <summary>Every peer: server-side movement cues for animation (dodge, leap).</summary>
        public event Action<EnemyAnimationCue> AnimationCue;

        /// <summary>Server only.</summary>
        public void BroadcastCue(EnemyAnimationCue cue)
        {
            if (IsServer && IsSpawned)
                AnimationCueRpc(cue);
        }

        [Rpc(SendTo.Everyone)]
        private void AnimationCueRpc(EnemyAnimationCue cue) => AnimationCue?.Invoke(cue);

        /// <summary>Every peer: swaps the placeholder visuals for a real model (tints then apply to the model).</summary>
        public void UseModel(GameObject model)
        {
            if (model == null)
                return;
            renderers ??= GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
                if (r != null && !r.transform.IsChildOf(model.transform))
                    r.enabled = false;
            renderers = model.GetComponentsInChildren<Renderer>(true);
            currentTint = null;
        }
        /// <summary>Every peer once spawned (null = normal enemy).</summary>
        public EliteAffix Elite { get; private set; }
        public bool IsElite => Elite != null;
        /// <summary>Copy created by a boss (no reward, not tracked as a boss).</summary>
        public bool IsIllusion => IsSpawned ? illusion.Value : pendingIllusion;

        /// <summary>Server, before Spawn().</summary>
        public void MakeIllusion(float maxHealth)
        {
            pendingIllusion = true;
            illusionMaxHealth = maxHealth;
            Initialize(pendingLevel);
        }
        /// <summary>Invulnerable (Éthéré elite), tinted on every peer.</summary>
        public bool IsPhased => IsSpawned && NetworkManager.ServerTime.Time < phasedUntil.Value;

        /// <summary>"Goule" or "Goule ardente".</summary>
        public string DisplayName
        {
            get
            {
                string baseName = definition != null ? definition.displayName : name;
                return Elite != null ? $"{baseName} {Elite.GetName(definition != null && definition.feminineName)}" : baseName;
            }
        }

        private EliteAffix CurrentElite => Elite != null ? Elite : pendingElite;
        public bool IsBoss => definition != null && definition.isBoss;
        private float EliteHealthMultiplier => CurrentElite == null ? 1f : IsBoss ? CurrentElite.bossHealthMultiplier : CurrentElite.healthMultiplier;
        private float EliteDamageMultiplier => CurrentElite == null ? 1f : IsBoss ? CurrentElite.bossDamageMultiplier : CurrentElite.damageMultiplier;
        private float EliteGoldMultiplier => CurrentElite == null ? 1f : IsBoss ? CurrentElite.bossGoldMultiplier : CurrentElite.goldMultiplier;

        /// <summary>Server, before Spawn() (CombatDirector.Spawn). Applies the health multiplier.</summary>
        public void MakeElite(EliteAffix affix)
        {
            pendingElite = affix;
            Initialize(pendingLevel);
        }

        /// <summary>Server only. Ignores hits for the duration.</summary>
        public void Phase(float duration)
        {
            if (!IsServer || !Health.IsAlive)
                return;
            double until = NetworkManager.ServerTime.Time + duration;
            phasedUntil.Value = until;
            Health.ServerInvulnerableUntil = until;
        }
        public Health Health { get; private set; }
        public int Level => IsSpawned ? level.Value : pendingLevel;
        /// <summary>No movement nor attacks (EnemyBrain), tinted on every peer.</summary>
        public bool IsFrozen => IsSpawned && NetworkManager.ServerTime.Time < frozenUntil.Value;

        /// <summary>Server only. Duration is reduced by EnemyDefinition.controlResistance.</summary>
        public void Freeze(float duration)
        {
            if (!IsServer || !Health.IsAlive)
                return;
            float resistance = definition != null ? definition.controlResistance : 0f;
            double until = NetworkManager.ServerTime.Time + duration * (1f - resistance);
            if (until > frozenUntil.Value)
                frozenUntil.Value = until;
        }
        public StatusEffects Status { get; private set; }
        public float DamageMultiplier => (definition != null ? 1f + definition.damagePerLevel * (Level - 1) : 1f)
                                         * EliteDamageMultiplier
                                         * (IsIllusion ? IllusionDamageMultiplier : 1f)
                                         * (Status != null ? Status.DamageDealtMultiplier : 1f);
        public float MoveSpeedMultiplier => Status != null ? Status.MoveSpeedMultiplier : 1f;
        public float AttackSpeedMultiplier => Status != null ? Status.AttackSpeedMultiplier : 1f;

        public uint GoldReward => definition != null && !IsIllusion
            ? (uint)Mathf.Max(0, Mathf.RoundToInt(definition.goldReward * EliteGoldMultiplier * RunDifficulty.CurrentCoefficient))
            : 0u;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            active.Clear();
            Killed = null;
            AnyFrozen = null;
        }

        private void Awake()
        {
            Health = GetComponent<Health>();
            colliders = GetComponentsInChildren<Collider>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            Status = GetComponent<StatusEffects>();
            if (GetComponent<RiskOfMagic.Feedback.EnemyHitFeedback>() == null)
                gameObject.AddComponent<RiskOfMagic.Feedback.EnemyHitFeedback>(); // cosmetic hit reactions, every peer
            if (definition == null)
                Debug.LogError($"[EnemyActor] Missing EnemyDefinition on {name}.", this);
            Initialize(1);
        }

        /// <summary>Server, before Spawn().</summary>
        public void Initialize(int enemyLevel)
        {
            pendingLevel = Mathf.Max(1, enemyLevel);
            if (pendingIllusion)
                Health.SetBaseMaxHealth(illusionMaxHealth);
            else if (definition != null)
                Health.SetBaseMaxHealth(definition.maxHealth * (1f + definition.healthPerLevel * (pendingLevel - 1)) * EliteHealthMultiplier);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                level.Value = pendingLevel;
                illusion.Value = pendingIllusion;
                if (pendingElite != null && GameCatalog.Instance != null)
                    eliteIndex.Value = GameCatalog.Instance.IndexOf(pendingElite);
            }
            eliteIndex.OnValueChanged += OnEliteChanged;
            frozenUntil.OnValueChanged += OnFrozenChanged;
            ApplyElite(eliteIndex.Value);
            active.Add(this);
            Health.Died += OnDied;
            if (definition != null)
                AudioService.PlayAt(definition.spawnSound, transform.position);
        }

        public override void OnNetworkDespawn()
        {
            active.Remove(this);
            Health.Died -= OnDied;
            eliteIndex.OnValueChanged -= OnEliteChanged;
            frozenUntil.OnValueChanged -= OnFrozenChanged;
        }

        private void OnEliteChanged(int previous, int current) => ApplyElite(current);

        private void OnFrozenChanged(double previous, double current)
        {
            if (current > previous)
                AnyFrozen?.Invoke(this);
        }

        private void ApplyElite(int index)
        {
            if (Elite != null || index < 0 || GameCatalog.Instance == null)
                return;
            Elite = GameCatalog.Instance.GetElite(index);
            if (Elite != null)
                EliteRuntime.Attach(this, Elite);
        }

        private void Update()
        {
            // Fell off the map (push, leap): dies (server).
            if (IsServer && IsSpawned && Health.IsAlive && StageBounds.IsBelowMap(transform.position))
            {
                var fall = DamageInfo.Create(Health.Max * 10f, DamageType.Physical, null, Team.Neutral, transform.position, Vector3.down);
                fall.IgnoreModifiers = true;
                fall.ProcCoefficient = 0f;
                Health.ApplyDamage(fall);
            }

            Color? tint = null;
            if (IsSpawned && Health.IsAlive)
            {
                if (IsPhased)
                    tint = phasedTint;
                else if (IsFrozen)
                    tint = frozenTint;
                else if (Status != null && Status.Has(StatusType.Curse))
                    tint = cursedTint;
                else if (Health.HasDot(DamageType.Poison))
                    tint = poisonedTint;
                else if (Elite != null)
                    tint = Elite.tint;
            }

            float flash = flashUntil > Time.time && flashDuration > 0f ? (flashUntil - Time.time) / flashDuration : 0f;
            bool flashing = flash > 0f;
            if (!flashing && !wasFlashing && tint == currentTint)
                return;
            wasFlashing = flashing;
            currentTint = tint;

            propertyBlock ??= new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == null)
                    continue;
                if (!flashing && !tint.HasValue)
                {
                    r.SetPropertyBlock(null);
                    continue;
                }
                Color color = tint ?? (r.sharedMaterial != null ? r.sharedMaterial.color : Color.white);
                if (flashing)
                {
                    float k = flash * flash; // sharp attack, quick release
                    color = Color.Lerp(color, flashColor, k) * (1f + k * flashOverbright); // HDR: also brightens textured models
                }
                r.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(ColorId, color);
                propertyBlock.SetColor(BaseColorId, color);
                r.SetPropertyBlock(propertyBlock);
            }
        }

        /// <summary>Every peer, cosmetic: brief flash of every renderer (hit feedback).</summary>
        public void Flash(Color color, float duration)
        {
            flashColor = color;
            flashDuration = Mathf.Max(0.01f, duration);
            flashUntil = Time.time + flashDuration;
        }

        /// <summary>Renderers currently showing this enemy (placeholder or model).</summary>
        public IReadOnlyList<Renderer> Renderers => renderers;

        private void OnDied(DamageInfo killingBlow)
        {
            foreach (var c in colliders)
                c.enabled = false;

            if (!IsServer)
                return;
            Killed?.Invoke(this, killingBlow);
            StartCoroutine(DespawnAfterDelay(definition != null ? definition.despawnDelay : 0f));
        }

        private IEnumerator DespawnAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (IsSpawned)
                NetworkObject.Despawn();
        }
    }
}
