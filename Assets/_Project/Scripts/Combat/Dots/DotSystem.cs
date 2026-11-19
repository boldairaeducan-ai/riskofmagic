using System.Collections.Generic;
using UnityEngine;

namespace RiskOfMagic.Combat
{
    /// <summary>
    /// Server-side damage over time. The stacks of one effect from one source on one target tick together: one
    /// damage instance per tick carrying the sum of the stacks (one damage number, e.g. 2 bleed stacks of 5 = 10).
    /// Ticks go through Health.ApplyDamage (credited to the source, ProcCoefficient 0 so they don't trigger items).
    /// </summary>
    public static class DotSystem
    {
        private struct Dot
        {
            public Health Target;
            public DotEffect Effect;
            public DamageInfo Tick;
            public float NextTick;
            public float EndTime;
        }

        private static readonly List<Dot> dots = new();
        // Shared tick clock of a group of stacks (target, effect, source).
        private static readonly Dictionary<(Health target, DotEffect effect, ulong source), float> groupNextTick = new();
        private static readonly List<(Health target, DotEffect effect, ulong source)> dueGroups = new();
        private static DotRunner runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            dots.Clear();
            groupNextTick.Clear();
            runner = null;
        }

        private static (Health, DotEffect, ulong) Key(in Dot dot) => (dot.Target, dot.Effect, dot.Tick.HasSource ? dot.Tick.SourceId : ulong.MaxValue);

        /// <summary>Server only. Adds one stack of <paramref name="effect"/> based on the applying hit.</summary>
        public static void Apply(Health target, DotEffect effect, in DamageInfo hit)
        {
            bool percent = effect != null && effect.maxHealthPerSecond > 0f;
            if (target == null || effect == null || !target.IsAlive || !target.IsServer || (hit.Amount <= 0f && !percent))
                return;
            EnsureRunner();

            int maxStacks = effect.maxStacks;
            var tick = hit;
            if (percent)
            {
                // Poison: % of the target's max health per second per stack, weaker on bosses / elites / players.
                float perSecond = effect.maxHealthPerSecond;
                if (target.TryGetComponent(out RiskOfMagic.Player.PlayerAvatar _))
                {
                    perSecond = effect.playerMaxHealthPerSecond;
                    maxStacks = effect.playerMaxStacks;
                }
                else if (target.TryGetComponent(out RiskOfMagic.Enemies.EnemyActor enemy) && (enemy.IsBoss || enemy.IsElite))
                    perSecond *= effect.bossAndEliteFactor;
                tick.Amount = Mathf.Max(0.01f, target.Max * perSecond * effect.tickInterval);
            }
            else
                tick.Amount = hit.Amount * effect.tickDamageFraction;
            tick.Type = effect.damageType;
            tick.IsCritical = false;
            tick.ProcCoefficient = 0f;
            tick.Point = Vector3.zero;

            float now = Time.time;
            int stacks = 0, oldest = -1;
            for (int i = 0; i < dots.Count; i++)
            {
                if (dots[i].Target != target || dots[i].Effect != effect)
                    continue;
                stacks++;
                if (oldest < 0 || dots[i].EndTime < dots[oldest].EndTime)
                    oldest = i;
            }

            if (effect.refreshAllStacks)
                for (int i = 0; i < dots.Count; i++)
                {
                    if (dots[i].Target != target || dots[i].Effect != effect)
                        continue;
                    var stack = dots[i];
                    stack.EndTime = now + effect.duration; // keep attacking to keep it going
                    dots[i] = stack;
                }

            var dot = new Dot { Target = target, Effect = effect, Tick = tick, NextTick = now + effect.tickInterval, EndTime = now + effect.duration };
            var key = Key(dot);
            if (!groupNextTick.ContainsKey(key))
                groupNextTick[key] = now + effect.tickInterval; // a new stack joins the group's clock
            if (stacks >= maxStacks && oldest >= 0)
            {
                if (!effect.refreshAllStacks)
                    dots[oldest] = dot; // at the cap: the oldest stack is replaced (refreshed)
            }
            else
                dots.Add(dot);
            RefreshMask(target);
        }

