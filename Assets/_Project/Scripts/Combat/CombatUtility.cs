using UnityEngine;

namespace RiskOfMagic.Combat
{
    public static class CombatUtility
    {
        /// <summary>Resolves a collider to its damage receiver (a Hitbox if present, otherwise the parent Health).</summary>
        public static bool TryGetDamageable(Collider collider, out IDamageable damageable)
        {
            damageable = collider != null ? collider.GetComponentInParent<IDamageable>() : null;
            return damageable != null;
        }
    }
}
