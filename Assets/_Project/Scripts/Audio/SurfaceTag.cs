using UnityEngine;

namespace RiskOfMagic.Audio
{
    /// <summary>
    /// Level design: forces the footstep surface of this object (and its children's colliders). Put it on a water
    /// trigger volume, a wooden bridge, a rock mesh... Takes priority over SurfaceLibrary.
    /// </summary>
    public class SurfaceTag : MonoBehaviour
    {
        public SurfaceType surface = SurfaceType.Rock;
    }
}
