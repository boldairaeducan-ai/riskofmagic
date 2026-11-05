using System.Collections;
using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Enemies.Elites
{
    /// <summary>Foudroyant: every few seconds, a telegraphed lightning strike falls on its target.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Elites/Storm", fileName = "Elite_Storm")]
    public class StormAffix : EliteAffix
    {
        [Min(0.5f)] public float interval = 4f;
        [Min(0f)] public float strikeDelay = 1f;
        [Min(0.5f)] public float strikeRadius = 2.5f;
        public float strikeDamage = 12f;
        [Min(1f)] public float maxRange = 40f;
        public LayerMask groundMask = Physics.DefaultRaycastLayers;
        public GameObject telegraphVfx;
        public GameObject boltVfx;
        public GameObject impactVfx;

        public override void ServerTick(EliteRuntime elite)
        {
            if (Time.time < elite.Timer)
                return;
            elite.Timer = Time.time + interval;
            var target = elite.Brain != null ? elite.Brain.Target : null;
            if (target == null || (target.transform.position - elite.transform.position).sqrMagnitude > maxRange * maxRange)
                return;

            Vector3 point = target.transform.position;
            if (Physics.Raycast(point + Vector3.up, Vector3.down, out var hit, 20f, groundMask, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(target.transform))
                point = hit.point;
            VfxNetwork.Spawn(telegraphVfx, point, Quaternion.identity, strikeRadius);
            elite.StartCoroutine(Strike(elite, point));
        }

        private IEnumerator Strike(EliteRuntime elite, Vector3 point)
        {
            yield return new WaitForSeconds(strikeDelay);
            if (elite == null || !elite.Actor.Health.IsAlive)
                yield break;
            VfxNetwork.SpawnLine(boltVfx, new[] { point + Vector3.up * 25f, point });
            VfxNetwork.Spawn(impactVfx, point, Quaternion.identity, strikeRadius);
            var info = DamageInfo.Create(strikeDamage * elite.Actor.DamageMultiplier, DamageType.Lightning, elite.Actor.NetworkObject,
                elite.Actor.Health.Team, point, Vector3.down);
            info.ProcCoefficient = 0f;
            AreaDamage.Apply(point + Vector3.up * 0.5f, strikeRadius, info);
        }
    }
}
