using RiskOfMagic.Core;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Combat.Projectiles
{
    /// <summary>
    /// Scene singleton replicating projectile launches: the server simulates the authoritative copy and
    /// tells clients to spawn visual copies. The shooting client can predict its own (skipped here).
    /// </summary>
    public class ProjectileNetwork : NetworkBehaviour
    {
        public const ulong NoClient = ulong.MaxValue;

        public static ProjectileNetwork Instance { get; private set; }

        public override void OnNetworkSpawn() => Instance = this;

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Server only.</summary>
        public static void FireServer(ProjectileDefinition definition, Vector3 origin, Vector3 direction, float scale,
            DamageInfo damage, NetworkObject shooter, ulong predictedByClient = NoClient, float speedOverride = -1f, NetworkObject homingTarget = null)
        {
            Transform ignore = shooter != null ? shooter.transform : null;
            Transform target = homingTarget != null ? homingTarget.transform : null;
            Projectile.Launch(definition, origin, direction, scale, damage, true, ignore, speedOverride, target);

            if (Instance == null || !Instance.IsSpawned)
                return;
            int index = GameCatalog.Instance.IndexOf(definition);
            if (index < 0)
            {
                Debug.LogWarning($"[ProjectileNetwork] {definition.name} is not in the GameCatalog, clients won't see it.");
                return;
            }
            Instance.SpawnVisualRpc((ushort)index, origin, direction, scale, damage.SourceTeam,
                shooter != null ? shooter.NetworkObjectId : ulong.MaxValue, predictedByClient, speedOverride,
                homingTarget != null ? homingTarget.NetworkObjectId : ulong.MaxValue);
        }

        /// <summary>Local, damage-less copy used for client-side prediction.</summary>
        public static void LaunchVisual(ProjectileDefinition definition, Vector3 origin, Vector3 direction, float scale, Team team, Transform ignore,
            float speedOverride = -1f, Transform homingTarget = null)
        {
            Projectile.Launch(definition, origin, direction, scale, new DamageInfo { SourceTeam = team }, false, ignore, speedOverride, homingTarget);
        }

        [Rpc(SendTo.NotServer)]
        private void SpawnVisualRpc(ushort projectileIndex, Vector3 origin, Vector3 direction, float scale, Team team, ulong shooterId, ulong predictedByClient,
            float speedOverride, ulong homingTargetId)
        {
            if (NetworkManager.LocalClientId == predictedByClient)
                return;
            var definition = GameCatalog.Instance.GetProjectile(projectileIndex);
            if (definition == null)
                return;
            var spawned = NetworkManager.SpawnManager.SpawnedObjects;
            Transform ignore = spawned.TryGetValue(shooterId, out var shooter) ? shooter.transform : null;
            Transform target = spawned.TryGetValue(homingTargetId, out var homing) ? homing.transform : null;
            LaunchVisual(definition, origin, direction, scale, team, ignore, speedOverride, target);
        }
    }
}
