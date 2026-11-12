using System;
using System.Collections.Generic;
using RiskOfMagic.Characters;
using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using RiskOfMagic.Economy;
using RiskOfMagic.Equipment;
using RiskOfMagic.Items;
using RiskOfMagic.Skills;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Player
{
    /// <summary>
    /// Root of a player: replicated character choice, local/remote setup and a registry for AI and UI.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(PlayerMotor), typeof(PlayerLook))]
    [RequireComponent(typeof(PlayerInputHandler), typeof(CharacterStats), typeof(PlayerWallet))]
    [RequireComponent(typeof(PlayerInventory))]
    public class PlayerAvatar : NetworkBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly List<PlayerAvatar> all = new();

        [SerializeField] private Renderer[] bodyRenderers = Array.Empty<Renderer>();
        [SerializeField, Min(0.05f)] private float regenTickInterval = 0.25f;

        private readonly NetworkVariable<int> characterIndex = new();
        /// <summary>Skill loadout: bit per slot = alternative skill (chosen in the lobby, from the owner's profile).</summary>
        private readonly NetworkVariable<byte> loadout = new();
        private MaterialPropertyBlock propertyBlock;
        private float regenTimer;

        public static IReadOnlyList<PlayerAvatar> All => all;
        public static PlayerAvatar Local { get; private set; }
        /// <summary>Character requested when the local player spawns (set by the lobby/UI).</summary>
        public static int PreferredCharacterIndex { get; set; }
        public static event Action<PlayerAvatar> LocalPlayerSpawned;

        public event Action<CharacterDefinition> CharacterChanged;

        public Health Health { get; private set; }
        public PlayerMotor Motor { get; private set; }
        public PlayerLook Look { get; private set; }
        public PlayerInputHandler Input { get; private set; }
        public CharacterStats Stats { get; private set; }
        public PlayerWallet Wallet { get; private set; }
        public PlayerInventory Inventory { get; private set; }
        public PlayerRunStats RunStats { get; private set; }
        public PlayerExperience Experience { get; private set; }
        public int Level => Experience != null ? Experience.Level : 1;
        public StatusEffects Status { get; private set; }
        public PlayerEquipment Equipment { get; private set; }
        public SkillController Skills { get; private set; }
        public CharacterDefinition Character { get; private set; }
        public int CharacterIndex => characterIndex.Value;
        public byte Loadout => loadout.Value;
        public bool IsAlive => Health.IsAlive;
        public PlayerDowned Downed { get; private set; }
        public bool IsDowned => Downed != null && Downed.IsDowned;
        /// <summary>Alive and standing: what enemies chase and what counts for objectives.</summary>
        public bool IsStanding => IsAlive && !IsDowned;
        public bool ControlEnabled { get; private set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
            Local = null;
            LocalPlayerSpawned = null;
        }

        public static PlayerAvatar FindByClient(ulong clientId)
        {
            foreach (var player in all)
                if (player.OwnerClientId == clientId)
                    return player;
            return null;
        }

        public static PlayerAvatar FindClosestAlive(Vector3 position, float maxDistance)
        {
            PlayerAvatar best = null;
            float bestSqr = maxDistance * maxDistance;
            foreach (var player in all)
            {
                if (!player.IsStanding)
                    continue;
                float sqr = (player.transform.position - position).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = player;
                }
            }
            return best;
        }

        private void Awake()
        {
            Health = GetComponent<Health>();
            Motor = GetComponent<PlayerMotor>();
            Look = GetComponent<PlayerLook>();
            Input = GetComponent<PlayerInputHandler>();
            Stats = GetComponent<CharacterStats>();
            Wallet = GetComponent<PlayerWallet>();
            Inventory = GetComponent<PlayerInventory>();
            Status = GetComponent<StatusEffects>();
            Equipment = GetComponent<PlayerEquipment>();
            Downed = GetComponent<PlayerDowned>();
            RunStats = GetComponent<PlayerRunStats>();
            Experience = GetComponent<PlayerExperience>();
            Skills = GetComponent<SkillController>();
        }

        public override void OnNetworkSpawn()
        {
            all.RemoveAll(p => p == null || p == this || (p.OwnerClientId == OwnerClientId && !p.IsSpawned)); // stale entries
            all.Add(this);
            Health.Died += OnDied;
            Health.Revived += OnRevived;
            if (IsServer && RiskOfMagic.Flow.RunSettings.TryGetCharacter(OwnerClientId, out int lobbyCharacter)
                && GameCatalog.Instance != null && GameCatalog.Instance.GetCharacter(lobbyCharacter) != null)
                characterIndex.Value = lobbyCharacter; // choice made in the lobby
            if (IsServer && RiskOfMagic.Flow.RunSettings.TryGetLoadout(OwnerClientId, out byte lobbyLoadout))
                loadout.Value = lobbyLoadout;
            characterIndex.OnValueChanged += OnCharacterIndexChanged;
            loadout.OnValueChanged += OnLoadoutChanged;
            Inventory.ItemsChanged += RecalculateStats;
            if (Downed != null)
                Downed.Changed += UpdateControl;
            if (Status != null)
                Status.Changed += RecalculateStats;
            if (Experience != null)
                Experience.LevelChanged += OnLevelChanged;
            ApplyCharacter(characterIndex.Value);

            Input.enabled = IsOwner;
            Look.SetLocal(IsOwner);
            if (!IsOwner)
                return;

            Local = this;
            if (PlayerSpawnPoint.TryGet((int)OwnerClientId, out var pose))
                Motor.Teleport(pose.position, pose.rotation);
            RequestCharacter(PreferredCharacterIndex);
            LocalPlayerSpawned?.Invoke(this);
        }

        /// <summary>Destroyed without a despawn (scene change, shutdown): never leave a ghost in the registry.</summary>
        public override void OnDestroy()
        {
            all.Remove(this);
            if (Local == this)
                Local = null;
            base.OnDestroy();
        }

        public override void OnNetworkDespawn()
        {
            all.Remove(this);
            Health.Died -= OnDied;
            Health.Revived -= OnRevived;
            characterIndex.OnValueChanged -= OnCharacterIndexChanged;
            loadout.OnValueChanged -= OnLoadoutChanged;
            Inventory.ItemsChanged -= RecalculateStats;
            if (Downed != null)
                Downed.Changed -= UpdateControl;
            if (Status != null)
                Status.Changed -= RecalculateStats;
            if (Experience != null)
                Experience.LevelChanged -= OnLevelChanged;
            if (Local == this)
                Local = null;
        }

        /// <summary>Owner only.</summary>
        public void RequestCharacter(int index)
        {
            if (IsOwner)
                RequestCharacterRpc(index);
        }

        [Rpc(SendTo.Server)]
        private void RequestCharacterRpc(int index)
        {
            if (GameCatalog.Instance != null && GameCatalog.Instance.GetCharacter(index) != null)
                characterIndex.Value = index;
        }

        /// <summary>Server → owner: move back to a spawn point (new stage). Movement is owner-authoritative.</summary>
        [Rpc(SendTo.Owner)]
        public void TeleportToSpawnRpc()
        {
            if (PlayerSpawnPoint.TryGet((int)OwnerClientId, out var pose))
                Motor.Teleport(pose.position, pose.rotation);
            RiskOfMagic.UI.LoadingScreen.NotifyLocalPlayerPlaced();
        }

        /// <summary>Server → owner: push (vortex, knockback). Movement is owner-authoritative.</summary>
        [Rpc(SendTo.Owner)]
        public void PushRpc(Vector3 impulse) => Motor.AddImpulse(impulse);

        public void SetControlEnabled(bool value)
        {
            ControlEnabled = value;
            Motor.ControlEnabled = value;
        }

        private void OnCharacterIndexChanged(int previous, int current) => ApplyCharacter(current);

        private void OnLevelChanged(int newLevel) => RecalculateStats();

        private void OnLoadoutChanged(byte previous, byte current)
        {
            if (Skills != null && Character != null)
                Skills.SetLoadout(Character, current);
        }

        [Rpc(SendTo.Server)]
        private void RequestLoadoutRpc(byte mask) => loadout.Value = mask;

        private void ApplyCharacter(int index)
        {
            var catalog = GameCatalog.Instance;
            var definition = catalog != null ? catalog.GetCharacter(index) : null;
            if (definition == null || definition == Character)
                return;

            Character = definition;
            if (definition.movement != null)
                Motor.SetConfig(definition.movement);
            if (Skills != null)
                Skills.SetLoadout(definition, loadout.Value);
            RecalculateStats();
            if (IsOwner && IsSpawned)
                RequestLoadoutRpc(RiskOfMagic.Meta.MetaProgression.GetLoadout(definition)); // the owner's own profile

            ApplyBodyColor(definition.bodyColor);
            CharacterChanged?.Invoke(definition);
        }

        /// <summary>Character base stats + items. Runs on every peer.</summary>
        public void RecalculateStats()
        {
            if (Character == null)
                return;

            Stats.Recalculate(Character, Inventory, Status, Level);
            Motor.SpeedMultiplier = Stats.MoveSpeedMultiplier;
            Motor.SprintBonus = Stats.SprintSpeedBonus;
            Motor.ExtraJumps = Stats.ExtraJumps;
            if (Skills != null)
                Skills.SetBonusCharges(Stats.BonusSkillCharges);

            if (!IsServer)
                return;
            Health.FlatDamageReduction = Stats.FlatDamageReduction;
            Health.DamageTakenMultiplier = Stats.DamageTakenMultiplier;
            Health.SetBaseMaxHealth(Stats.MaxHealth);
            if (Health.IsSpawned && !Mathf.Approximately(Health.Max, Stats.MaxHealth))
                Health.SetMaxHealth(Stats.MaxHealth);
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || !IsStanding || Stats.HealthRegen <= 0f)
                return;
            regenTimer += Time.deltaTime;
            if (regenTimer < regenTickInterval)
                return;
            Health.Heal(Stats.HealthRegen * regenTimer);
            regenTimer = 0f;
        }

        /// <summary>Hides the placeholder capsule when a real model is used (PlayerAnimationDriver).</summary>
        public void SetPlaceholderVisible(bool visible)
        {
            foreach (var body in bodyRenderers)
                if (body != null)
                    body.enabled = visible;
        }

        private void ApplyBodyColor(Color color)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            foreach (var body in bodyRenderers)
            {
                if (body == null)
                    continue;
                body.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(ColorId, color);
                propertyBlock.SetColor(BaseColorId, color);
                body.SetPropertyBlock(propertyBlock);
            }
        }

        /// <summary>Control follows health and the downed state, whatever order their events arrive in.</summary>
        private void UpdateControl() => SetControlEnabled(IsStanding);

        private void OnDied(DamageInfo info) => UpdateControl();
        private void OnRevived() => UpdateControl();
    }
}
