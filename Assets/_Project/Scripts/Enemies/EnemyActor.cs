using System;
using System.Collections;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    /// <summary>
    /// Root of an enemy: applies its definition, handles death and exposes a registry for spawners/score.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyActor : NetworkBehaviour
    {
        private static readonly List<EnemyActor> active = new();

        public static IReadOnlyList<EnemyActor> Active => active;
        /// <summary>Server only. Hook score, gold and XP here.</summary>
        public static event Action<EnemyActor, DamageInfo> Killed;

        [SerializeField] private EnemyDefinition definition;

        private Collider[] colliders;

        public EnemyDefinition Definition => definition;
        public Health Health { get; private set; }

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
            if (definition != null)
                Health.SetBaseMaxHealth(definition.maxHealth);
            else
                Debug.LogError($"[EnemyActor] Missing EnemyDefinition on {name}.", this);
        }

        public override void OnNetworkSpawn()
        {
            active.Add(this);
            Health.Died += OnDied;
        }

        public override void OnNetworkDespawn()
        {
            active.Remove(this);
            Health.Died -= OnDied;
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
