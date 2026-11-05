using RiskOfMagic.Combat;
using UnityEngine;

namespace RiskOfMagic.Enemies.Elites
{
    /// <summary>
    /// Elite affix (RoR2-style): stat multipliers, tint/aura and server behaviour. Registered in the GameCatalog
    /// (replicated by index). Per-enemy state lives in EliteRuntime.
    /// </summary>
    public abstract class EliteAffix : ScriptableObject
    {
        [Header("Name (\"Goule ardente\", \"Ogre ardent\")")]
        public string nameMasculine = "élite";
        public string nameFeminine = "élite";
        public Color tint = Color.white;
        [Tooltip("Attached to the enemy on every peer (halo, blizzard area), scale kept in world units.")]
        public GameObject auraPrefab;

        [Header("Multipliers")]
        [Min(1f)] public float healthMultiplier = 4f;
        [Min(1f)] public float damageMultiplier = 2f;
        [Min(1f), Tooltip("Director cost multiplier of the spawn card.")] public float costMultiplier = 6f;
        [Min(1f)] public float goldMultiplier = 4f;

        [Header("Bosses")]
        [Min(1f)] public float bossHealthMultiplier = 1.5f;
        [Min(1f)] public float bossDamageMultiplier = 1.25f;
        [Min(1f)] public float bossGoldMultiplier = 1.5f;

        public string GetName(bool feminine) => feminine ? nameFeminine : nameMasculine;

        /// <summary>Server, every frame while alive.</summary>
        public virtual void ServerTick(EliteRuntime elite) { }

        /// <summary>Server: the elite hit something (ProcCoefficient &gt; 0 only).</summary>
        public virtual void OnServerDealtDamage(EliteRuntime elite, Health victim, in DamageInfo info) { }

        /// <summary>Server: the elite took damage.</summary>
        public virtual void OnServerDamaged(EliteRuntime elite, in DamageInfo info) { }
    }
}
