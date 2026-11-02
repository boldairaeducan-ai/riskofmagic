using RiskOfMagic.Combat;
using UnityEngine;

namespace RiskOfMagic.Items
{
    public readonly struct ProcEvent
    {
        public ProcEvent(Health victim, DamageInfo damage, Vector3 position)
        {
            Victim = victim;
            Damage = damage;
            Position = position;
        }

        /// <summary>May be destroyed by the time the proc runs: null-check it.</summary>
        public Health Victim { get; }
        public DamageInfo Damage { get; }
        /// <summary>Hit point (or victim center for kills), captured when the event happened.</summary>
        public Vector3 Position { get; }
    }
}
