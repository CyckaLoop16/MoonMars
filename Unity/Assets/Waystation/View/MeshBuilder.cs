using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Waystation.View
{
    /// <summary>
    /// Builds flat-shaded, vertex-coloured low-poly meshes. Lighting from a fixed sun is baked into
    /// the colours, so placeholders need no textures or scene lights and match the GPT art style.
    /// </summary>
    public sealed class MeshBuilder
    {
        static readonly Vector3 Sun = new Vector3(0.35f, 0.85f, -0.4f).normalized;

        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<int> _triangles = new List<int>();

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        /// <summary>Vertex colours are authored in sRGB; convert when the project renders in linear space.</summary>
        public static Color ToRenderSpace(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;

        public static Color Lit(Color c, Vector3 normal)
        {
            float light = 0.62f + 0.38f * Mathf.Max(0f, Vector3.Dot(normal.normalized, Sun));
            return new Color(c.r * light, c.g * light, c.b * light, 1f);
        }

        public static Material CreateMaterial()
        {
            var shader = Shader.Find("Waystation/Unlit Vertex Color");
            if (shader == null)
            {
                Debug.LogError("Waystation: shader 'Waystation/Unlit Vertex Color' not found.");
                shader = Shader.Find("Sprites/Default");
            }
            return new Material(shader);
        }

        public void Clear()
        {
            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            ApplyTo(mesh);
            return mesh;
        }

        public void ApplyTo(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>Triangle facing <paramref name="outward"/>; the winding is fixed up automatically.</summary>
        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Color color, bool shade = true)
        {
            // Unity treats clockwise (as seen by the viewer) as the front; Cross(b-a, c-a) points at that viewer.
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0) (b, c) = (c, b);
            var col = ToRenderSpace(shade ? Lit(color, outward) : color);
            int start = _vertices.Count;
            _vertices.Add(a); _vertices.Add(b); _vertices.Add(c);
            _colors.Add(col); _colors.Add(col); _colors.Add(col);
            _triangles.Add(start); _triangles.Add(start + 1); _triangles.Add(start + 2);
        }

        /// <summary>Quad a-b-c-d (in order around its edge) facing <paramref name="outward"/>.</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color color, bool shade = true)
        {
            AddTriangle(a, b, c, outward, color, shade);
            AddTriangle(a, c, d, outward, color, shade);
        }

        /// <summary>Axis-aligned box (optionally turned about Y), without a bottom face.</summary>
        public void AddBox(Vector3 center, Vector3 size, Color color, float yawDegrees = 0f)
        {
            var rot = Quaternion.Euler(0f, yawDegrees, 0f);
            var h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + rot * new Vector3(x * h.x, y * h.y, z * h.z);

            AddQuad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), Vector3.up, color);
            AddQuad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), rot * Vector3.forward, color);
            AddQuad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), rot * Vector3.back, color);
            AddQuad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), rot * Vector3.right, color);
            AddQuad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), rot * Vector3.left, color);
        }

        /// <summary>Upright n-sided prism (tanks, pillars, tree caps).</summary>
        public void AddPrism(Vector3 baseCenter, float radius, float height, int sides, Color color, float yawDegrees = 0f)
        {
            var top = baseCenter + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = (yawDegrees + 360f * i / sides) * Mathf.Deg2Rad;
                float a1 = (yawDegrees + 360f * (i + 1) / sides) * Mathf.Deg2Rad;
                var o0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                var o1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                var mid = (o0 + o1).normalized;
                AddQuad(baseCenter + o0, baseCenter + o1, top + o1, top + o0, mid, color);
                AddTriangle(top, top + o0, top + o1, Vector3.up, color);
            }
        }

        /// <summary>Pointed n-sided spike (crystals, nose cones).</summary>
        public void AddSpike(Vector3 baseCenter, Vector3 tip, float radius, int sides, Color color, float yawDegrees = 0f)
        {
            var axis = (tip - baseCenter).normalized;
            var side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            var side2 = Vector3.Cross(axis, side);
            for (int i = 0; i < sides; i++)
            {
                float a0 = (yawDegrees + 360f * i / sides) * Mathf.Deg2Rad;
                float a1 = (yawDegrees + 360f * (i + 1) / sides) * Mathf.Deg2Rad;
                var p0 = baseCenter + (side * Mathf.Cos(a0) + side2 * Mathf.Sin(a0)) * radius;
                var p1 = baseCenter + (side * Mathf.Cos(a1) + side2 * Mathf.Sin(a1)) * radius;
                var outward = Vector3.Cross(p1 - p0, tip - p0);
                if (Vector3.Dot(outward, (p0 + p1) * 0.5f - baseCenter) < 0) outward = -outward;
                AddTriangle(p0, p1, tip, outward, color);
            }
        }
    }
}
