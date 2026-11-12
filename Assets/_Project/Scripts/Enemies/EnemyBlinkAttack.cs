using System.Collections;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;
using UnityEngine.AI;

namespace RiskOfMagic.Enemies
{
    /// <summary>Teleports to a NavMesh point around the target (repositioning between attacks).</summary>
    public class EnemyBlinkAttack : EnemyAttack
    {
        private BlinkAttackData D => data as BlinkAttackData ?? (BlinkAttackData)(data = ScriptableObject.CreateInstance<BlinkAttackData>());

        private Vector2 distanceFromTarget => D.distanceFromTarget;
        private float windup => D.windup;
        private float recovery => D.recovery;
        private GameObject blinkVfx => D.blinkVfx;

        protected override IEnumerator Perform(PlayerAvatar target)
        {
            VfxNetwork.Spawn(blinkVfx, transform.position, Quaternion.identity, 2f);
            yield return new WaitForSeconds(windup);
            if (!Actor.Health.IsAlive || target == null)
                yield break;
            var agent = GetComponent<NavMeshAgent>();
            for (int attempt = 0; attempt < 8 && agent != null; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(distanceFromTarget.x, distanceFromTarget.y);
                if (!NavMesh.SamplePosition(target.transform.position + new Vector3(offset.x, 0f, offset.y), out var hit, 3f, NavMesh.AllAreas))
                    continue;
                agent.Warp(hit.position);
                Vector3 look = target.transform.position - hit.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(look);
                VfxNetwork.Spawn(blinkVfx, hit.position, Quaternion.identity, 2f);
                break;
            }
            yield return new WaitForSeconds(recovery);
        }
    }
}
