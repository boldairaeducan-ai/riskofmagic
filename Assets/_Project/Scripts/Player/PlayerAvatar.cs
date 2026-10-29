using System;
using System.Collections.Generic;
using RiskOfMagic.Characters;
using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using RiskOfMagic.Economy;
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
    public class PlayerAvatar : NetworkBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly List<PlayerAvatar> all = new();

        [SerializeField] private Renderer[] bodyRenderers = Array.Empty<Renderer>();

        private readonly NetworkVariable<int> characterIndex = new();
        private MaterialPropertyBlock propertyBlock;

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
        public SkillController Skills { get; private set; }
        public CharacterDefinition Character { get; private set; }
        public int CharacterIndex => characterIndex.Value;
        public bool IsAlive => Health.IsAlive;
        public bool ControlEnabled { get; private set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
            Local = null;
            LocalPlayerSpawned = null;
        }

        public static PlayerAvatar FindClosestAlive(Vector3 position, float maxDistance)
        {
            PlayerAvatar best = null;
            float bestSqr = maxDistance * maxDistance;
            foreach (var player in all)
            {
                if (!player.IsAlive)
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
            Skills = GetComponent<SkillController>();
        }

        public override void OnNetworkSpawn()
        {
            all.Add(this);
            Health.Died += OnDied;
            Health.Revived += OnRevived;
            characterIndex.OnValueChanged += OnCharacterIndexChanged;
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

        public override void OnNetworkDespawn()
        {
            all.Remove(this);
            Health.Died -= OnDied;
            Health.Revived -= OnRevived;
            characterIndex.OnValueChanged -= OnCharacterIndexChanged;
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

        public void SetControlEnabled(bool value)
        {
            ControlEnabled = value;
            Motor.ControlEnabled = value;
        }

        private void OnCharacterIndexChanged(int previous, int current) => ApplyCharacter(current);

        private void ApplyCharacter(int index)
        {
            var catalog = GameCatalog.Instance;
            var definition = catalog != null ? catalog.GetCharacter(index) : null;
            if (definition == null || definition == Character)
                return;

            Character = definition;
            Stats.ApplyDefinition(definition);
            if (definition.movement != null)
                Motor.SetConfig(definition.movement);
            if (Skills != null)
                Skills.SetLoadout(definition);

            if (IsServer)
            {
                Health.SetBaseMaxHealth(definition.maxHealth);
                if (Health.IsSpawned)
                    Health.SetMaxHealth(definition.maxHealth);
            }

            ApplyBodyColor(definition.bodyColor);
            CharacterChanged?.Invoke(definition);
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

        private void OnDied(DamageInfo info) => SetControlEnabled(false);
        private void OnRevived() => SetControlEnabled(true);
    }
}
