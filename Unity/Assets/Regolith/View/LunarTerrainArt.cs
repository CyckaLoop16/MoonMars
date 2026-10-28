using System.Collections.Generic;
using Regolith.Sim;
using UnityEngine;

namespace Regolith.View
{
    /// <summary>Cached lunar art, combined into the map rather than instantiated per hex.</summary>
    public sealed class LunarTerrainArt
    {
        public sealed class Surface
        {
            internal readonly List<Vector3> Vertices = new List<Vector3>();
            internal readonly List<Color> Colors = new List<Color>();
            internal readonly List<int> Triangles = new List<int>();

            public float HeightAt(float x, float z)
            {
                float height = float.NegativeInfinity;
                for (int i = 0; i < Triangles.Count; i += 3)
                {
                    var a = Vertices[Triangles[i]];
                    var b = Vertices[Triangles[i + 1]];
                    var c = Vertices[Triangles[i + 2]];
                    float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(d) < 1e-8f || Vector3.Cross(b - a, c - a).y <= 0f) continue;
                    float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
                    float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
                    float w = 1f - u - v;
                    if (u >= -.0001f && v >= -.0001f && w >= -.0001f)
                        height = Mathf.Max(height, u * a.y + v * b.y + w * c.y);
                }
                return float.IsNegativeInfinity(height) ? 0f : height;
            }

            // Keep a horizontal marker above every triangle overlapping its footprint.
            public float MarkerHeight(float x, float z, float radius)
            {
                float height = HeightAt(x, z);
                for (int i = 0; i < Triangles.Count; i += 3)
                {
                    var a = Vertices[Triangles[i]];
                    var b = Vertices[Triangles[i + 1]];
                    var c = Vertices[Triangles[i + 2]];
                    if (Mathf.Max(a.x, b.x, c.x) < x - radius || Mathf.Min(a.x, b.x, c.x) > x + radius ||
                        Mathf.Max(a.z, b.z, c.z) < z - radius || Mathf.Min(a.z, b.z, c.z) > z + radius) continue;
                    height = Mathf.Max(height, a.y, b.y, c.y);
                }
                return height;
            }

            public void AppendTo(HexMeshBuilder builder, Vector3 origin, Color? tint)
            {
                // Asset colours already match the project's render space.
                Color target = tint.HasValue ? HexMeshBuilder.ToRenderSpace(tint.Value) : Color.white;
                for (int i = 0; i < Triangles.Count; i += 3)
                {
                    int a = Triangles[i], b = Triangles[i + 1], c = Triangles[i + 2];
                    builder.AddRenderSpaceTriangle(origin + Vertices[a], origin + Vertices[b], origin + Vertices[c],
                        tint.HasValue ? Color.Lerp(Colors[a], target, .3f) : Colors[a],
                        tint.HasValue ? Color.Lerp(Colors[b], target, .3f) : Colors[b],
                        tint.HasValue ? Color.Lerp(Colors[c], target, .3f) : Colors[c]);
                }
            }
        }

        readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
        readonly Dictionary<string, Surface> _surfaces = new Dictionary<string, Surface>();

        static uint Hash(int seed, HexCoord coord)
        {
            unchecked
            {
                uint value = (uint)seed ^ (uint)coord.Q * 0x9e3779b9u ^ (uint)coord.R * 0x85ebca6bu;
                value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15;
                return value;
            }
        }

