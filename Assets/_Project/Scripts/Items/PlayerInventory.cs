using System;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Items
{
    /// <summary>
    /// Replicated item stacks (index = GameCatalog item index). The server dispatches item procs,
    /// deferred to the next frame to avoid re-entrant damage chains.
    /// </summary>
    public class PlayerInventory : NetworkBehaviour, IHealthHooks, IDamageDealerHooks
    {
        private readonly struct PendingProc
        {
            public PendingProc(Health victim, DamageInfo info, Vector3 position, bool isKill)
            {
                Victim = victim;
                Info = info;
                Position = position;
                IsKill = isKill;
            }

            public readonly Health Victim;
            public readonly DamageInfo Info;
            public readonly Vector3 Position;
            public readonly bool IsKill;
        }

        private readonly List<PendingProc> pending = new();
        private readonly List<PendingProc> processing = new();
        private readonly Dictionary<ItemDefinition, int> counters = new();
        private NetworkList<int> counts;
        private PlayerAvatar avatar;

        /// <summary>Every peer.</summary>
        public event Action ItemsChanged;
        /// <summary>Raised on the owner's machine only: (item, new stack count).</summary>
        public static event Action<ItemDefinition, int> LocalItemGained;

        public int SlotCount => counts.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => LocalItemGained = null;

        private void Awake()
        {
            avatar = GetComponent<PlayerAvatar>();
            counts = new NetworkList<int>();
        }

        public override void OnNetworkSpawn()
        {
            counts.OnListChanged += OnListChanged;
            if (IsServer)
            {
                var catalog = GameCatalog.Instance;
                while (catalog != null && counts.Count < catalog.Items.Count)
                    counts.Add(0);
                Health.ServerDamaged += OnServerDamaged;
                Health.ServerKilled += OnServerKilled;
            }
            ItemsChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            counts.OnListChanged -= OnListChanged;
            Health.ServerDamaged -= OnServerDamaged;
            Health.ServerKilled -= OnServerKilled;
            pending.Clear();
        }

        public int GetCount(ItemDefinition item) => GetCountAt(GameCatalog.Instance.IndexOf(item));

        public int GetCountAt(int index) => index >= 0 && index < counts.Count ? counts[index] : 0;

        /// <summary>Server only.</summary>
        public void AddItem(ItemDefinition item, int amount = 1)
        {
            if (!IsServer || item == null || amount <= 0)
                return;
            int index = GameCatalog.Instance.IndexOf(item);
            if (index < 0)
            {
                Debug.LogWarning($"[PlayerInventory] {item.name} is not in the GameCatalog.");
                return;
            }
            while (counts.Count <= index)
                counts.Add(0);
            counts[index] += amount;
        }

        /// <summary>Server only.</summary>
        public void RemoveItem(ItemDefinition item, int amount = 1)
        {
            if (!IsServer || item == null)
                return;
            int index = GameCatalog.Instance.IndexOf(item);
            if (index >= 0 && index < counts.Count && counts[index] > 0)
                counts[index] = Mathf.Max(0, counts[index] - amount);
        }

        /// <summary>Server-side per-item counter for items that need state (e.g. "every 5 hits").</summary>
        public int IncrementCounter(ItemDefinition item)
        {
            counters.TryGetValue(item, out int value);
            counters[item] = ++value;
            return value;
        }

        /// <summary>Server only (new run).</summary>
        public void Clear()
        {
            if (!IsServer)
                return;
            for (int i = 0; i < counts.Count; i++)
                if (counts[i] != 0)
                    counts[i] = 0;
            counters.Clear();
        }

        private void OnListChanged(NetworkListEvent<int> change)
        {
            if (IsOwner && change.Type == NetworkListEvent<int>.EventType.Value && change.Value > change.PreviousValue)
                LocalItemGained?.Invoke(GameCatalog.Instance.GetItem(change.Index), change.Value);
            ItemsChanged?.Invoke();
        }

        private bool IsFromMe(in DamageInfo info) => info.HasSource && info.SourceId == NetworkObjectId;

        private void OnServerDamaged(Health victim, DamageInfo info)
        {
            if (victim == avatar.Health)
                ForEachItem((item, stacks) => item.OnOwnerDamagedServer(avatar, info, stacks));

            if (!IsFromMe(info) || info.ProcCoefficient <= 0f)
                return;
            Vector3 position = info.Point != Vector3.zero ? info.Point : TargetFinder.Center(victim);
            pending.Add(new PendingProc(victim, info, position, false));
        }

        private void OnServerKilled(Health victim, DamageInfo info)
        {
            if (IsFromMe(info))
                pending.Add(new PendingProc(victim, info, TargetFinder.Center(victim), true));
        }

        // ---------------------------------------------------------------- Hooks (server)

        public float ModifyOutgoingDamage(Health victim, in DamageInfo info, float amount)
        {
            var catalog = GameCatalog.Instance;
            for (int i = 0; i < counts.Count; i++)
            {
                int stacks = counts[i];
                var item = stacks > 0 ? catalog.GetItem(i) : null;
                if (item != null)
                    amount = item.ModifyOutgoingDamage(avatar, victim, info, amount, stacks);
            }

            // Shadow: the first hit out of stealth is doubled, an ambush makes the next hit a critical (x2).
            var status = avatar.Status;
            if (status != null && info.ProcCoefficient > 0f)
            {
                if (status.Has(StatusType.Stealth))
                {
                    amount *= 2f;
                    status.Clear(StatusType.Stealth);
                }
                if (status.Has(StatusType.Ambush))
                {
                    if (!info.IsCritical)
                        amount *= 2f;
                    status.Clear(StatusType.Ambush);
                }
            }
            return amount;
        }

        public float ModifyIncomingDamage(Health self, in DamageInfo info, float amount) => amount;

        public bool TryPreventDeath(Health self, in DamageInfo info, out float healthFraction)
        {
            healthFraction = 0f;
            var catalog = GameCatalog.Instance;
            for (int i = 0; i < counts.Count; i++)
            {
                int stacks = counts[i];
                var item = stacks > 0 ? catalog.GetItem(i) : null;
                if (item == null || !item.TryPreventDeath(avatar, stacks, out healthFraction))
                    continue;
                RemoveItem(item);
                return true;
            }
            return false;
        }

        private void ForEachItem(System.Action<ItemDefinition, int> action)
        {
            var catalog = GameCatalog.Instance;
            for (int i = 0; i < counts.Count; i++)
            {
                int stacks = counts[i];
                var item = stacks > 0 ? catalog.GetItem(i) : null;
                if (item != null)
                    action(item, stacks);
            }
        }

        private const float TickInterval = 0.5f;
        private float nextTick;

        private void Update()
        {
            if (IsServer && IsSpawned && Time.time >= nextTick && avatar.IsStanding)
            {
                nextTick = Time.time + TickInterval;
                var tickCatalog = GameCatalog.Instance;
                for (int i = 0; i < counts.Count && tickCatalog != null; i++)
                {
                    int stacks = counts[i];
                    var item = stacks > 0 ? tickCatalog.GetItem(i) : null;
                    if (item != null)
                        item.OnServerTick(avatar, stacks, TickInterval);
                }
            }

            if (!IsServer || pending.Count == 0)
                return;

            processing.Clear();
            processing.AddRange(pending);
            pending.Clear();
            if (!avatar.IsAlive)
                return;

            var catalog = GameCatalog.Instance;
            foreach (var request in processing)
            {
                var proc = new ProcEvent(request.Victim, request.Info, request.Position);
                for (int i = 0; i < counts.Count; i++)
                {
                    int stacks = counts[i];
                    var item = stacks > 0 ? catalog.GetItem(i) : null;
                    if (item == null)
                        continue;
                    if (request.IsKill)
                        item.OnKillServer(avatar, proc, stacks);
                    else
                        item.OnHitServer(avatar, proc, stacks);
                }
            }
        }
    }
}
