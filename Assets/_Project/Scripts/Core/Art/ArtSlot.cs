using UnityEngine;

namespace RiskOfMagic.Core
{
    /// <summary>
    /// On generated VFX, projectile and interactable prefabs: when the instance is created, looks up its entry in
    /// Resources/ArtLibrary and, if a model is plugged in, shows it instead of the placeholder. Read at runtime:
    /// changing the ArtLibrary only needs a new Play, not a rebuild.
    /// </summary>
    public class ArtSlot : MonoBehaviour
    {
        [SerializeField, Tooltip("ArtLibrary entry name (the generated prefab name).")] private string key;
        [SerializeField] private ArtLibrary.Category category;

        public string Key => key;

        private void Awake()
        {
            var library = ArtLibrary.Instance;
            var entry = library != null ? library.Find(category, key) : null;
            if (entry == null || entry.model == null)
                return;

            if (!entry.keepPlaceholder)
                foreach (var placeholder in GetComponentsInChildren<Renderer>(true))
                    placeholder.enabled = false;

            var art = Instantiate(entry.model, transform, false);
            art.name = "Art";
            // The placeholder root is often scaled (a 0.15 sphere for an arrow): the model keeps its own size.
            Vector3 rootScale = transform.localScale;
            Vector3 inverse = new(1f / Mathf.Max(0.0001f, rootScale.x), 1f / Mathf.Max(0.0001f, rootScale.y), 1f / Mathf.Max(0.0001f, rootScale.z));
            float size = entry.scale > 0f ? entry.scale : 1f;
            art.transform.localRotation = Quaternion.Euler(entry.rotation);
            art.transform.localScale = inverse * size;
            art.transform.localPosition = Vector3.Scale(entry.offset, inverse);
            if (category != ArtLibrary.Category.Interactable)
                foreach (var collider in art.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false; // effects and projectiles must not block raycasts
            foreach (var behaviour in art.GetComponentsInChildren<Unity.Netcode.NetworkBehaviour>(true))
                behaviour.enabled = false; // a model is visual only
            if (category == ArtLibrary.Category.Vfx && TryGetComponent(out RiskOfMagic.Vfx.TimedEffect timed))
                FitParticlesToLifetime(art, timed);
        }

        /// <summary>
        /// Effect packs often loop their particles. A short-lived effect plays them once and lives until they fade;
        /// an effect that must persist longer than one cycle (telegraph, fire patch) keeps looping and is cut on release.
        /// </summary>
        private static void FitParticlesToLifetime(GameObject art, RiskOfMagic.Vfx.TimedEffect timed)
        {
            float end = 0f;
            foreach (var system in art.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                if (main.duration < timed.Lifetime)
                    continue;
                main.loop = false;
                end = Mathf.Max(end, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
            }
            timed.ExtendLifetime(Mathf.Min(end, 4f));
        }
    }
}
