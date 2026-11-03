using System;
using System.Collections;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Run;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
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

        [SerializeField] private EnemyDefinition definition;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Color frozenTint = new(0.45f, 0.75f, 1f);

        private readonly NetworkVariable<int> level = new(1);
        private readonly NetworkVariable<double> frozenUntil = new();
        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;
        private bool frozenVisual;
        private Collider[] colliders;
        private int pendingLevel = 1;

        public EnemyDefinition Definition => definition;
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
        public float DamageMultiplier => definition != null ? 1f + definition.damagePerLevel * (Level - 1) : 1f;

        public uint GoldReward => definition != null
            ? (uint)Mathf.Max(0, Mathf.RoundToInt(definition.goldReward * RunDifficulty.CurrentCoefficient))
            : 0u;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            active.Clear();
            Killed = null;
        }

        private void Awake()
        {
            Health = GetComponent<Health>();
            colliders = GetComponentsInChildren<Collider>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            if (definition == null)
                Debug.LogError($"[EnemyActor] Missing EnemyDefinition on {name}.", this);
            Initialize(1);
        }

        /// <summary>Server, before Spawn().</summary>
        public void Initialize(int enemyLevel)
        {
            pendingLevel = Mathf.Max(1, enemyLevel);
            if (definition != null)
                Health.SetBaseMaxHealth(definition.maxHealth * (1f + definition.healthPerLevel * (pendingLevel - 1)));
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                level.Value = pendingLevel;
            active.Add(this);
            Health.Died += OnDied;
        }

        public override void OnNetworkDespawn()
        {
            active.Remove(this);
            Health.Died -= OnDied;
        }

        private void Update()
        {
            bool frozen = IsFrozen && Health.IsAlive;
            if (frozen == frozenVisual)
                return;
            frozenVisual = frozen;

            propertyBlock ??= new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == null)
                    continue;
                if (frozen)
                {
                    r.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor(ColorId, frozenTint);
                    propertyBlock.SetColor(BaseColorId, frozenTint);
                    r.SetPropertyBlock(propertyBlock);
                }
                else
                {
                    r.SetPropertyBlock(null);
                }
            }
        }

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
