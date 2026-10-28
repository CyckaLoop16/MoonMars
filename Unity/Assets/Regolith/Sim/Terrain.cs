using System.Collections.Generic;

namespace Regolith.Sim
{
    public enum TerrainType
    {
        RegolithPlains,
        DuneField,
        Highlands,
        VolcanicRise,
        CraterFloor,
        Canyon,
        PolarIce,

        // Moon
        LunarHighlands,
        LunarMare,
        ShadowedCrater,
    }

    public enum TileFeature
    {
        None,
        CraterRim,
        LavaTube,
        /// <summary>Moon: a high rim near the pole that sees the Sun almost constantly.</summary>
        PeakOfLight,
    }

    public struct Yields
    {
        public int Materials;
        public int Energy;
        public int Water;
        public int Science;

        public Yields(int materials, int energy, int water, int science)
        {
            Materials = materials;
            Energy = energy;
            Water = water;
            Science = science;
        }

        public static Yields operator +(Yields a, Yields b) =>
            new Yields(a.Materials + b.Materials, a.Energy + b.Energy, a.Water + b.Water, a.Science + b.Science);

        public int Total => Materials + Energy + Water + Science;

        public override string ToString() => $"Materials {Materials} · Energy {Energy} · Water {Water} · Science {Science}";
    }

    public sealed class TerrainDef
    {
        public TerrainType Type;
        public string Name;
        public string Description;
        /// <summary>sRGB hex colour, e.g. "#b5603a". The sim never draws, it only names the colour.</summary>
        public string Color;
        public Yields BaseYields;
        public int MoveCost;
    }

    /// <summary>
    /// Terrain definitions. Hard-coded while prototyping; will move to data files (see docs/TECH.md).
    /// </summary>
    public static class TerrainCatalog
    {
        static readonly Dictionary<TerrainType, TerrainDef> Defs = new Dictionary<TerrainType, TerrainDef>();

        static TerrainCatalog()
        {
            Add(TerrainType.RegolithPlains, "Regolith Plains", "#b5653e", new Yields(1, 1, 0, 0), 1,
                "Flat, dusty, unremarkable. Easy to build on.");
            Add(TerrainType.DuneField, "Dune Field", "#d08a55", new Yields(0, 1, 0, 0), 2,
                "Shifting sand. Buries solar panels.");
            Add(TerrainType.Highlands, "Cratered Highlands", "#8f4a2f", new Yields(2, 0, 0, 0), 2,
                "Ancient, rugged southern terrain. Rich in rock.");
            Add(TerrainType.VolcanicRise, "Volcanic Rise", "#5e3226", new Yields(2, 1, 0, 0), 3,
                "Basaltic slopes. Lava tubes make natural radiation shelters.");
            Add(TerrainType.CraterFloor, "Crater Floor", "#7a4a3a", new Yields(1, 0, 1, 0), 1,
                "Sheltered basin. Shadowed edges trap ice.");
            Add(TerrainType.Canyon, "Canyon", "#6b3a2e", new Yields(0, 0, 1, 2), 3,
                "Exposed strata reveal billions of years of history.");
            Add(TerrainType.PolarIce, "Polar Ice Cap", "#e8e2da", new Yields(0, 0, 3, 0), 3,
                "Water and CO₂ ice. Endless water, brutal cold.");

            Add(TerrainType.LunarHighlands, "Lunar Highlands", "#a9a6a0", new Yields(1, 0, 0, 0), 2,
                "Bright, ancient anorthosite crust. Rugged but solid ground.");
            Add(TerrainType.LunarMare, "Mare Basalt", "#62605c", new Yields(2, 0, 0, 0), 1,
                "Dark plains of ancient lava. Smooth, rich in iron and titanium.");
            Add(TerrainType.ShadowedCrater, "Permanently Shadowed Crater", "#2c303a", new Yields(0, 0, 3, 1), 3,
                "Has not seen sunlight in two billion years. −230 °C and full of water ice.");
        }

        static void Add(TerrainType type, string name, string color, Yields yields, int moveCost, string description)
        {
            Defs[type] = new TerrainDef
            {
                Type = type, Name = name, Color = color, BaseYields = yields, MoveCost = moveCost, Description = description,
            };
        }

        public static TerrainDef Get(TerrainType type) => Defs[type];

        public static IEnumerable<TerrainDef> All => Defs.Values;
    }
}
