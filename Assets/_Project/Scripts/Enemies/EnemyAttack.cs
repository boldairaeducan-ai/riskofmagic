using System;
using System.Collections;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    /// <summary>
    /// Base class for enemy attacks driven by EnemyBrain (server). Derive for melee, ranged, AoE...
    /// </summary>
    [RequireComponent(typeof(EnemyActor))]
    public abstract class EnemyAttack : NetworkBehaviour
    {
        [SerializeField, Min(0f)] protected float range = 2f;
        [SerializeField, Min(0f)] protected float cooldown = 1.5f;

        private float readyTime;
        private Coroutine routine;

        /// <summary>Raised on every peer when an attack starts (animation, telegraph VFX).</summary>
        public event Action AttackStarted;

        public float Range => range;
        public bool IsAttacking { get; private set; }
        public bool IsReady => !IsAttacking && Time.time >= readyTime;
        protected EnemyActor Actor { get; private set; }

        protected virtual void Awake() => Actor = GetComponent<EnemyActor>();

        public virtual bool IsInRange(Transform target)
        {
            Vector3 offset = target.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= range * range;
        }

        public bool TryBegin(PlayerAvatar target)
        {
            if (!IsServer || !IsReady || target == null || !IsInRange(target.transform))
                return false;
            IsAttacking = true;
            AttackStartedRpc();
            routine = StartCoroutine(Run(target));
            return true;
        }

        public void Cancel()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
            IsAttacking = false;
        }

        /// <summary>Server-side attack sequence (windup, hit, recovery).</summary>
        protected abstract IEnumerator Perform(PlayerAvatar target);

        private IEnumerator Run(PlayerAvatar target)
        {
            yield return Perform(target);
            IsAttacking = false;
            readyTime = Time.time + cooldown;
            routine = null;
        }

        [Rpc(SendTo.Everyone)]
        private void AttackStartedRpc() => AttackStarted?.Invoke();
    }
}
