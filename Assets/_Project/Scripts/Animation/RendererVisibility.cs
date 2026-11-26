using System.Collections.Generic;
using UnityEngine;

namespace RiskOfMagic.Animation
{
    /// <summary>Cosmetic updates skip characters no camera sees (Renderer.isVisible: any camera, shadows included).</summary>
    public static class RendererVisibility
    {
        public static bool AnyVisible(IReadOnlyList<Renderer> renderers)
        {
            if (renderers == null || renderers.Count == 0)
                return true; // nothing to judge by: keep updating
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                if (r != null && r.enabled && r.isVisible)
                    return true;
            }
            return false;
        }
    }
}
