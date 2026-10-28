using System;
using System.Collections.Generic;

namespace Regolith.Sim
{
    /// <summary>
    /// Procedural Mars-like map. Deterministic: the same (width, height, seed) always yields the same map.
    /// Row 0 is the north pole, the last row the south pole.
    /// </summary>
    public static class MapGenerator
    {
        public const float PolarLatitude = 0.86f;

        readonly struct Crater
        {
            public readonly HexCoord Center;
            public readonly int Radius;

            public Crater(HexCoord center, int radius)
            {
                Center = center;
                Radius = radius;
            }
        }

        public static WorldMap Generate(int width, int height, int seed)
        {
            if (width < 8 || height < 8) throw new ArgumentException("Map must be at least 8x8.");

            var map = new WorldMap(width, height, seed);
            var rng = new Random(seed);
            var elevationNoise = new PerlinNoise(seed);
            var iceNoise = new PerlinNoise(seed + 1);
            var mineralNoise = new PerlinNoise(seed + 2);
            var duneNoise = new PerlinNoise(seed + 3);

            var craters = PlaceCraters(rng, width, height);
            var volcanoes = PlaceVolcanoes(rng, width, height);

            for (int row = 0; row < height; row++)
            {
                double southness = (double)row / (height - 1); // 0 = north pole, 1 = south pole
                double latitude = Math.Abs(southness * 2.0 - 1.0); // 0 = equator, 1 = pole

                for (int col = 0; col < width; col++)
                {
                    var coord = HexCoord.FromOffset(col, row);
                    var tile = new Tile(coord);

                    // Hemispheric dichotomy: low smooth north, high cratered south.
                    double elevation = elevationNoise.Fbm(col * 0.07, row * 0.07) * 1.5 + Lerp(-0.25, 0.25, southness);

                    bool volcanicCore = false;
                    foreach (var v in volcanoes)
                    {
                        double d = coord.DistanceTo(v.Center) / (double)v.Radius;
                        double bulge = 0.7 * Math.Exp(-d * d * 2.0);
                        elevation += bulge;
                        if (bulge > 0.45) volcanicCore = true;
                    }

                    bool craterFloor = false, craterRim = false;
                    foreach (var c in craters)
                    {
                        int d = coord.DistanceTo(c.Center);
                        if (d < c.Radius) { craterFloor = true; elevation -= 0.35; }
                        else if (d == c.Radius) { craterRim = true; elevation += 0.15; }
                    }

                    double ice = Clamp01(0.5 + iceNoise.Fbm(col * 0.11, row * 0.11, 3) * 1.4) * 0.55 + latitude * latitude * 0.6;
                    double minerals = Clamp01(0.5 + mineralNoise.Fbm(col * 0.13, row * 0.13, 3) * 1.2);

                    TerrainType terrain;
                    if (latitude > PolarLatitude) terrain = TerrainType.PolarIce;
                    else if (craterFloor) terrain = TerrainType.CraterFloor;
                    else if (volcanicCore || elevation > 0.55) terrain = TerrainType.VolcanicRise;
                    else if (elevation > 0.2) terrain = TerrainType.Highlands;
                    else if (duneNoise.Fbm(col * 0.12, row * 0.12, 2) > 0.18) terrain = TerrainType.DuneField;
                    else terrain = TerrainType.RegolithPlains;

                    var feature = TileFeature.None;
                    if (terrain != TerrainType.PolarIce && craterRim && !craterFloor)
                    {
                        feature = TileFeature.CraterRim;
                        minerals += 0.25; // impacts excavate deep ore
                    }

                    if (terrain == TerrainType.CraterFloor) ice += 0.2; // shadowed walls trap ice
                    if (terrain == TerrainType.VolcanicRise)
                    {
                        ice -= 0.15;
                        minerals += 0.2;
                        if (feature == TileFeature.None && rng.NextDouble() < 0.12) feature = TileFeature.LavaTube;
                    }

                    tile.Terrain = terrain;
                    tile.Feature = feature;
                    tile.Elevation = (float)Clamp(elevation, -1.0, 1.0);
                    tile.Ice = (float)Clamp01(ice);
                    tile.Minerals = (float)Clamp01(minerals);
                    map.Add(tile);
                }
            }

            CarveCanyon(map, rng);
            return map;
        }

        static List<Crater> PlaceCraters(Random rng, int width, int height)
        {
            var craters = new List<Crater>();
            int count = width * height / 120;
            for (int i = 0; i < count; i++)
            {
                // Bias toward the southern hemisphere, like real Mars; keep out of the polar caps.
                int margin = (int)(height * (1.0 - PolarLatitude) / 2.0) + 1;
                int row = margin + (int)((1.0 - Math.Pow(rng.NextDouble(), 2.0)) * (height - 2 * margin - 1));
                int col = rng.Next(width);
                double roll = rng.NextDouble();
                int radius = roll < 0.55 ? 1 : roll < 0.85 ? 2 : 3;
                craters.Add(new Crater(HexCoord.FromOffset(col, row), radius));
            }
            return craters;
        }

        static List<Crater> PlaceVolcanoes(Random rng, int width, int height)
        {
            // Reuses the Crater shape as "centre + radius" for Tharsis-like volcanic bulges.
            var volcanoes = new List<Crater>();
            int count = 1 + rng.Next(2);
            for (int i = 0; i < count; i++)
            {
                int col = (int)(width * (0.15 + rng.NextDouble() * 0.7));
                int row = (int)(height * (0.35 + rng.NextDouble() * 0.3));
                int radius = Math.Max(3, width / 9);
                volcanoes.Add(new Crater(HexCoord.FromOffset(col, row), radius));
            }
            return volcanoes;
        }

        /// <summary>A Valles Marineris-like trench running roughly east-west near the equator.</summary>
        static void CarveCanyon(WorldMap map, Random rng)
        {
            int row = (int)(map.Height * (0.4 + rng.NextDouble() * 0.2));
            int startCol = (int)(map.Width * (0.1 + rng.NextDouble() * 0.25));
            int length = (int)(map.Width * (0.3 + rng.NextDouble() * 0.15));

            for (int i = 0; i < length; i++)
            {
                int col = startCol + i;
                if (col >= map.Width) break;

                MakeCanyon(map, HexCoord.FromOffset(col, row));
                if (rng.NextDouble() < 0.5) MakeCanyon(map, HexCoord.FromOffset(col, row + 1));

                if (rng.NextDouble() < 0.3)
                    row = Math.Max(2, Math.Min(map.Height - 3, row + rng.Next(-1, 2)));
            }
        }

        static void MakeCanyon(WorldMap map, HexCoord c)
        {
            var tile = map.GetTile(c);
            if (tile == null || tile.Terrain == TerrainType.PolarIce) return;
            tile.Terrain = TerrainType.Canyon;
            tile.Feature = TileFeature.None;
            tile.Elevation = Math.Min(tile.Elevation, -0.6f);
            tile.Ice = Math.Min(1f, tile.Ice + 0.15f);
        }

        static double Lerp(double a, double b, double t) => a + (b - a) * t;
        static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
        static double Clamp01(double v) => Clamp(v, 0.0, 1.0);
    }
}
