using RiskOfMagic.Vfx;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    /// <summary>Below a health threshold, every attack of the boss comes faster (once per life), with a red flash.</summary>
    [RequireComponent(typeof(EnemyActor))]
    public class EnemyEnrage : NetworkBehaviour
    {
        [SerializeField, Range(0.05f, 0.95f)] private float healthThreshold = 0.3f;
        [SerializeField, Min(1f), Tooltip("Attack rate multiplier once enraged.")] private float attackRate = 1.5f;
        [SerializeField] private Color flashColor = new(1f, 0.25f, 0.1f);
        [SerializeField] private GameObject enrageVfx;

        private EnemyActor actor;
        private bool enraged;

        private void Awake() => actor = GetComponent<EnemyActor>();

        private void Update()
        {
            if (!IsServer || !IsSpawned || enraged || !actor.Health.IsAlive || actor.Health.Normalized > healthThreshold)
                return;
            enraged = true;
            foreach (var attack in GetComponents<EnemyAttack>())
                attack.CooldownMultiplier /= attackRate;
            VfxNetwork.Spawn(enrageVfx, transform.position, Quaternion.identity, 4f);
            EnragedRpc();
        }

        [Rpc(SendTo.Everyone)]
        private void EnragedRpc() => actor.Flash(flashColor, 0.6f);
    }
}
