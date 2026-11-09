using System;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Combat
{
    public enum StatusType : byte
    {
        /// <summary>Less damage dealt, slower, more damage taken.</summary>
        Curse,
        /// <summary>Attack and move speed bonus (Corne de ralliement).</summary>
        Rally,
        /// <summary>Stacking attack/move speed, lost when hit (Cor de guerre).</summary>
        Warhorn,
        /// <summary>Slowed (Glacial elite blizzard).</summary>
        Chilled,
        /// <summary>Max health reduced by 1% per stack (cursed chest), until the end of the stage.</summary>
        Withered,
        /// <summary>Slowed while channelling a skill (Rayon du néant).</summary>
        Channeling,
        /// <summary>Cannot move (Enracinement).</summary>
        Rooted,
        /// <summary>+1% attack speed per stack (Fiole d'adrénaline).</summary>
        Adrenaline,
        /// <summary>Damage bonus aura (Bannière de guerre).</summary>
        Banner,
        /// <summary>+1% damage per stack until the end of the stage (Couronne des âmes).</summary>
        SoulHarvest,
        /// <summary>Invisible to enemies, faster; the first hit dealt is boosted and ends it (Voile d'ombre).</summary>
        Stealth,
        /// <summary>Takes more damage from every source (Marque de mort).</summary>
        Marked,
        /// <summary>The next hit dealt is a guaranteed critical (Pas de l'ombre).</summary>
        Ambush,
        /// <summary>Map secret reward: more damage until the end of the stage.</summary>
        RuneBlessing
    }

    public struct StatusEntry : INetworkSerializeByMemcpy, IEquatable<StatusEntry>
    {
        public StatusType Type;
        public byte Stacks;
        public double Until;

        public bool Equals(StatusEntry other) => Type == other.Type && Stacks == other.Stacks && Until.Equals(other.Until);
        public override bool Equals(object obj) => obj is StatusEntry other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Type, Stacks, Until);
    }

    /// <summary>
    /// Replicated timed/stacking status effects (server writes, every peer reads).
    /// Exposes the resulting multipliers; incoming damage modifiers apply through IHealthHooks.
    /// </summary>
    public class StatusEffects : NetworkBehaviour, IHealthHooks
    {
        [Header("Curse")]
        [SerializeField] private float curseDamageDealtMultiplier = 0.5f;
        [SerializeField] private float curseSpeedMultiplier = 0.9f;
        [SerializeField] private float curseDamageTakenMultiplier = 1.3f;

        [Header("Chilled")]
        [SerializeField] private float chillSpeedMultiplier = 0.7f;
        [SerializeField] private float channelingSpeedMultiplier = 0.5f;
        [SerializeField] private float bannerDamageBonus = 0.2f;
        [SerializeField] private float stealthSpeedBonus = 0.3f;
        [SerializeField] private float markedDamageTakenMultiplier = 1.5f;
        [SerializeField] private float runeBlessingDamageBonus = 0.3f;

        [Header("Buffs")]
        [SerializeField] private float rallyBonus = 0.4f;
        [SerializeField] private float warhornBonusPerStack = 0.15f;

        private NetworkList<StatusEntry> entries;

        /// <summary>Every peer.</summary>
        public event Action Changed;

        private double Now => IsSpawned ? NetworkManager.ServerTime.Time : 0d;

        /// <summary>Withered: 1% max health per stack (at least 5% left).</summary>
        public float MaxHealthMultiplier => Mathf.Max(0.05f, 1f - GetStacks(StatusType.Withered) / 100f);
        public float DamageDealtMultiplier => (Has(StatusType.Curse) ? curseDamageDealtMultiplier : 1f)
                                              * (Has(StatusType.Banner) ? 1f + bannerDamageBonus : 1f)
                                              * (1f + GetStacks(StatusType.SoulHarvest) / 100f)
                                              * (Has(StatusType.RuneBlessing) ? 1f + runeBlessingDamageBonus : 1f);
        public float MoveSpeedMultiplier => Has(StatusType.Rooted) ? 0f
            : (Has(StatusType.Curse) ? curseSpeedMultiplier : 1f) * (Has(StatusType.Chilled) ? chillSpeedMultiplier : 1f)
              * (Has(StatusType.Channeling) ? channelingSpeedMultiplier : 1f) * (1f + SpeedBonus)
              * (Has(StatusType.Stealth) ? 1f + stealthSpeedBonus : 1f);
        public float AttackSpeedMultiplier => (Has(StatusType.Curse) ? curseSpeedMultiplier : 1f) * (1f + SpeedBonus)
                                              * (1f + GetStacks(StatusType.Adrenaline) / 100f);

        private float SpeedBonus => (Has(StatusType.Rally) ? rallyBonus : 0f) + GetStacks(StatusType.Warhorn) * warhornBonusPerStack;

        private void Awake() => entries = new NetworkList<StatusEntry>();

        public override void OnNetworkSpawn() => entries.OnListChanged += OnEntriesChanged;
        public override void OnNetworkDespawn() => entries.OnListChanged -= OnEntriesChanged;

        public bool Has(StatusType type) => GetStacks(type) > 0;

        /// <summary>Replicated entries (UI). Expired ones may linger until the server removes them: check Until.</summary>
        public int EntryCount => entries.Count;
        public StatusEntry GetEntry(int index) => entries[index];
        /// <summary>Server time, the clock of StatusEntry.Until (double.MaxValue = until cleared).</summary>
        public double CurrentTime => Now;

        public int GetStacks(StatusType type)
        {
            double now = Now;
            foreach (var entry in entries)
                if (entry.Type == type && entry.Until > now)
                    return entry.Stacks;
            return 0;
        }

        /// <summary>Server only. duration &lt;= 0 = until cleared. Re-applying refreshes the duration.</summary>
        public void Apply(StatusType type, float duration, int addStacks = 1, int maxStacks = 1)
        {
            if (!IsServer || !IsSpawned)
                return;
            double until = duration <= 0f ? double.MaxValue : Now + duration;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type != type)
                    continue;
                int stacks = entry.Until > Now ? entry.Stacks + addStacks : addStacks;
                entry.Stacks = (byte)Mathf.Clamp(stacks, 1, Mathf.Clamp(maxStacks, 1, 255));
                entry.Until = until;
                entries[i] = entry;
                return;
            }
            entries.Add(new StatusEntry { Type = type, Stacks = (byte)Mathf.Clamp(addStacks, 1, Mathf.Clamp(maxStacks, 1, 255)), Until = until });
        }

        /// <summary>Server only.</summary>
        public void Clear(StatusType type)
        {
            if (!IsServer)
                return;
            for (int i = entries.Count - 1; i >= 0; i--)
                if (entries[i].Type == type)
                    entries.RemoveAt(i);
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned)
                return;
            double now = Now;
            for (int i = entries.Count - 1; i >= 0; i--)
                if (entries[i].Until <= now)
                    entries.RemoveAt(i);
        }

        public float ModifyIncomingDamage(Health self, in DamageInfo info, float amount)
            => amount * (Has(StatusType.Curse) ? curseDamageTakenMultiplier : 1f) * (Has(StatusType.Marked) ? markedDamageTakenMultiplier : 1f);

        public bool TryPreventDeath(Health self, in DamageInfo info, out float healthFraction)
        {
            healthFraction = 0f;
            return false;
        }

        private void OnEntriesChanged(NetworkListEvent<StatusEntry> change) => Changed?.Invoke();
    }
}
