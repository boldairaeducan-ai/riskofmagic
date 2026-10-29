using RiskOfMagic.Core;

namespace RiskOfMagic.Combat
{
    public interface IDamageable
    {
        Health Health { get; }
        Team Team { get; }
        bool IsAlive { get; }

        /// <summary>Server only.</summary>
        void ApplyDamage(DamageInfo info);
    }
}
