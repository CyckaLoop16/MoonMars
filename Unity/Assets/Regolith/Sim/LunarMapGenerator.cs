using System;
using System.Collections.Generic;

namespace Regolith.Sim
{
    /// <summary>
    /// The lunar south-polar region at <see cref="KmPerHex"/> km per hex, with the pole at the map centre.
    ///
    /// Real landmarks (Shackleton, de Gerlache, Shoemaker, Malapert Massif…) are placed at their true
    /// positions and sizes; everything between them is procedural and seeded. Earth-facing longitude 0°
    /// points right (east on screen); the far side, inside the South Pole–Aitken basin, lies to the left.
    ///
    /// Craters are stamped oldest-first (biggest first), so young craters overprint old ones as they do on
    /// the real Moon. Near the pole, crater floors become permanently shadowed ice traps while high rims and
    /// summits become peaks of light: settling between the two is the core Act I puzzle.
    /// </summary>
    public static class LunarMapGenerator
    {
        public const double KmPerHex = 3.0;
        /// <summary>Crater floors closer to the pole than this never see the Sun.</summary>
        public const double ShadowLimitKm = 150.0;
        /// <summary>High ground closer to the pole than this sees the Sun most of the lunar month.</summary>
        public const double PeakLightLimitKm = 130.0;
        public const float PeakElevation = 0.75f;

        const double KmPerDegree = 30.32; // lunar radius 1737.4 km

        sealed class CraterSite
        {
            public string Name;
            public string Description;
            public HexCoord Center;
            public int Radius;
            public double RimBoost;
            public double IceBonus;
            public bool Fresh;
        }

        /// <summary>Per-hex scratch data while generating.</summary>
        sealed class Cell
        {
            public Tile Tile;
            public double Elevation;
            public double DistanceKm;
            public double FarSide; // 0 = Earth-facing edge, 1 = far-side edge
            public bool Interior, Rim, Ejecta, Massif, Ridge, CentralPeak;
            public double IceBonus;
        }

        public static WorldMap Generate(int width, int height, int seed)
        {
            if (width < 16 || height < 12) throw new ArgumentException("Map must be at least 16x12.");

            var map = new WorldMap(width, height, seed, BodyId.Moon);
            var rng = new Random(seed);
            var terrainNoise = new PerlinNoise(seed);
            var mineralNoise = new PerlinNoise(seed + 1);
            var detailNoise = new PerlinNoise(seed + 2);

            var pole = HexCoord.FromOffset(width / 2, height / 2);
            var (poleX, poleY) = HexLayout.ToPlane(pole, 1.0);
            double halfWidthKm = width / 2.0 * KmPerHex;

            // 1. Base terrain: gently rolling, heavily cratered highlands.
            var cells = new Dictionary<HexCoord, Cell>();
            for (int row = 0; row < height; row++)
            for (int col = 0; col < width; col++)
            {
                var coord = HexCoord.FromOffset(col, row);
                var (x, y) = HexLayout.ToPlane(coord, 1.0);
                double kmX = (x - poleX) / Math.Sqrt(3.0) * KmPerHex;
                double kmY = (y - poleY) / Math.Sqrt(3.0) * KmPerHex;

                var tile = new Tile(coord);
                map.Add(tile);
                cells[coord] = new Cell
                {
                    Tile = tile,
                    DistanceKm = Math.Sqrt(kmX * kmX + kmY * kmY),
                    FarSide = Clamp01(0.5 - kmX / (2.0 * halfWidthKm)),
                    Elevation = terrainNoise.Fbm(col * 0.05, row * 0.05, 5) * 0.35,
                };
            }

            HexCoord Place(double latS, double lonE) => PolarToHex(latS, lonE, pole, poleX, poleY);

            // 2. Mountains first: they predate most of the craters around them.
            var malapert = Place(86.0, -2.9);
            var mouton = Place(84.6, 31.1);
            StampMassif(cells, detailNoise, malapert, 6, 1.35);
            StampMassif(cells, detailNoise, mouton, 10, 1.15);
            AddLandmark(map, "Malapert Massif", LandmarkKind.Massif, malapert, 6,
                "A 5 km mountain with near-constant sunlight and a direct line of sight to Earth.");
            AddLandmark(map, "Mons Mouton", LandmarkKind.Massif, mouton, 10,
                "A broad plateau 6 km high, named for NASA mathematician Melba Mouton.");

            // 3. Craters, biggest (oldest) first, so young ones overprint old ones.
            var craters = RandomCraters(rng, width, height);
            craters.AddRange(NamedCraters(Place));
            craters.Sort((a, b) => b.Radius.CompareTo(a.Radius));
            foreach (var c in craters)
            {
                StampCrater(cells, detailNoise, c);
                if (c.Name != null) AddLandmark(map, c.Name, LandmarkKind.Crater, c.Center, c.Radius, c.Description);
            }

            // 4. The ridge linking Shackleton and de Gerlache: sunlit high ground between two ice traps.
            var shackleton = map.Landmarks.Find(l => l.Name == "Shackleton");
            var gerlache = map.Landmarks.Find(l => l.Name == "de Gerlache");
            StampRidge(cells, shackleton, gerlache);

            // 5. Classify every hex, then scatter deposits (row by row, so the RNG order is fixed).
            foreach (var cell in cells.Values)
                Classify(cell, mineralNoise, detailNoise);

            for (int row = 0; row < height; row++)
            for (int col = 0; col < width; col++)
                PlaceDeposit(cells[HexCoord.FromOffset(col, row)], rng, mineralNoise);

            return map;
        }

