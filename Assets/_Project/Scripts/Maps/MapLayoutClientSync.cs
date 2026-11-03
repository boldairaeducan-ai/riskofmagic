using RiskOfMagic.Run;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Maps
{
    /// <summary>
    /// Client side: a map scene loaded after the seed was already received gets its variants applied here.
    /// (Seed changes while the scene is loaded are handled by StageManager.)
    /// </summary>
    [RequireComponent(typeof(MapLayout))]
    public class MapLayoutClientSync : MonoBehaviour
    {
        private MapLayout layout;

        private void Awake() => layout = GetComponent<MapLayout>();

        private void Update()
        {
            var manager = NetworkManager.Singleton;
            var stages = StageManager.Instance;
            if (manager == null || manager.IsServer || stages == null || stages.LayoutSeed == 0)
                return;
            if (layout.AppliedSeed != stages.LayoutSeed)
                layout.Apply(stages.LayoutSeed, rebuildNavMesh: false);
        }
    }
}