        private static void RefreshMask(Health target)
        {
            if (target == null)
                return;
            int mask = 0;
            var counts = new int[8];
            foreach (var dot in dots)
                if (dot.Target == target)
                {
                    mask |= 1 << (int)dot.Tick.Type;
                    int type = (int)dot.Tick.Type;
                    if (type < counts.Length)
                        counts[type]++;
                }
            target.SetDotMask(mask);
            ulong packed = 0;
            for (int i = 0; i < counts.Length; i++)
                packed |= (ulong)Mathf.Min(counts[i], 63) << (i * 6);
            target.SetDotStacks(packed);
        }

        public static int CountStacks(Health target, DamageType type)
        {
            int count = 0;
            foreach (var dot in dots)
                if (dot.Target == target && dot.Tick.Type == type)
                    count++;
            return count;
        }

        /// <summary>Server only. Removes every DoT of <paramref name="type"/> on the target and returns the damage they had left.</summary>
        public static float Consume(Health target, DamageType type)
        {
            float remaining = 0f;
            float now = Time.time;
            for (int i = dots.Count - 1; i >= 0; i--)
            {
                var dot = dots[i];
                if (dot.Target != target || dot.Tick.Type != type)
                    continue;
                float next = groupNextTick.TryGetValue(Key(dot), out float groupTick) ? groupTick : now;
                int ticksLeft = Mathf.Max(0, Mathf.FloorToInt((dot.EndTime - Mathf.Max(now, next)) / dot.Effect.tickInterval) + 1);
                remaining += ticksLeft * dot.Tick.Amount;
                dots.RemoveAt(i);
            }
            RefreshMask(target);
            return remaining;
        }

        private static void EnsureRunner()
        {
            if (runner != null)
                return;
            var go = new GameObject("DotSystem") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<DotRunner>();
        }

        private static void Tick()
        {
            float now = Time.time;

            // Groups due this frame: one damage instance = the sum of their live stacks.
            dueGroups.Clear();
            foreach (var pair in groupNextTick)
                if (pair.Value <= now)
                    dueGroups.Add(pair.Key);
            foreach (var key in dueGroups)
            {
                float tickTime = groupNextTick[key];
                while (tickTime <= now)
                {
                    float total = 0f;
                    DamageInfo info = default;
                    bool any = false;
                    foreach (var dot in dots)
                    {
                        if (dot.Target != key.target || dot.Effect != key.effect || Key(dot) != key || dot.EndTime < tickTime)
                            continue;
                        if (!any)
                            info = dot.Tick;
                        total += dot.Tick.Amount;
                        any = true;
                    }
                    if (!any || key.target == null || !key.target.IsSpawned || !key.target.IsAlive)
                        break;
                    info.Amount = total;
                    key.target.ApplyDamage(info);
                    tickTime += key.effect.tickInterval;
                }
                groupNextTick[key] = tickTime;
            }

            // Expired stacks and dead targets.
            for (int i = dots.Count - 1; i >= 0; i--)
            {
                if (i >= dots.Count)
                    continue; // a tick killed and despawned something
                var dot = dots[i];
                bool dead = dot.Target == null || !dot.Target.IsSpawned || !dot.Target.IsAlive;
                if (dead || now >= dot.EndTime)
                {
                    dots.RemoveAt(i);
                    if (!dead)
                        RefreshMask(dot.Target);
                }
            }

            // Groups without stacks.
            dueGroups.Clear();
            foreach (var key in groupNextTick.Keys)
            {
                bool alive = false;
                foreach (var dot in dots)
                    if (Key(dot) == key)
                    {
                        alive = true;
                        break;
                    }
                if (!alive)
                    dueGroups.Add(key);
            }
            foreach (var key in dueGroups)
                groupNextTick.Remove(key);
        }

        private sealed class DotRunner : MonoBehaviour
        {
            private void Update() => Tick();
        }
    }
}
