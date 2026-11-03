using Unity.AI.Navigation;
using UnityEngine;

namespace RiskOfMagic.Maps
{
    /// <summary>
    /// Applies the stage's layout seed to every LayoutVariantGroup of its scene (deterministic hierarchy
    /// order, so every peer gets the same result) and rebakes the NavMesh on the server.
    /// The seed comes from StageManager. NavMeshSurfaces should collect Physics Colliders (runtime baking).
    /// </summary>
    public class MapLayout : MonoBehaviour
    {
        public static MapLayout Instance { get; private set; }

        public int AppliedSeed { get; private set; }

        private void OnEnable() => Instance = this;

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Apply(int seed, bool rebuildNavMesh)
        {
            AppliedSeed = seed;
            var random = new System.Random(seed);
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var group in root.GetComponentsInChildren<LayoutVariantGroup>(true))
                    group.Select(random);

            if (!rebuildNavMesh)
                return;
            Physics.SyncTransforms();
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var surface in root.GetComponentsInChildren<NavMeshSurface>())
                    surface.BuildNavMesh();
        }
    }
}
