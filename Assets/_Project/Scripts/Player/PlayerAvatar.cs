using System;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Player
{
    /// <summary>
    /// Root of a player: wires local/remote setup and keeps a registry of players for AI and UI.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(PlayerMotor), typeof(PlayerLook))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerAvatar : NetworkBehaviour
    {
        private static readonly List<PlayerAvatar> all = new();

        public static IReadOnlyList<PlayerAvatar> All => all;
        public static PlayerAvatar Local { get; private set; }
        public static event Action<PlayerAvatar> LocalPlayerSpawned;

        public Health Health { get; private set; }
        public PlayerMotor Motor { get; private set; }
        public PlayerLook Look { get; private set; }
        public PlayerInputHandler Input { get; private set; }
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
        }

        public override void OnNetworkSpawn()
        {
            all.Add(this);
            Health.Died += OnDied;
            Health.Revived += OnRevived;

            Input.enabled = IsOwner;
            Look.SetLocal(IsOwner);
            if (!IsOwner)
                return;

            Local = this;
            if (PlayerSpawnPoint.TryGet((int)OwnerClientId, out var pose))
                Motor.Teleport(pose.position, pose.rotation);
            LocalPlayerSpawned?.Invoke(this);
        }

        public override void OnNetworkDespawn()
        {
            all.Remove(this);
            Health.Died -= OnDied;
            Health.Revived -= OnRevived;
            if (Local == this)
                Local = null;
        }

        public void SetControlEnabled(bool value)
        {
            ControlEnabled = value;
            Motor.ControlEnabled = value;
        }

        private void OnDied(DamageInfo info) => SetControlEnabled(false);
        private void OnRevived() => SetControlEnabled(true);
    }
}