        static List<CraterSite> NamedCraters(Func<double, double, HexCoord> place)
        {
            CraterSite Named(string name, double latS, double lonE, double diameterKm, string description,
                double rimBoost = 0.25, double ice = 0.0)
            {
                return new CraterSite
                {
                    Name = name,
                    Description = description,
                    Center = place(latS, lonE),
                    Radius = Math.Max(1, (int)Math.Round(diameterKm / 2.0 / KmPerHex)),
                    RimBoost = rimBoost,
                    IceBonus = ice,
                };
            }

            return new List<CraterSite>
            {
                Named("Shackleton", 89.67, 129.8, 21,
                    "Almost exactly on the south pole. Its floor has been dark for billions of years; its rim is lit almost constantly.",
                    rimBoost: 0.75, ice: 0.1),
                Named("de Gerlache", 88.5, -87.1, 32.5,
                    "Joined to Shackleton by a sunlit ridge, one of the most sought-after landing sites on the Moon.",
                    rimBoost: 0.55),
                Named("Sverdrup", 88.5, -152.0, 33, "Deep, dark and cold. A dependable ice trap."),
                Named("Shoemaker", 88.1, 44.9, 51,
                    "Named for Gene Shoemaker, whose ashes rest on the Moon. Its floor is a vast cold trap.",
                    ice: 0.05),
                Named("Faustini", 87.3, 77.0, 39, "One of the coldest places ever measured in the solar system.", ice: 0.05),
                Named("Haworth", 87.45, -5.0, 51, "A huge permanently shadowed crater facing Earth."),
                Named("Nobile", 85.2, 53.5, 73, "An old, worn crater on the road to Mons Mouton.", rimBoost: 0.1),
                Named("Cabeus", 85.3, -42.1, 98,
                    "In 2009 NASA's LCROSS probe struck Cabeus and confirmed water ice in the plume.",
                    rimBoost: 0.15, ice: 0.15),
                Named("Amundsen", 84.4, 85.6, 103, "A large complex crater with a central peak.", rimBoost: 0.2),
            };
        }

        /// <summary>Power-law crater sizes: many small craters, a few big ones.</summary>
        static List<CraterSite> RandomCraters(Random rng, int width, int height)
        {
            var list = new List<CraterSite>();
            int count = width * height / 55;
            for (int i = 0; i < count; i++)
            {
                double u = 0.01 + rng.NextDouble() * 0.99;
                int radius = Math.Min(12, (int)(1.0 / Math.Sqrt(u)));
                list.Add(new CraterSite
                {
                    Center = HexCoord.FromOffset(rng.Next(width), rng.Next(height)),
                    Radius = radius,
                    RimBoost = 0.1 + rng.NextDouble() * 0.15,
                    Fresh = radius <= 4 && rng.NextDouble() < 0.3,
                });
            }
            return list;
        }

