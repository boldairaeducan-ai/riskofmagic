using System.Collections.Generic;
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
        RuneBlessing,
        /// <summary>Slowed by bolas (Bolas). 2 stacks = full slow, 1 stack = reduced slow (bosses, elites).</summary>
        Ensnared,
        /// <summary>Swarm of ravens (Envol de corbeaux): faster.</summary>
        RavenForm,
        /// <summary>Slowed by sticky resin (Résine collante): a fraction per stack.</summary>
        Resin,
        /// <summary>Paladin seals (1-3), detonated by Judgment / thrown shield (SealSystem).</summary>
        Sealed,
        /// <summary>Attack speed per stack after critical hits (Talisman de frénésie).</summary>
        Frenzy,
        /// <summary>More damage taken per stack (Brise-armure).</summary>
        Shattered,
        /// <summary>More damage dealt, hits heal (Tome sombre).</summary>
        DarkPact
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
        private float curseDamageDealtMultiplier => RiskOfMagic.Core.GameBalance.Instance.statuses.curseDamageDealtMultiplier;
        private float curseSpeedMultiplier => RiskOfMagic.Core.GameBalance.Instance.statuses.curseSpeedMultiplier;
        private float curseDamageTakenMultiplier => RiskOfMagic.Core.GameBalance.Instance.statuses.curseDamageTakenMultiplier;

        private float chillSpeedMultiplier => RiskOfMagic.Core.GameBalance.Instance.statuses.chillSpeedMultiplier;
        private float ensnareSpeedMultiplier => RiskOfMagic.Core.GameBalance.Instance.statuses.ensnareSpeedMultiplier;
        private float ensnareReducedSpeedMultiplier => RiskOfMagic.Core.GameBalance.Instance.statuses.ensnareReducedSpeedMultiplier;
        private float channelingSpeedMultiplier => RiskOfMagic.Core.GameBalance.Instance.statuses.channelingSpeedMultiplier;
        private float bannerDamageBonus => RiskOfMagic.Core.GameBalance.Instance.statuses.bannerDamageBonus;
        private float bannerDamageBonusPerExtraStack => RiskOfMagic.Core.GameBalance.Instance.statuses.bannerDamageBonusPerExtraStack;
        private float stealthSpeedBonus => RiskOfMagic.Core.GameBalance.Instance.statuses.stealthSpeedBonus;
        private float markedDamageTakenMultiplier => RiskOfMagic.Core.GameBalance.Instance.statuses.markedDamageTakenMultiplier;
        private float runeBlessingDamageBonus => RiskOfMagic.Core.GameBalance.Instance.statuses.runeBlessingDamageBonus;

        private float rallyBonus => RiskOfMagic.Core.GameBalance.Instance.statuses.rallyBonus;
        private float warhornBonusPerStack => RiskOfMagic.Core.GameBalance.Instance.statuses.warhornBonusPerStack;

        private NetworkList<StatusEntry> entries;

        /// <summary>Every peer.</summary>
        public event Action Changed;

        private double Now => IsSpawned ? NetworkManager.ServerTime.Time : 0d;

        /// <summary>Withered: 1% max health per stack (at least 5% left).</summary>
        public float MaxHealthMultiplier => Mathf.Max(0.05f, 1f - GetStacks(StatusType.Withered) / 100f);
        public float DamageDealtMultiplier => (Has(StatusType.Curse) ? curseDamageDealtMultiplier : 1f)
                                              * (Has(StatusType.Banner) ? 1f + bannerDamageBonus + bannerDamageBonusPerExtraStack * (GetStacks(StatusType.Banner) - 1) : 1f)
                                              * (1f + GetStacks(StatusType.SoulHarvest) / 100f)
                                              * (Has(StatusType.RuneBlessing) ? 1f + runeBlessingDamageBonus : 1f)
                                              * (Has(StatusType.DarkPact) ? 1f + RiskOfMagic.Core.GameBalance.Instance.statuses.darkPactDamageBonus : 1f);
        public float MoveSpeedMultiplier => Has(StatusType.Rooted) ? 0f
            : (Has(StatusType.Curse) ? curseSpeedMultiplier : 1f) * (Has(StatusType.Chilled) ? chillSpeedMultiplier : 1f)
              * (Has(StatusType.Channeling) ? channelingSpeedMultiplier : 1f) * (1f + SpeedBonus)
              * (Has(StatusType.Stealth) ? 1f + stealthSpeedBonus : 1f)
              * EnsnareMultiplier
              * Mathf.Max(0f, 1f - GetStacks(StatusType.Resin) * RiskOfMagic.Core.GameBalance.Instance.statuses.resinSlowPerStack)
              * (Has(StatusType.RavenForm) ? 1f + RiskOfMagic.Core.GameBalance.Instance.statuses.ravenFormSpeedBonus : 1f);

        private float EnsnareMultiplier
        {
            get
            {
                int stacks = GetStacks(StatusType.Ensnared);
                return stacks <= 0 ? 1f : stacks >= 2 ? ensnareSpeedMultiplier : ensnareReducedSpeedMultiplier;
            }
        }
        public float AttackSpeedMultiplier => (Has(StatusType.Curse) ? curseSpeedMultiplier : 1f) * (1f + SpeedBonus)
                                              * (1f + GetStacks(StatusType.Adrenaline) / 100f)
                                              * (1f + GetStacks(StatusType.Frenzy) * RiskOfMagic.Core.GameBalance.Instance.statuses.frenzyAttackSpeedPerStack);

        private float SpeedBonus => (Has(StatusType.Rally) ? rallyBonus : 0f) + GetStacks(StatusType.Warhorn) * warhornBonusPerStack;

        private readonly Dictionary<StatusType, GameObject> attachedEffects = new();

        private void Awake() => entries = new NetworkList<StatusEntry>();

        public override void OnNetworkSpawn()
        {
            entries.OnListChanged += OnEntriesChanged;
            RefreshAttachedEffects();
            if (IsServer && TryGetComponent(out RiskOfMagic.Player.PlayerAvatar _))
                Health.ServerDamaged += OnServerDamaged; // Tome sombre lifesteal (players only)
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
                Health.ServerDamaged -= OnServerDamaged;
            entries.OnListChanged -= OnEntriesChanged;
            foreach (var effect in attachedEffects.Values)
                if (effect != null)
                    Destroy(effect);
            attachedEffects.Clear();
        }

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
            => amount * (Has(StatusType.Curse) ? curseDamageTakenMultiplier : 1f) * (Has(StatusType.Marked) ? markedDamageTakenMultiplier : 1f)
               * (1f + GetStacks(StatusType.Shattered) * RiskOfMagic.Core.GameBalance.Instance.statuses.shatteredDamageTakenPerStack);

        public bool TryPreventDeath(Health self, in DamageInfo info, out float healthFraction)
        {
            healthFraction = 0f;
            return false;
        }

        /// <summary>Server: Tome sombre lifesteal on the damage this character deals.</summary>
        private void OnServerDamaged(Health victim, DamageInfo info)
        {
            if (!info.HasSource || info.SourceId != NetworkObjectId || victim.gameObject == gameObject || !Has(StatusType.DarkPact))
                return;
            if (TryGetComponent(out Health self) && self.IsAlive)
                self.Heal(info.Amount * RiskOfMagic.Core.GameBalance.Instance.statuses.darkPactLifesteal);
        }

        /// <summary>Server: the curse removes a share of an enemy's max health while it lasts.</summary>
        private void SyncCurseMaxHealth()
        {
            if (!IsServer || !TryGetComponent(out RiskOfMagic.Enemies.EnemyActor _) || !TryGetComponent(out Health health))
                return;
            bool cursed = Has(StatusType.Curse);
            if (cursed && health.MaxHealthDebuff <= 0f)
                health.ApplyMaxHealthDebuff(RiskOfMagic.Core.GameBalance.Instance.statuses.curseMaxHealthReduction);
            else if (!cursed && health.MaxHealthDebuff > 0f)
                health.ClearMaxHealthDebuff();
        }

        private void OnEntriesChanged(NetworkListEvent<StatusEntry> change)
        {
            SyncCurseMaxHealth();
            RefreshAttachedEffects();
            Changed?.Invoke();
        }

        /// <summary>
        /// Every peer: shows ArtLibrary.StatusArt.attachedEffect (e.g. bolas around the feet) while its status is active,
        /// sized to the character's footprint.
        /// </summary>
        private void RefreshAttachedEffects()
        {
            var library = RiskOfMagic.Core.ArtLibrary.Instance;
            if (library == null)
                return;
            foreach (var art in library.statuses)
            {
                if (art == null || art.attachedEffect == null)
                    continue;
                bool active = Has(art.status);
                attachedEffects.TryGetValue(art.status, out var current);
                if (active && current == null)
                {
                    current = Instantiate(art.attachedEffect, transform);
                    current.transform.localPosition = Vector3.zero;
                    current.transform.localRotation = Quaternion.identity;
                    current.transform.localScale = Vector3.one * Footprint() / Mathf.Max(0.0001f, transform.lossyScale.x);
                    attachedEffects[art.status] = current;
                }
                else if (!active && current != null)
                {
                    Destroy(current);
                    attachedEffects.Remove(art.status);
                }
            }
        }

        /// <summary>Horizontal size of the character (1 = human sized).</summary>
        private float Footprint()
        {
            var body = GetComponentInChildren<Collider>();
            if (body == null)
                return 1f;
            var extents = body.bounds.extents;
            return Mathf.Clamp(Mathf.Max(extents.x, extents.z) / 0.5f, 0.6f, 4f);
        }
    }
}
