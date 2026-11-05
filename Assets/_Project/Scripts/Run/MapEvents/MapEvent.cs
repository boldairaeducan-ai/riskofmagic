using System.Collections.Generic;
using RiskOfMagic.Core;
using RiskOfMagic.Enemies;
using RiskOfMagic.Enemies.Elites;
using RiskOfMagic.Interaction;
using RiskOfMagic.Items;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace RiskOfMagic.Run.MapEvents
{
    /// <summary>Base of map events: registry, HUD objective line, shared server helpers (spawns, rewards).</summary>
    public abstract class MapEvent : NetworkBehaviour, IInteractable
    {
        private static readonly List<MapEvent> active = new();

        /// <summary>Spawned events (every peer).</summary>
        public static IReadOnlyList<MapEvent> Active => active;

        /// <summary>Every peer: an event was completed successfully (tomb purified, altar cleared).</summary>
        public static event System.Action<MapEventType> Completed;

        protected static void RaiseCompleted(MapEventType type) => Completed?.Invoke(type);

        protected static MapEventsConfig Config => MapEventsConfig.Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            active.Clear();
            Completed = null;
        }

        public override void OnNetworkSpawn() => active.Add(this);
        public override void OnNetworkDespawn() => active.Remove(this);

        /// <summary>HUD objective line for the local player (empty = nothing to show).</summary>
        public virtual string GetObjective(PlayerAvatar player) => string.Empty;

        public abstract bool CanInteract(PlayerAvatar player);
        public abstract string GetPrompt(PlayerAvatar player);
        public abstract void Interact(PlayerAvatar player);

        /// <summary>Server. Unlocked director card (+ optional elite affix) on the NavMesh around the event, hunting.</summary>
        protected EnemyActor SpawnEnemy(Vector2 distance, EliteAffix elite = null)
        {
            var director = CombatDirector.Instance;
            var card = director != null ? director.PickUnlockedCard() : null;
            if (card == null)
                return null;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(distance.x, distance.y);
                if (!NavMesh.SamplePosition(transform.position + new Vector3(dir.x, 0f, dir.y), out var hit, 3f, NavMesh.AllAreas))
                    continue;
                var facing = Vector3.ProjectOnPlane(transform.position - hit.position, Vector3.up);
                var enemy = director.Spawn(card, hit.position, facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing) : Quaternion.identity,
                    RunDifficulty.CurrentEnemyLevel, elite);
                var target = NearestStandingPlayer(hit.position);
                if (target != null && enemy.TryGetComponent(out EnemyBrain brain))
                    brain.Alert(target, false);
                return enemy;
            }
            return null;
        }

        /// <summary>Server. One item per player, straight into the inventory.</summary>
        protected static void GiveItemToEveryone(RarityWeights weights)
        {
            var catalog = GameCatalog.Instance;
            if (catalog == null)
                return;
            foreach (var player in PlayerAvatar.All)
            {
                var item = catalog.RollItem(weights.Roll());
                if (item != null)
                    player.Inventory.AddItem(item);
            }
        }

        protected static PlayerAvatar NearestStandingPlayer(Vector3 position)
        {
            PlayerAvatar best = null;
            float bestSqr = float.MaxValue;
            foreach (var player in PlayerAvatar.All)
            {
                if (player == null || !player.IsStanding)
                    continue;
                float sqr = (player.transform.position - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = player;
                }
            }
            return best;
        }

        protected bool IsInside(Vector3 position, float radius)
        {
            Vector3 offset = position - transform.position;
            return Mathf.Abs(offset.y) <= 4f && new Vector2(offset.x, offset.z).sqrMagnitude <= radius * radius;
        }

        protected static void DrawRing(LineRenderer ring, float radius, int segments = 64)
        {
            if (ring == null)
                return;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.15f, Mathf.Sin(angle) * radius));
            }
        }
    }
}
