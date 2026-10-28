using System;
using System.Collections.Generic;

namespace Regolith.Sim
{
    /// <summary>
    /// Procedural lunar south-polar region, with the pole at the map centre. Deterministic per seed.
    /// Near the pole, crater floors never see the Sun (ice-rich permanently shadowed craters) while
    /// their high rims see it almost constantly (peaks of light). Settling between the two is the
    /// central Act I puzzle, just as it is for real lunar missions.
    /// </summary>
    public static class LunarMapGenerator
    {
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

            var map = new WorldMap(width, height, seed, BodyId.Moon);
            var rng = new Random(seed);
            var elevationNoise = new PerlinNoise(seed);
            var mareNoise = new PerlinNoise(seed + 1);
            var mineralNoise = new PerlinNoise(seed + 2);
            var iceNoise = new PerlinNoise(seed + 3);

            var pole = HexCoord.FromOffset(width / 2, height / 2);
            double poleRange = Math.Max(width, height) / 2.0;
            var craters = PlaceCraters(rng, width, height, pole);

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    var coord = HexCoord.FromOffset(col, row);
                    var tile = new Tile(coord);

                    // 1 at the pole, falling to 0 at the map edge.
                    double polar = 1.0 - Math.Min(1.0, coord.DistanceTo(pole) / poleRange);
                    double elevation = elevationNoise.Fbm(col * 0.08, row * 0.08) * 1.3;

                    bool craterFloor = false, craterRim = false;
                    foreach (var c in craters)
                    {
                        int d = coord.DistanceTo(c.Center);
                        if (d < c.Radius)
                        {
                            craterFloor = true;
                            elevation -= 0.3 + 0.4 * (1.0 - d / (double)c.Radius);
                        }
                        else if (d == c.Radius)
                        {
                            craterRim = true;
                            elevation += 0.3;
                        }
                    }

                    TerrainType terrain;
                    if (craterFloor && polar > 0.45) terrain = TerrainType.ShadowedCrater;
                    else if (polar < 0.5 && mareNoise.Fbm(col * 0.06, row * 0.06, 3) > 0.2) terrain = TerrainType.LunarMare;
                    else terrain = TerrainType.LunarHighlands;

                    var feature = TileFeature.None;
                    if (craterRim && !craterFloor) feature = polar > 0.75 ? TileFeature.PeakOfLight : TileFeature.CraterRim;
                    else if (terrain == TerrainType.LunarMare && rng.NextDouble() < 0.04) feature = TileFeature.LavaTube;

                    double ice = polar * polar * 0.25 + Math.Max(0.0, iceNoise.Fbm(col * 0.15, row * 0.15, 2)) * 0.1;
                    if (terrain == TerrainType.ShadowedCrater) ice = 0.55 + 0.45 * polar;

                    double minerals = 0.45 + mineralNoise.Fbm(col * 0.12, row * 0.12, 3) * 1.2;
                    if (terrain == TerrainType.LunarMare) minerals += 0.2; // ilmenite: iron and titanium
                    if (feature == TileFeature.CraterRim) minerals += 0.15;

                    tile.Terrain = terrain;
                    tile.Feature = feature;
                    tile.Elevation = (float)Clamp(elevation, -1.0, 1.0);
                    tile.Ice = (float)Clamp(ice, 0.0, 1.0);
                    tile.Minerals = (float)Clamp(minerals, 0.0, 1.0);
                    map.Add(tile);
                }
            }

            return map;
        }

        static List<Crater> PlaceCraters(Random rng, int width, int height, HexCoord pole)
        {
            // A guaranteed crater on the pole itself, like Shackleton.
            var craters = new List<Crater> { new Crater(pole, 2) };

            int count = width * height / 70;
            for (int i = 0; i < count; i++)
            {
                var center = HexCoord.FromOffset(rng.Next(width), rng.Next(height));
                double roll = rng.NextDouble();
                int radius = roll < 0.5 ? 1 : roll < 0.85 ? 2 : 3;
                craters.Add(new Crater(center, radius));
            }
            return craters;
        }

        static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
    }
}
