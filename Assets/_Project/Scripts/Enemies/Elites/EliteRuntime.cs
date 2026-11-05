using System;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using UnityEngine;
using UnityEngine.AI;

namespace RiskOfMagic.Enemies.Elites
{
    /// <summary>
    /// Added at runtime to an elite on every peer: aura visual everywhere, affix behaviour on the server.
    /// Holds the per-enemy state the (shared) affix assets need.
    /// </summary>
    public class EliteRuntime : MonoBehaviour
    {
        public EnemyActor Actor { get; private set; }
        public EliteAffix Affix { get; private set; }
        public EnemyBrain Brain { get; private set; }
        public NavMeshAgent Agent { get; private set; }

        // Generic state used by affixes.
        public float Timer;
        public float Cooldown;
        public Vector3 LastDrop;
        /// <summary>xyz = position, w = expiry (Time.time).</summary>
        public readonly List<Vector4> Points = new();

        private GameObject aura;
        private bool subscribed;

        public static EliteRuntime Attach(EnemyActor actor, EliteAffix affix)
        {
            var runtime = actor.gameObject.AddComponent<EliteRuntime>();
            runtime.Actor = actor;
            runtime.Affix = affix;
            runtime.Brain = actor.GetComponent<EnemyBrain>();
            runtime.Agent = actor.GetComponent<NavMeshAgent>();
            runtime.LastDrop = actor.transform.position;
            runtime.Timer = Time.time + 1f;
            runtime.CreateAura();
            if (actor.IsServer)
            {
                Health.ServerDamaged += runtime.OnServerDamaged;
                runtime.subscribed = true;
            }
            return runtime;
        }

        private void CreateAura()
        {
            if (Affix.auraPrefab == null)
                return;
            aura = Instantiate(Affix.auraPrefab, transform);
            aura.transform.localPosition = Vector3.zero;
            aura.transform.localRotation = Quaternion.identity;
            float parentScale = Mathf.Max(0.01f, transform.lossyScale.x);
            aura.transform.localScale = Affix.auraPrefab.transform.localScale / parentScale;
        }

        private void OnDestroy()
        {
            if (subscribed)
                Health.ServerDamaged -= OnServerDamaged;
        }

        private void Update()
        {
            bool alive = Actor != null && Actor.IsSpawned && Actor.Health.IsAlive;
            if (aura != null && aura.activeSelf != alive)
                aura.SetActive(alive);
            if (alive && Actor.IsServer)
                Affix.ServerTick(this);
        }

        private void OnServerDamaged(Health victim, DamageInfo info)
        {
            if (Actor == null || !Actor.Health.IsAlive)
                return;
            if (victim == Actor.Health)
                Affix.OnServerDamaged(this, info);
            else if (info.HasSource && info.SourceId == Actor.NetworkObjectId && info.ProcCoefficient > 0f)
                Affix.OnServerDealtDamage(this, victim, info);
        }

        /// <summary>Server. Damages each standing player within radius (horizontal) once. ProcCoefficient 0.</summary>
        public void DamagePlayersAround(Vector3 center, float radius, float damage, DamageType type, Action<PlayerAvatar> onHit = null)
        {
            float sqr = radius * radius;
            foreach (var player in PlayerAvatar.All)
            {
                if (player == null || !player.IsStanding)
                    continue;
                Vector3 offset = player.transform.position - center;
                if (Mathf.Abs(offset.y) > 3f)
                    continue;
                offset.y = 0f;
                if (offset.sqrMagnitude > sqr)
                    continue;
                HitPlayer(player, damage, type, offset);
                onHit?.Invoke(player);
            }
        }

        public void HitPlayer(PlayerAvatar player, float damage, DamageType type, Vector3 direction)
        {
            var info = DamageInfo.Create(damage * Actor.DamageMultiplier, type, Actor.NetworkObject, Actor.Health.Team,
                player.transform.position + Vector3.up, direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.up);
            info.ProcCoefficient = 0f;
            player.Health.ApplyDamage(info);
        }
    }
}
