using RiskOfMagic.Core;
using UnityEngine;

namespace RiskOfMagic.Combat
{
    /// <summary>
    /// Optional damage zone (head, weak point...) forwarding to the parent Health with a multiplier.
    /// Must live on a child object that owns its own collider.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Hitbox : MonoBehaviour, IDamageable
    {
        [SerializeField] private Health owner;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [SerializeField] private bool isWeakPoint;

        public Health Health => owner;
        public Team Team => owner.Team;
        public bool IsAlive => owner.IsAlive;

        private void Awake()
        {
            if (owner == null)
                owner = GetComponentInParent<Health>();
        }

        public void ApplyDamage(DamageInfo info)
        {
            info.Amount *= damageMultiplier;
            info.IsCritical |= isWeakPoint;
            owner.ApplyDamage(info);
        }
    }
}
