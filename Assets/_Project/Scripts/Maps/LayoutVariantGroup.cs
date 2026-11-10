using UnityEngine;

namespace RiskOfMagic.Maps
{
    /// <summary>
    /// Level design: each direct child is one alternative (bridge A / bridge B, open / closed passage...).
    /// MapLayout activates exactly one of them per stage (or none if allowed), the same on every peer.
    /// Groups can be nested inside variants.
    /// </summary>
    public class LayoutVariantGroup : MonoBehaviour
    {
        [SerializeField, Tooltip("Adds a 'nothing' option.")] private bool allowNone;

        public bool AllowNone => allowNone;

        public void Select(System.Random random)
        {
            int count = transform.childCount;
            if (count == 0)
                return;
            int pick = random.Next(count + (allowNone ? 1 : 0));
            for (int i = 0; i < count; i++)
                transform.GetChild(i).gameObject.SetActive(i == pick);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                foreach (var renderer in child.GetComponentsInChildren<Renderer>(true))
                    Gizmos.DrawWireCube(renderer.bounds.center, renderer.bounds.size);
            }
        }
    }
}
