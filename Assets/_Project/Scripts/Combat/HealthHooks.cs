namespace RiskOfMagic.Combat
{
    /// <summary>Server-side hooks on a Health, implemented by components on the same GameObject (status, items).</summary>
    public interface IHealthHooks
    {
        float ModifyIncomingDamage(Health self, in DamageInfo info, float amount);

        /// <summary>Return true to survive a lethal hit with <paramref name="healthFraction"/> of max health.</summary>
        bool TryPreventDeath(Health self, in DamageInfo info, out float healthFraction);
    }

    /// <summary>Server-side hook on the attacker's NetworkObject (items boosting outgoing damage).</summary>
    public interface IDamageDealerHooks
    {
        float ModifyOutgoingDamage(Health victim, in DamageInfo info, float amount);
    }
}
