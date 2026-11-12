using System;
using System.Collections;
using RiskOfMagic.Audio;
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
        protected float range => Base.range;
        protected float cooldown => Base.cooldown;
        protected float maxHeightDifference => Base.maxHeightDifference;
        protected float selectionWeight => Base.selectionWeight;
        private string animatorTrigger => Base.animatorTrigger;

        [SerializeField, Tooltip("Tuning of this attack (Configs/Enemies/Attacks, shared by any enemy using it).")]
        protected EnemyAttackData data;

        private float readyTime;
        private Coroutine routine;

        /// <summary>Raised on every peer when an attack starts (animation, telegraph VFX).</summary>
        public event Action AttackStarted;

        /// <summary>Tuning asset (null = the class defaults).</summary>
        public EnemyAttackData Data => data;
        private EnemyAttackData fallback;
        /// <summary>Base fields of the data asset, or defaults when none is assigned.</summary>
        private EnemyAttackData Base => data != null ? data : fallback ??= ScriptableObject.CreateInstance<EnemyAttackData>();
        public float Range => range;
        public float SelectionWeight => selectionWeight;
        public string AnimatorTrigger => animatorTrigger;
        /// <summary>Server. Phase tuning (ChaosAvatar): &lt; 1 = more frequent.</summary>
        public float CooldownMultiplier { get; set; } = 1f;
        /// <summary>Disabled attacks are never picked (phase-gated attacks).</summary>
        public bool IsAvailable => isActiveAndEnabled;
        /// <summary>Multiplier of the brain's turn speed while this attack runs (low = sweeping breath).</summary>
        public virtual float TrackingFactor => 1f;
        public bool IsAttacking { get; private set; }
        public bool IsReady => !IsAttacking && Time.time >= readyTime;
        protected EnemyActor Actor { get; private set; }

        protected virtual void Awake() => Actor = GetComponent<EnemyActor>();

        public virtual bool IsInRange(Transform target)
        {
            Vector3 offset = target.position - transform.position;
            if (Mathf.Abs(offset.y) > maxHeightDifference)
                return false;
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
            var balance = RiskOfMagic.Core.GameBalance.Instance.enemies;
            float globalRate = Actor.IsBoss ? balance.bossAttackRateMultiplier : balance.regularAttackRateMultiplier;
            readyTime = Time.time + cooldown * CooldownMultiplier / Mathf.Max(0.1f, Actor.AttackSpeedMultiplier * globalRate);
            routine = null;
        }

        [Rpc(SendTo.Everyone)]
        private void AttackStartedRpc()
        {
            if (Actor != null && Actor.Definition != null)
                AudioService.PlayAt(Actor.Definition.attackSound, transform.position);
            AttackStarted?.Invoke();
        }
    }
}
