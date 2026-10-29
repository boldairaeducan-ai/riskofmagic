using UnityEngine;

namespace RiskOfMagic.Combat.Projectiles
{
    /// <summary>Must be registered in the GameCatalog to be replicated.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Projectile", fileName = "Projectile")]
    public class ProjectileDefinition : ScriptableObject
    {
        [Header("Visuals")]
        public GameObject visualPrefab;
        public GameObject impactVfx;

        [Header("Motion")]
        [Min(0f)] public float speed = 60f;
        public float gravity;
        [Min(0.05f)] public float lifetime = 3f;
        [Min(0f)] public float radius = 0.15f;

        [Header("Hits")]
        [Min(0)] public int pierceCount;
        [Min(0f)] public float explosionRadius;
        public bool explodeOnExpire;
        public LayerMask hitMask = Physics.DefaultRaycastLayers;
    }
}
