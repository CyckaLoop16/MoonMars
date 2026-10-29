using UnityEngine;

namespace Waystation.View
{
    /// <summary>
    /// Fits installed art to its slot. Art made for an older scale, or exported in other units, is scaled
    /// uniformly so its footprint (or length, or height) matches what the game expects. Art that's already
    /// within 15% is left alone.
    /// </summary>
    public static class ArtFit
    {
        const float Tolerance = 0.15f;

        /// <summary>Scale so the horizontal footprint fits <paramref name="width"/> × <paramref name="depth"/> world units.</summary>
        public static void Footprint(GameObject go, float width, float depth)
        {
            if (!TryBounds(go, out var b) || b.size.x < 1e-4f || b.size.z < 1e-4f) return;
            Apply(go, Mathf.Min(width / b.size.x, depth / b.size.z));
        }

        /// <summary>Scale so the longest horizontal side is <paramref name="length"/> world units (ships).</summary>
        public static void Length(GameObject go, float length)
        {
            if (!TryBounds(go, out var b)) return;
            float size = Mathf.Max(b.size.x, b.size.z);
            if (size > 1e-4f) Apply(go, length / size);
        }

        /// <summary>Scale so the art is <paramref name="height"/> world units tall (people).</summary>
        public static void Height(GameObject go, float height)
        {
            if (TryBounds(go, out var b) && b.size.y > 1e-4f) Apply(go, height / b.size.y);
        }

        static void Apply(GameObject go, float factor)
        {
            if (Mathf.Abs(factor - 1f) > Tolerance) go.transform.localScale *= factor;
        }

        static bool TryBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (!any) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                any = true;
            }
            return any;
        }
    }
}
