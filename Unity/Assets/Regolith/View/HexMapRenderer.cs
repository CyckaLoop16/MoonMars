using System.Collections.Generic;
using Regolith.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace Regolith.View
{
    /// <summary>
    /// Builds one mesh of terraced hex prisms for the whole map. Lighting is baked into
    /// vertex colours so the prototype needs no textures and works in any render pipeline.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class HexMapRenderer : MonoBehaviour
    {
        public const float HexSize = 1f;
        public const float LevelHeight = 0.3f;

        static readonly Vector3 LightDirection = new Vector3(-0.6f, 0f, 0.8f).normalized;
        static readonly Color IceColor = new Color(0.62f, 0.85f, 1f);
        static readonly Color MineralColor = new Color(0.9f, 0.72f, 0.29f);
        static readonly Color LavaTubeColor = new Color(0.12f, 0.08f, 0.08f);
        static readonly Color HighlightColor = new Color(1f, 0.95f, 0.85f);

        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<int> _triangles = new List<int>();
        readonly Dictionary<TerrainType, Color> _terrainColors = new Dictionary<TerrainType, Color>();

        WorldMap _map;
        Mesh _mesh;
        Material _material;
        LineRenderer _highlight;

        public MeshCollider Collider { get; private set; }

        // --- Coordinate helpers (sim plane Y maps to world -Z, so row 0 / north is at the top) ---

        public static Vector3 ToWorld(HexCoord c)
        {
            var (x, y) = HexLayout.ToPlane(c, HexSize);
            return new Vector3((float)x, 0f, -(float)y);
        }

        public static HexCoord FromWorld(Vector3 p) => HexLayout.FromPlane(p.x, -p.z, HexSize);

        static Vector3 Corner(Vector3 center, int i, float height, float size = HexSize)
        {
            var (x, y) = HexLayout.CornerOffset(i % 6, size);
            return new Vector3(center.x + (float)x, height, center.z - (float)y);
        }

        public static int HeightLevel(Tile t) => Mathf.Clamp(Mathf.RoundToInt(t.Elevation * 3f), -2, 3);
        public static float TopHeight(Tile t) => (HeightLevel(t) + 2) * LevelHeight + 0.2f;

        void Awake()
        {
            _mesh = new Mesh { name = "HexMap", indexFormat = IndexFormat.UInt32 };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            Collider = GetComponent<MeshCollider>();

            var shader = Shader.Find("Regolith/Unlit Vertex Color");
            if (shader == null)
            {
                Debug.LogError("Regolith: shader 'Regolith/Unlit Vertex Color' not found.");
                shader = Shader.Find("Sprites/Default");
            }
            _material = new Material(shader);
            GetComponent<MeshRenderer>().sharedMaterial = _material;

            _highlight = new GameObject("HoverHighlight").AddComponent<LineRenderer>();
            _highlight.transform.SetParent(transform, false);
            _highlight.sharedMaterial = _material;
            _highlight.loop = true;
            _highlight.positionCount = 6;
            _highlight.widthMultiplier = 0.07f;
            _highlight.startColor = _highlight.endColor = ToRenderSpace(HighlightColor);
            _highlight.shadowCastingMode = ShadowCastingMode.Off;
            _highlight.enabled = false;

            foreach (var def in TerrainCatalog.All)
                _terrainColors[def.Type] = ColorUtility.TryParseHtmlString(def.Color, out var c) ? c : Color.magenta;
        }

        public void Build(WorldMap map)
        {
            _map = map;
            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();

            foreach (var tile in map.Tiles)
                AddTile(tile);

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateBounds();

            Collider.sharedMesh = null; // force the collider to pick up the rebuilt mesh
            Collider.sharedMesh = _mesh;
            _highlight.enabled = false;
        }

        /// <summary>World-space XZ rectangle covering the map, for camera bounds.</summary>
        public Rect WorldBounds()
        {
            var b = _mesh.bounds;
            return Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
        }

        public void SetHovered(Tile tile)
        {
            if (tile == null)
            {
                _highlight.enabled = false;
                return;
            }

            var center = ToWorld(tile.Coord);
            float h = TopHeight(tile) + 0.03f;
            for (int i = 0; i < 6; i++)
                _highlight.SetPosition(i, Corner(center, i, h, HexSize * 0.95f));
            _highlight.enabled = true;
        }

        void AddTile(Tile tile)
        {
            var center = ToWorld(tile.Coord);
            float top = TopHeight(tile);
            var baseColor = _terrainColors[tile.Terrain];

            // Top face: brighter on higher terraces.
            var topColor = Shade(baseColor, 0.9f + 0.05f * HeightLevel(tile));
            AddHexTop(center, top, HexSize, topColor);

            // Side walls only where this tile stands above its neighbour (or at the map edge).
            for (int i = 0; i < 6; i++)
            {
                var a = Corner(center, i, top);
                var b = Corner(center, i + 1, top);
                var edgeMid = (a + b) * 0.5f;
                var outward = new Vector3(edgeMid.x - center.x, 0f, edgeMid.z - center.z);

                var neighborCenter = center + outward * 2f;
                float bottom = _map.TryGetTile(FromWorld(neighborCenter), out var neighbor) ? TopHeight(neighbor) : 0f;
                if (bottom >= top) continue;

                float light = 0.5f + 0.3f * Mathf.Max(0f, Vector3.Dot(outward.normalized, LightDirection));
                AddWall(a, b, bottom, Shade(baseColor, light));
            }

            // Resource pips, matching the yield bonuses in Tile.Yields.
            float pipY = top + 0.01f;
            if (tile.Terrain != TerrainType.PolarIce && tile.Ice >= 0.6f)
                AddHexTop(center + new Vector3(-0.38f, 0f, -0.3f), pipY, 0.17f, IceColor);
            if (tile.Minerals >= 0.7f)
                AddHexTop(center + new Vector3(0.38f, 0f, -0.3f), pipY, 0.17f, MineralColor);
            if (tile.Feature == TileFeature.LavaTube)
                AddHexTop(center + new Vector3(0f, 0f, 0.35f), pipY, 0.24f, LavaTubeColor);
        }

        void AddHexTop(Vector3 center, float height, float size, Color color)
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

        void AddWall(Vector3 topA, Vector3 topB, float bottom, Color color)
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

        static Color Shade(Color c, float factor) => new Color(c.r * factor, c.g * factor, c.b * factor, 1f);

        /// <summary>Vertex colours are authored in sRGB; convert when the project renders in linear space.</summary>
        static Color ToRenderSpace(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
    }
}
