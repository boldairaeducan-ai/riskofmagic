using System.Collections.Generic;
using UnityEngine;

namespace RiskOfMagic.Combat
{
    /// <summary>
    /// Server-side damage over time. Ticks go through Health.ApplyDamage (credited to the source,
    /// shown as damage numbers, ProcCoefficient 0 so they don't trigger items).
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
        private static DotRunner runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            dots.Clear();
            runner = null;
        }

        /// <summary>Server only. Adds one stack of <paramref name="effect"/> based on the applying hit.</summary>
        public static void Apply(Health target, DotEffect effect, in DamageInfo hit)
        {
            if (target == null || effect == null || !target.IsAlive || !target.IsServer || hit.Amount <= 0f)
                return;
            EnsureRunner();

            var tick = hit;
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

            var dot = new Dot { Target = target, Effect = effect, Tick = tick, NextTick = now + effect.tickInterval, EndTime = now + effect.duration };
            if (stacks >= effect.maxStacks && oldest >= 0)
                dots[oldest] = dot;
            else
                dots.Add(dot);
            RefreshMask(target);
        }

        private static void RefreshMask(Health target)
        {
            if (target == null)
                return;
            int mask = 0;
            foreach (var dot in dots)
                if (dot.Target == target)
                    mask |= 1 << (int)dot.Tick.Type;
            target.SetDotMask(mask);
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
                int ticksLeft = Mathf.Max(0, Mathf.FloorToInt((dot.EndTime - Mathf.Max(now, dot.NextTick)) / dot.Effect.tickInterval) + 1);
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
            for (int i = dots.Count - 1; i >= 0; i--)
            {
                var dot = dots[i];
                if (dot.Target == null || !dot.Target.IsSpawned || !dot.Target.IsAlive)
                {
                    dots.RemoveAt(i);
                    continue;
                }

                while (dot.NextTick <= now && dot.NextTick <= dot.EndTime && dot.Target.IsAlive)
                {
                    dot.Target.ApplyDamage(dot.Tick);
                    dot.NextTick += dot.Effect.tickInterval;
                }

                // Ticks can kill and despawn targets, which may reorder nothing but shrink the list: re-check bounds.
                if (i >= dots.Count)
                    continue;
                if (now >= dot.EndTime || !dot.Target.IsAlive)
                {
                    dots.RemoveAt(i);
                    RefreshMask(dot.Target);
                }
                else
                {
                    dots[i] = dot;
                }
            }
        }

        private sealed class DotRunner : MonoBehaviour
        {
            private void Update() => Tick();
        }
    }
}
