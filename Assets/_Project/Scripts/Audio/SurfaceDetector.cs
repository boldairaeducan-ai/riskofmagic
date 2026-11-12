using UnityEngine;

namespace RiskOfMagic.Audio
{
    /// <summary>
    /// What a character stands on: SurfaceTag (water volumes included) → terrain layer under the feet (dominant splat) →
    /// mesh material → defaults. Cheap enough to run on every footstep.
    /// </summary>
    public static class SurfaceDetector
    {
        private static readonly RaycastHit[] hits = new RaycastHit[8];
        private static readonly Collider[] overlaps = new Collider[8];

        public static SurfaceType Detect(Vector3 feet, float probe = 0.6f)
        {
            // Water and other tagged volumes: triggers around the feet.
            int count = Physics.OverlapSphereNonAlloc(feet + Vector3.up * 0.3f, 0.35f, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var tag = overlaps[i].GetComponentInParent<SurfaceTag>();
                if (tag != null && overlaps[i].isTrigger)
                    return tag.surface;
            }

            count = Physics.RaycastNonAlloc(feet + Vector3.up * 0.3f, Vector3.down, hits, 0.3f + probe, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            int best = -1;
            for (int i = 0; i < count; i++)
                if (best < 0 || hits[i].distance < hits[best].distance)
                    if (hits[i].collider.GetComponentInParent<RiskOfMagic.Player.PlayerAvatar>() == null) // not our own body
                        best = i;
            if (best < 0)
                return SurfaceType.Default;
            var hit = hits[best];

            var surfaceTag = hit.collider.GetComponentInParent<SurfaceTag>();
            if (surfaceTag != null)
                return surfaceTag.surface;

            var library = SurfaceLibrary.Instance;
            if (hit.collider is TerrainCollider terrainCollider && terrainCollider.TryGetComponent(out Terrain terrain))
                return FromTerrain(terrain, hit.point, library);

            if (library != null && hit.collider.TryGetComponent(out Renderer renderer) && library.TryGet(renderer.sharedMaterial, out var surface))
                return surface;
            return library != null ? library.meshDefault : SurfaceType.Default;
        }

        /// <summary>Dominant painted layer at the point.</summary>
        private static SurfaceType FromTerrain(Terrain terrain, Vector3 point, SurfaceLibrary library)
        {
            var data = terrain.terrainData;
            if (data == null || library == null || data.terrainLayers.Length == 0)
                return library != null ? library.terrainDefault : SurfaceType.Dirt;
            Vector3 local = point - terrain.transform.position;
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);
            float[,,] weights = data.GetAlphamaps(x, z, 1, 1);
            int dominant = 0;
            for (int i = 1; i < weights.GetLength(2); i++)
                if (weights[0, 0, i] > weights[0, 0, dominant])
                    dominant = i;
            var layer = dominant < data.terrainLayers.Length ? data.terrainLayers[dominant] : null;
            return library.TryGet(layer, out var surface) ? surface : library.terrainDefault;
        }
    }
}
