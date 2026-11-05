using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using RiskOfMagic.Run;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Player
{
    /// <summary>
    /// Out-of-map fall (below StageBounds.KillHeight): the owner (movement authority) teleports back to a safe ground
    /// position from a moment ago, then the server applies a non-lethal penalty (% max health, ignores armor).
    /// </summary>
    [RequireComponent(typeof(PlayerAvatar))]
    public class FallRecovery : NetworkBehaviour
    {
        [SerializeField, Range(0f, 1f), Tooltip("Max health lost per fall (never kills: at least 1 HP left).")] private float damageFraction = 0.2f;
        [SerializeField, Min(0.05f)] private float sampleInterval = 0.25f;
        [SerializeField, Min(0f), Tooltip("Go back to a safe position at least this old (away from the edge you fell from).")]
        private float rewindSeconds = 1f;
        [SerializeField, Min(0.5f)] private float historySeconds = 4f;
        [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;
        [SerializeField, Range(0f, 1f)] private float cameraTrauma = 0.5f;

        private readonly List<(Vector3 position, float time)> safePositions = new();
        private static readonly RaycastHit[] hits = new RaycastHit[8];
        private PlayerAvatar avatar;
        private float nextSample;

        private void Awake() => avatar = GetComponent<PlayerAvatar>();

        private void Update()
        {
            if (!IsSpawned || !IsOwner || StageBounds.Instance == null)
                return;

            if (StageBounds.IsBelowMap(transform.position))
            {
                Recover();
                return;
            }
            if (Time.time >= nextSample)
            {
                nextSample = Time.time + sampleInterval;
                RecordSafePosition();
            }
        }

        private void RecordSafePosition()
        {
            Vector3 position = transform.position;
            // A teleport (new stage, respawn) invalidates the history.
            if (safePositions.Count > 0 && (safePositions[^1].position - position).sqrMagnitude > 30f * 30f)
                safePositions.Clear();
            safePositions.RemoveAll(p => Time.time - p.time > historySeconds);
            if (!avatar.IsStanding || avatar.Motor == null || !avatar.Motor.IsGrounded || !HasGroundBelow(position))
                return;
            safePositions.Add((position, Time.time));
        }

        private bool HasGroundBelow(Vector3 position)
        {
            int count = Physics.RaycastNonAlloc(position + Vector3.up * 0.3f, Vector3.down, hits, 1.5f, groundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!hits[i].collider.transform.IsChildOf(transform))
                    return true;
            return false;
        }

        private void Recover()
        {
            Vector3 target;
            int index = -1;
            for (int i = safePositions.Count - 1; i >= 0; i--)
            {
                index = i;
                if (Time.time - safePositions[i].time >= rewindSeconds)
                    break;
            }
            if (index >= 0)
                target = safePositions[index].position;
            else if (PlayerSpawnPoint.TryGet((int)OwnerClientId, out var pose))
                target = pose.position;
            else
                target = StageBounds.Instance.transform.position + Vector3.up * 2f;

            safePositions.Clear();
            avatar.Motor.Teleport(target + Vector3.up * 0.2f, transform.rotation);
            if (avatar.Look.Effects != null)
                avatar.Look.Effects.AddTrauma(cameraTrauma);
            FellRpc();
        }

        [Rpc(SendTo.Server)]
        private void FellRpc()
        {
            var health = avatar.Health;
            if (!health.IsAlive)
                return;
            float amount = Mathf.Min(health.Max * damageFraction, health.Current - 1f);
            if (amount <= 0f)
                return;
            var info = DamageInfo.Create(amount, DamageType.Physical, null, Team.Neutral, transform.position, Vector3.down);
            info.IgnoreModifiers = true;
            info.ProcCoefficient = 0f;
            health.ApplyDamage(info);
        }
    }
}