        public Surface GetSurface(Tile tile, int seed, bool foundation)
        {
            uint hash = Hash(seed, tile.Coord);
            string name;
            if (foundation) name = tile.Terrain == TerrainType.LunarMare ? "LunarMare" : "LunarHighlands";
            else if (tile.Feature == TileFeature.LavaTube) name = "LavaTubeEntrance";
            else if (tile.Terrain == TerrainType.ShadowedCrater) name = "ShadowedCrater";
            else if (tile.Feature == TileFeature.CraterRim || tile.Feature == TileFeature.PeakOfLight) name = "CraterRim";
            else if (tile.Terrain == TerrainType.LunarMare) name = "LunarMare";
            else name = hash % 9 == 0 ? "SmallImpactCrater" : "LunarHighlands";

            int rotation = (int)((hash >> 8) % 6);
            bool scatter = !foundation && (name == "LunarHighlands" || name == "LunarMare") && (hash >> 16) % 4 == 0;
            string key = name + "/" + rotation + "/" + foundation + "/" + scatter;
            if (_surfaces.TryGetValue(key, out var surface)) return surface;
            var mesh = LoadMesh(name);
            if (mesh == null) return null; // A missing art asset retains the original terrain fallback.

            surface = new Surface();
            var turn = Quaternion.Euler(0f, rotation * 60f, 0f);
            if (foundation)
            {
                // A level construction pad also removes loose rocks and fills bowls.
                var ground = HexMapRenderer.ParseColor(TerrainCatalog.Get(name == "LunarMare"
                    ? TerrainType.LunarMare : TerrainType.LunarHighlands).Color);
                for (int i = 0; i < 6; i++)
                {
                    int start = surface.Vertices.Count;
                    surface.Vertices.Add(Vector3.zero);
                    surface.Vertices.Add(HexMeshBuilder.Corner(Vector3.zero, i, 0f, 1f));
                    surface.Vertices.Add(HexMeshBuilder.Corner(Vector3.zero, i + 1, 0f, 1f));
                    for (int j = 0; j < 3; j++)
                    {
                        surface.Colors.Add(HexMeshBuilder.ToRenderSpace(HexMeshBuilder.Shade(ground, .95f)));
                        surface.Triangles.Add(start + j);
                    }
                }
            }
            else AddMesh(surface, mesh, turn, Vector3.zero, 1f, false, true);
            if (scatter)
            {
                var rocks = LoadMesh(name == "LunarMare" ? "BoulderClusterA" : "BoulderClusterB");
                var offset = turn * new Vector3(.48f, 0f, .13f);
                offset.y = surface.HeightAt(offset.x, offset.z);
                if (rocks != null) AddMesh(surface, rocks, turn, offset, .43f, false, false);
            }
            _surfaces[key] = surface;
            return surface;
        }

        Mesh LoadMesh(string name)
        {
            if (!_meshes.TryGetValue(name, out var mesh))
            {
                var prefab = Resources.Load<GameObject>("Terrain/" + name);
                var filter = prefab != null ? prefab.GetComponent<MeshFilter>() : null;
                mesh = filter != null ? filter.sharedMesh : null;
                _meshes[name] = mesh;
            }
            return mesh;
        }

        static bool Boundary(Vector3 p) => Mathf.Abs(p.x) >= .8659f ||
            Mathf.Abs(p.z) + Mathf.Abs(p.x) / Mathf.Sqrt(3f) >= .9999f;

        static void AddMesh(Surface surface, Mesh mesh, Quaternion rotation, Vector3 offset, float scale, bool flatten, bool tile)
        {
            var vertices = mesh.vertices;
            var colors = mesh.colors;
            var normals = mesh.normals;
            var triangles = mesh.triangles;
            int start = surface.Vertices.Count;
            var sun = new Vector3(-.45f, .82f, -.35f);
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = rotation * vertices[i] * scale + offset;
                if (flatten) p.y = 0f;
                surface.Vertices.Add(p);
                // Undo the source face light before rotating it under the same fixed sun.
                Color color = colors[i];
                if (QualitySettings.activeColorSpace == ColorSpace.Linear) color = color.gamma;
                float before = .66f + .34f * Mathf.Max(0f, Vector3.Dot(normals[i], sun));
                float after = .66f + .34f * Mathf.Max(0f, Vector3.Dot(rotation * normals[i], sun));
                color *= after / before;
                color.a = 1f;
                surface.Colors.Add(HexMeshBuilder.ToRenderSpace(color));
            }
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                var p = vertices[a]; var q = vertices[b]; var r = vertices[c];
                // The map supplies its own continuous terrace walls. Omit the art tile's
                // shallow skirt and bottom so neighbouring tiles never overlap or seal bowls.
                if (tile && (Mathf.Max(p.y, q.y, r.y) < -.1799f ||
                    (Mathf.Max(p.y, q.y, r.y) <= .0001f && Boundary(p) && Boundary(q) && Boundary(r)))) continue;
                if (flatten && Vector3.Cross(surface.Vertices[start + b] - surface.Vertices[start + a],
                    surface.Vertices[start + c] - surface.Vertices[start + a]).sqrMagnitude < 1e-14f) continue;
                surface.Triangles.Add(start + a); surface.Triangles.Add(start + b); surface.Triangles.Add(start + c);
            }
        }
    }
}
