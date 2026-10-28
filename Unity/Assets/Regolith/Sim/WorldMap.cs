using System.Collections.Generic;

namespace Regolith.Sim
{
    public sealed class Tile
    {
        public HexCoord Coord { get; }
        public TerrainType Terrain;
        public TileFeature Feature;

        /// <summary>Roughly [-1, 1]. Rendered as terraced height levels.</summary>
        public float Elevation;
        /// <summary>0..1 subsurface water ice.</summary>
        public float Ice;
        /// <summary>0..1 mineral richness.</summary>
        public float Minerals;

        public Tile(HexCoord coord)
        {
            Coord = coord;
        }

        public TerrainDef Def => TerrainCatalog.Get(Terrain);

        /// <summary>Base terrain yields plus bonuses from deposits and features.</summary>
        public Yields Yields
        {
            get
            {
                var y = Def.BaseYields;
                if (Terrain != TerrainType.PolarIce)
                {
                    if (Ice >= 0.85f) y.Water += 2;
                    else if (Ice >= 0.6f) y.Water += 1;
                }
                if (Minerals >= 0.7f) y.Materials += 1;
                if (Feature == TileFeature.CraterRim) y.Science += 1;
                return y;
            }
        }
    }

    public sealed class WorldMap
    {
        public int Width { get; }
        public int Height { get; }
        public int Seed { get; }

        readonly Dictionary<HexCoord, Tile> _tiles = new Dictionary<HexCoord, Tile>();

        public WorldMap(int width, int height, int seed)
        {
            Width = width;
            Height = height;
            Seed = seed;
        }

        public IEnumerable<Tile> Tiles => _tiles.Values;
        public int TileCount => _tiles.Count;

        public void Add(Tile tile) => _tiles[tile.Coord] = tile;
        public bool TryGetTile(HexCoord c, out Tile tile) => _tiles.TryGetValue(c, out tile);
        public Tile GetTile(HexCoord c) => _tiles.TryGetValue(c, out var t) ? t : null;
        public bool Contains(HexCoord c) => _tiles.ContainsKey(c);
    }
}
