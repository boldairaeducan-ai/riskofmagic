using System.Collections.Generic;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Helpers for UI elements following world positions on a Screen Space Overlay canvas.</summary>
    public static class ScreenProjection
    {
        public static Camera GetGameplayCamera()
        {
            var local = PlayerAvatar.Local;
            if (local != null && local.Look.Camera != null && local.Look.Camera.enabled)
                return local.Look.Camera;
            return Camera.main;
        }

        /// <summary>False when behind the camera or beyond maxDistance.</summary>
        public static bool TryProject(Camera camera, RectTransform space, Vector3 world, float maxDistance, out Vector2 local)
        {
            local = default;
            if (camera == null)
                return false;
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f || screen.z > maxDistance)
                return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen, null, out local);
        }
    }

    /// <summary>Tiny pool of clones of an inactive template.</summary>
    public sealed class TemplatePool<T> where T : Component
    {
        private readonly T template;
        private readonly Transform parent;
        private readonly List<T> items = new();
        private int used;

        public TemplatePool(T template, Transform parent)
        {
            this.template = template;
            this.parent = parent;
            template.gameObject.SetActive(false);
        }

        public void Begin() => used = 0;

        public T Next()
        {
            if (used == items.Count)
                items.Add(Object.Instantiate(template, parent));
            var item = items[used++];
            if (!item.gameObject.activeSelf)
                item.gameObject.SetActive(true);
            return item;
        }

        public void End()
        {
            for (int i = used; i < items.Count; i++)
                if (items[i].gameObject.activeSelf)
                    items[i].gameObject.SetActive(false);
        }
    }
}
