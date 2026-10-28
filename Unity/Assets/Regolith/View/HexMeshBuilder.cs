using System.Collections.Generic;
using Regolith.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace Regolith.View
{
    /// <summary>Accumulates vertex-coloured hex geometry and writes it to a Mesh.</summary>
    public sealed class HexMeshBuilder
    {
        static readonly Vector3 LightDirection = new Vector3(-0.6f, 0f, 0.8f).normalized;

        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<int> _triangles = new List<int>();

        public static Vector3 Corner(Vector3 center, int i, float height, float size)
        {
            var (x, y) = HexLayout.CornerOffset(i % 6, size);
            return new Vector3(center.x + (float)x, height, center.z - (float)y);
        }

        public static Color Shade(Color c, float factor) => new Color(c.r * factor, c.g * factor, c.b * factor, 1f);

        /// <summary>Side-wall brightness for the edge between corners i and i+1, from a fixed fake sun.</summary>
        public static float WallLight(int i)
        {
            var a = Corner(Vector3.zero, i, 0f, 1f);
            var b = Corner(Vector3.zero, i + 1, 0f, 1f);
            var outward = ((a + b) * 0.5f).normalized;
            return 0.5f + 0.3f * Mathf.Max(0f, Vector3.Dot(outward, LightDirection));
        }

        /// <summary>Vertex colours are authored in sRGB; convert when the project renders in linear space.</summary>
        public static Color ToRenderSpace(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;

        public void Clear()
        {
            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();
        }

        public void AddHexTop(Vector3 center, float height, float size, Color color)
        {
            int start = _vertices.Count;
            _vertices.Add(new Vector3(center.x, height, center.z));
            for (int i = 0; i < 6; i++)
                _vertices.Add(Corner(center, i, height, size));

            var c = ToRenderSpace(color);
            for (int i = 0; i < 7; i++) _colors.Add(c);

            // Corners run clockwise seen from above, which is Unity's front-face winding.
            for (int i = 0; i < 6; i++)
            {
                _triangles.Add(start);
                _triangles.Add(start + 1 + i);
                _triangles.Add(start + 1 + (i + 1) % 6);
            }
        }

        public void AddWall(Vector3 topA, Vector3 topB, float bottom, Color color)
        {
            int start = _vertices.Count;
            _vertices.Add(topA);
            _vertices.Add(topB);
            _vertices.Add(new Vector3(topB.x, bottom, topB.z));
            _vertices.Add(new Vector3(topA.x, bottom, topA.z));

            var c = ToRenderSpace(color);
            for (int i = 0; i < 4; i++) _colors.Add(c);

            _triangles.Add(start); _triangles.Add(start + 3); _triangles.Add(start + 2);
            _triangles.Add(start); _triangles.Add(start + 2); _triangles.Add(start + 1);
        }

        /// <summary>A closed hex column, used for placeholder buildings.</summary>
        public void AddPrism(Vector3 center, float bottom, float top, float size, Color color)
        {
            AddHexTop(center, top, size, color);
            for (int i = 0; i < 6; i++)
                AddWall(Corner(center, i, top, size), Corner(center, i + 1, top, size), bottom, Shade(color, WallLight(i)));
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

        public static Material CreateMaterial()
        {
            var shader = Shader.Find("Regolith/Unlit Vertex Color");
            if (shader == null)
            {
                Debug.LogError("Regolith: shader 'Regolith/Unlit Vertex Color' not found.");
                shader = Shader.Find("Sprites/Default");
            }
            return new Material(shader);
        }
    }
}