        static void StampCrater(Dictionary<HexCoord, Cell> cells, PerlinNoise noise, CraterSite c)
        {
            double baseElevation = cells.TryGetValue(c.Center, out var centerCell) ? centerCell.Elevation : 0.0;
            double depth = 0.45 + 0.35 * Math.Min(1.0, c.Radius / 8.0);
            double floor = baseElevation - depth;
            double rim = baseElevation + 0.2 + 0.15 * Math.Min(1.0, c.Radius / 6.0) + c.RimBoost;
            int ejectaReach = c.Fresh ? c.Radius + 1 + c.Radius / 2 : c.Radius;

            foreach (var h in c.Center.WithinRange(ejectaReach))
            {
                if (!cells.TryGetValue(h, out var cell)) continue;
                int d = h.DistanceTo(c.Center);
                double wobble = noise.Sample(h.Q * 0.31, h.R * 0.31) * 0.08;

                if (d < c.Radius)
                {
                    double t = d / (double)c.Radius;
                    double e = t < 0.6 ? floor : Lerp(floor, rim - 0.2, (t - 0.6) / 0.4);
                    bool centralPeak = c.Radius >= 5 && d <= 1; // complex craters rebound into a central peak
                    if (centralPeak) e = floor + 0.45;

                    cell.Elevation = e + wobble;
                    cell.Interior = true;
                    cell.Rim = cell.Ejecta = cell.Massif = cell.Ridge = false;
                    cell.CentralPeak = centralPeak;
                    cell.IceBonus = Math.Max(cell.IceBonus, c.IceBonus);
                }
                else if (d == c.Radius)
                {
                    cell.Elevation = Math.Max(cell.Elevation, rim + wobble);
                    if (c.Radius >= 2) cell.Rim = true;
                }
                else
                {
                    cell.Elevation += 0.05;
                    cell.Ejecta = true;
                }
            }
        }

        static void StampMassif(Dictionary<HexCoord, Cell> cells, PerlinNoise noise, HexCoord center, int radius, double height)
        {
            double baseElevation = cells.TryGetValue(center, out var c) ? c.Elevation : 0.0;
            foreach (var h in center.WithinRange(radius))
            {
                if (!cells.TryGetValue(h, out var cell)) continue;
                double t = h.DistanceTo(center) / (double)radius;
                double shape = t < 0.45 ? 1.0 : 1.0 - SmoothStep((t - 0.45) / 0.55);
                double e = baseElevation + height * shape + noise.Sample(h.Q * 0.4, h.R * 0.4) * 0.12;
                if (e > cell.Elevation)
                {
                    cell.Elevation = e;
                    cell.Massif = shape > 0.4;
                }
            }
        }

        static void StampRidge(Dictionary<HexCoord, Cell> cells, Landmark a, Landmark b)
        {
            if (a == null || b == null) return;

            var (ax, ay) = HexLayout.ToPlane(a.Center, 1.0);
            var (bx, by) = HexLayout.ToPlane(b.Center, 1.0);
            int steps = a.Center.DistanceTo(b.Center) * 2;
            for (int i = 0; i <= steps; i++)
            {
                double t = i / (double)steps;
                var h = HexLayout.FromPlane(Lerp(ax, bx, t), Lerp(ay, by, t), 1.0);
                if (h.DistanceTo(a.Center) < a.Radius || h.DistanceTo(b.Center) < b.Radius) continue;
                if (!cells.TryGetValue(h, out var cell)) continue;

                cell.Elevation = Math.Max(cell.Elevation, 0.95);
                cell.Ridge = true;
                cell.Interior = false;
            }
        }

