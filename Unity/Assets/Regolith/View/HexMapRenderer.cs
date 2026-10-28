using System.Collections.Generic;
using Regolith.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace Regolith.View
{
    /// <summary>
    /// Renders the map as terraced hex prisms, split into chunks so large maps only rebuild what changed.
    /// Lunar surface art and picking use the same chunk meshes, with baked vertex lighting.
    /// </summary>
    public sealed class HexMapRenderer : MonoBehaviour
    {
        public const float HexSize = 1f;
        public const float LevelHeight = 0.22f;
        const float TerritoryTint = 0.3f;
        const int ChunkSize = 16;

        sealed class Chunk
        {
            public GameObject GameObject;
            public Mesh Mesh;
            public MeshCollider Collider;
            public readonly List<Tile> Tiles = new List<Tile>();
        }

        static readonly Color IceColor = new Color(0.62f, 0.85f, 1f);
        static readonly Color MineralColor = new Color(0.9f, 0.72f, 0.29f);
        static readonly Color LavaTubeColor = new Color(0.12f, 0.08f, 0.08f);
        static readonly Color PeakColor = new Color(1f, 0.93f, 0.55f);
        static readonly Color HoverColor = new Color(1f, 0.95f, 0.85f);
        static readonly Color SelectColor = new Color(1f, 0.8f, 0.2f);

        readonly HexMeshBuilder _builder = new HexMeshBuilder();
        readonly Dictionary<TerrainType, Color> _terrainColors = new Dictionary<TerrainType, Color>();
        readonly LunarTerrainArt _lunarArt = new LunarTerrainArt();
        readonly Dictionary<HexCoord, LunarTerrainArt.Surface> _surfaces = new Dictionary<HexCoord, LunarTerrainArt.Surface>();
        readonly HashSet<HexCoord> _foundations = new HashSet<HexCoord>();

        readonly Dictionary<(int, int), Chunk> _chunks = new Dictionary<(int, int), Chunk>();
        readonly Dictionary<HexCoord, Color> _territory = new Dictionary<HexCoord, Color>();

        WorldMap _map;
        Material _material;
        LineRenderer _hoverOutline;
        LineRenderer _selectOutline;
        Rect _bounds;

        // --- Coordinate helpers (sim plane Y maps to world -Z, so row 0 is at the top) ---

        public static Vector3 ToWorld(HexCoord c)
        {
            var (x, y) = HexLayout.ToPlane(c, HexSize);
            return new Vector3((float)x, 0f, -(float)y);
        }

        public static HexCoord FromWorld(Vector3 p) => HexLayout.FromPlane(p.x, -p.z, HexSize);

        public static int HeightLevel(Tile t) => Mathf.Clamp(Mathf.RoundToInt(t.Elevation * 3f), -4, 5);
        public static float TopHeight(Tile t) => (HeightLevel(t) + 4) * LevelHeight + 0.2f;

        public static Color ParseColor(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        void Awake()
        {
            _material = HexMeshBuilder.CreateMaterial();

            _hoverOutline = CreateOutline("HoverOutline", HoverColor, 0.06f);
            _selectOutline = CreateOutline("SelectOutline", SelectColor, 0.1f);

            foreach (var def in TerrainCatalog.All)
                _terrainColors[def.Type] = ParseColor(def.Color);
        }

        LineRenderer CreateOutline(string name, Color color, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = _material;
            line.loop = true;
            line.positionCount = 48;
            line.widthMultiplier = width;
            line.startColor = line.endColor = HexMeshBuilder.ToRenderSpace(color);
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.enabled = false;
            return line;
        }

        /// <summary>Builds every chunk for a new map.</summary>
        public void Build(WorldMap map, IReadOnlyDictionary<HexCoord, Color> territory = null,
            IEnumerable<HexCoord> occupied = null)
        {
            foreach (var chunk in _chunks.Values)
            {
                DestroyOwned(chunk.GameObject);
                DestroyOwned(chunk.Mesh);
            }
            _chunks.Clear();
            _territory.Clear();
            _map = map;
            _builder.Clear();
            _surfaces.Clear();
            _foundations.Clear();
            if (occupied != null) _foundations.UnionWith(occupied);
            if (territory != null)
                foreach (var kv in territory) _territory[kv.Key] = kv.Value;

            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var tile in map.Tiles)
            {
                var key = ChunkKey(tile.Coord);
                if (!_chunks.TryGetValue(key, out var chunk))
                {
                    chunk = CreateChunk(key);
                    _chunks[key] = chunk;
                }
                chunk.Tiles.Add(tile);

                var p = ToWorld(tile.Coord);
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
            }
            _bounds = Rect.MinMaxRect(minX - HexSize, minZ - HexSize, maxX + HexSize, maxZ + HexSize);

            foreach (var chunk in _chunks.Values) RebuildChunk(chunk);
            Physics.SyncTransforms();
            SetHovered(null);
            SetSelected(null);
        }

        /// <summary>Tints claimed hexes with their owner's colour, rebuilding only the chunks that changed.</summary>
        public void SetTerritory(IReadOnlyDictionary<HexCoord, Color> territory, IEnumerable<HexCoord> occupied = null)
        {
            var dirty = new HashSet<(int, int)>();
            if (occupied != null)
            {
                var next = new HashSet<HexCoord>(occupied);
                foreach (var coord in next)
                    if (!_foundations.Contains(coord)) dirty.Add(ChunkKey(coord));
                foreach (var coord in _foundations)
                    if (!next.Contains(coord)) dirty.Add(ChunkKey(coord));
                _foundations.Clear();
                _foundations.UnionWith(next);
            }
            foreach (var kv in territory)
                if (!_territory.TryGetValue(kv.Key, out var old) || old != kv.Value)
                    dirty.Add(ChunkKey(kv.Key));
            foreach (var c in _territory.Keys)
                if (!territory.ContainsKey(c))
                    dirty.Add(ChunkKey(c));

            _territory.Clear();
            foreach (var kv in territory) _territory[kv.Key] = kv.Value;

            foreach (var key in dirty)
                if (_chunks.TryGetValue(key, out var chunk))
                    RebuildChunk(chunk);
        }

        /// <summary>Point on the map under a screen ray (nudged into the hit prism so walls resolve to the taller tile).</summary>
        public bool Raycast(Ray ray, out Vector3 point)
        {
            if (Physics.Raycast(ray, out var hit, 5000f))
            {
                point = hit.point - hit.normal * 0.02f;
                return true;
            }
            point = default;
            return false;
        }

        /// <summary>World-space XZ rectangle covering the map, for camera bounds.</summary>
        public Rect WorldBounds() => _bounds;

        static void DestroyOwned(Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        void OnDestroy()
        {
            foreach (var chunk in _chunks.Values) DestroyOwned(chunk.Mesh);
            if (_material != null) DestroyOwned(_material);
        }

        static (int, int) ChunkKey(HexCoord c)
        {
            var (col, row) = c.ToOffset();
            return (Mathf.FloorToInt(col / (float)ChunkSize), Mathf.FloorToInt(row / (float)ChunkSize));
        }

        Chunk CreateChunk((int, int) key)
        {
            var go = new GameObject($"Chunk {key.Item1},{key.Item2}");
            go.transform.SetParent(transform, false);
            var mesh = new Mesh { name = go.name, indexFormat = IndexFormat.UInt32 };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = _material;
            return new Chunk { GameObject = go, Mesh = mesh, Collider = go.AddComponent<MeshCollider>() };
        }

        void RebuildChunk(Chunk chunk)
        {
            _builder.Clear();
            foreach (var tile in chunk.Tiles)
            {
                Color? tint = null;
                if (_territory.TryGetValue(tile.Coord, out var t)) tint = t;
                AddTile(tile, tint);
            }
            _builder.ApplyTo(chunk.Mesh);
            chunk.Collider.sharedMesh = null; // force the collider to pick up the rebuilt mesh
            chunk.Collider.sharedMesh = chunk.Mesh;
        }

        public void SetHovered(Tile tile) => PlaceOutline(_hoverOutline, tile, 0.95f);
        public void SetSelected(Tile tile) => PlaceOutline(_selectOutline, tile, 0.88f);

        void PlaceOutline(LineRenderer line, Tile tile, float scale)
        {
            if (tile == null)
            {
                line.enabled = false;
                return;
            }

            var center = ToWorld(tile.Coord);
            for (int i = 0; i < 48; i++)
            {
                int edge = i / 8;
                var a = HexMeshBuilder.Corner(center, edge, 0f, HexSize * scale);
                var b = HexMeshBuilder.Corner(center, edge + 1, 0f, HexSize * scale);
                var p = Vector3.Lerp(a, b, (i % 8) / 8f);
                p.y = SurfaceHeight(tile, p) + .04f;
                line.SetPosition(i, p);
            }
            line.enabled = true;
        }

        public float SurfaceHeight(Tile tile, Vector3 point)
        {
            var center = ToWorld(tile.Coord);
            return TopHeight(tile) + (_surfaces.TryGetValue(tile.Coord, out var surface)
                ? surface.HeightAt(point.x - center.x, point.z - center.z) : 0f);
        }

        float MarkerHeight(Tile tile, float x, float z, float radius) => TopHeight(tile) + .025f +
            (_surfaces.TryGetValue(tile.Coord, out var surface) ? surface.MarkerHeight(x, z, radius) : 0f);

        void AddTile(Tile tile, Color? tint)
        {
            var center = ToWorld(tile.Coord);
            float top = TopHeight(tile);
            var baseColor = _terrainColors[tile.Terrain];
            if (tint.HasValue) baseColor = Color.Lerp(baseColor, tint.Value, TerritoryTint);

            // Top face: brighter on higher terraces.
            var surface = _map.Body == BodyId.Moon
                ? _lunarArt.GetSurface(tile, _map.Seed, _foundations.Contains(tile.Coord)) : null;
            if (surface != null)
            {
                _surfaces[tile.Coord] = surface;
                surface.AppendTo(_builder, new Vector3(center.x, top, center.z), tint);
            }
            else _builder.AddHexTop(center, top, HexSize, HexMeshBuilder.Shade(baseColor, 0.9f + 0.05f * HeightLevel(tile)));

            // Side walls only where this tile stands above its neighbour (or at the map edge).
            for (int i = 0; i < 6; i++)
            {
                var a = HexMeshBuilder.Corner(center, i, top, HexSize);
                var b = HexMeshBuilder.Corner(center, i + 1, top, HexSize);
                var edgeMid = (a + b) * 0.5f;
                var neighborCenter = center + new Vector3(edgeMid.x - center.x, 0f, edgeMid.z - center.z) * 2f;

                float bottom = _map.TryGetTile(FromWorld(neighborCenter), out var neighbor) ? TopHeight(neighbor) : 0f;
                if (bottom >= top) continue;

                _builder.AddWall(a, b, bottom, HexMeshBuilder.Shade(baseColor, HexMeshBuilder.WallLight(i)));
            }

            // Resource pips, matching the bonuses in Tile.Yields and the ice-mine threshold.
            if (tile.Terrain != TerrainType.PolarIce && tile.Terrain != TerrainType.ShadowedCrater && tile.Ice >= 0.4f)
                _builder.AddHexTop(center + new Vector3(-0.38f, 0f, -0.3f), MarkerHeight(tile, -.38f, -.3f, .17f), 0.17f, IceColor);
            if (tile.Minerals >= 0.7f)
                _builder.AddHexTop(center + new Vector3(0.38f, 0f, -0.3f), MarkerHeight(tile, .38f, -.3f, .17f), 0.17f, MineralColor);
            if (tile.Feature == TileFeature.LavaTube)
                _builder.AddHexTop(center + new Vector3(0f, 0f, 0.35f), MarkerHeight(tile, 0f, .35f, .24f), 0.24f, LavaTubeColor);
            var deposit = DepositCatalog.Get(tile.Deposit);
            if (deposit != null)
                _builder.AddHexTop(center + new Vector3(0f, 0f, -0.05f), MarkerHeight(tile, 0f, -.05f, .24f) + .005f, 0.24f, ParseColor(deposit.Color));
            if (tile.Feature == TileFeature.PeakOfLight)
                _builder.AddHexTop(center + new Vector3(0f, 0f, 0.35f), MarkerHeight(tile, 0f, .35f, .2f), 0.2f, PeakColor);
        }
    }
}