        static void Classify(Cell cell, PerlinNoise mineralNoise, PerlinNoise detailNoise)
        {
            var tile = cell.Tile;
            var (col, row) = tile.Coord.ToOffset();
            double polar = 1.0 - Math.Min(1.0, cell.DistanceKm / ShadowLimitKm);

            bool shadowed = cell.Interior && cell.DistanceKm <= ShadowLimitKm && cell.Elevation < 0.0;
            bool high = cell.Elevation >= PeakElevation && cell.DistanceKm <= PeakLightLimitKm;

            TerrainType terrain;
            if (shadowed) terrain = TerrainType.ShadowedCrater;
            else if (cell.Massif) terrain = TerrainType.LunarMassif;
            else if (cell.Ejecta && !cell.Interior) terrain = TerrainType.EjectaBlanket;
            else terrain = TerrainType.LunarHighlands;

            var feature = TileFeature.None;
            if (!shadowed && high && (cell.Rim || cell.Massif || cell.Ridge)) feature = TileFeature.PeakOfLight;
            else if (cell.Rim) feature = TileFeature.CraterRim;

            double ice = shadowed
                ? 0.55 + 0.3 * polar + cell.IceBonus + Math.Max(0.0, detailNoise.Sample(col * 0.2, row * 0.2)) * 0.1
                : 0.25 * polar * polar;

            // The far side sits inside the South Pole–Aitken basin, which exposes deep crust.
            double minerals = 0.4 + mineralNoise.Fbm(col * 0.09, row * 0.09, 3) + 0.25 * cell.FarSide;
            if (terrain == TerrainType.EjectaBlanket) minerals += 0.25;
            if (terrain == TerrainType.LunarMassif) minerals += 0.1;
            if (feature == TileFeature.CraterRim) minerals += 0.1;

            tile.Terrain = terrain;
            tile.Feature = feature;
            tile.Elevation = (float)Clamp(cell.Elevation, -1.6, 1.6);
            tile.Ice = (float)Clamp01(ice);
            tile.Minerals = (float)Clamp01(minerals);
        }

        /// <summary>Civ-style resources: scarce, and tied to the geology that would really produce them.</summary>
        static void PlaceDeposit(Cell cell, Random rng, PerlinNoise clusterNoise)
        {
            var tile = cell.Tile;
            var (col, row) = tile.Coord.ToOffset();
            double roll = rng.NextDouble();
            double cluster = clusterNoise.Sample(col * 0.17 + 50.0, row * 0.17 + 50.0);

            if (tile.Terrain == TerrainType.ShadowedCrater)
            {
                // Volatiles concentrate in the coldest traps; Cabeus is famously rich.
                if (roll < (cell.IceBonus >= 0.15 ? 0.35 : 0.08)) tile.Deposit = Deposit.Volatiles;
            }
            else if (cell.CentralPeak)
            {
                if (roll < 0.5) tile.Deposit = Deposit.PlatinumMetals;
            }
            else if (cell.FarSide > 0.55 && cluster > 0.2)
            {
                if (roll < 0.2) tile.Deposit = Deposit.RareEarths;
            }
            else if (tile.Terrain == TerrainType.EjectaBlanket)
            {
                if (roll < 0.12) tile.Deposit = Deposit.Ilmenite;
            }
            else if (tile.Feature == TileFeature.CraterRim)
            {
                if (roll < 0.015) tile.Deposit = Deposit.PlatinumMetals;
                else if (roll < 0.05) tile.Deposit = Deposit.Ilmenite;
            }
        }

        /// <summary>Polar stereographic-ish placement: distance from the pole by latitude, bearing by longitude.</summary>
        static HexCoord PolarToHex(double latS, double lonE, HexCoord pole, double poleX, double poleY)
        {
            double km = (90.0 - latS) * KmPerDegree;
            double rad = lonE * Math.PI / 180.0;
            double planePerKm = Math.Sqrt(3.0) / KmPerHex;
            double x = poleX + km * Math.Cos(rad) * planePerKm;
            double y = poleY + km * Math.Sin(rad) * planePerKm;
            return HexLayout.FromPlane(x, y, 1.0);
        }

        static void AddLandmark(WorldMap map, string name, LandmarkKind kind, HexCoord center, int radius, string description)
        {
            map.Landmarks.Add(new Landmark { Name = name, Kind = kind, Center = center, Radius = radius, Description = description });
        }

        static double Lerp(double a, double b, double t) => a + (b - a) * t;
        static double SmoothStep(double t) => t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - 2 * t);
        static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
        static double Clamp01(double v) => Clamp(v, 0.0, 1.0);
    }
}
