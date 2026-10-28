using System;
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
        LunarMassif,
        EjectaBlanket,
    }

    public enum TileFeature
    {
        None,
        CraterRim,
        LavaTube,
        /// <summary>Moon: a high rim near the pole that sees the Sun almost constantly.</summary>
        PeakOfLight,
    }

    /// <summary>Harvestable resource on a tile (Civ-style). Build the right module on it to benefit.</summary>
    public enum Deposit
    {
        None,
        Ilmenite,
        Volatiles,
        RareEarths,
        PlatinumMetals,
    }

    public enum DepositKind
    {
        /// <summary>Boosts a module built on the tile.</summary>
        Bonus,
        /// <summary>Mined into its own stockpile for advanced construction and Earth trade.</summary>
        Strategic,
    }

    public sealed class DepositDef
    {
        public Deposit Type;
        public DepositKind Kind;
        public string Name;
        public string Description;
        /// <summary>sRGB hex colour for the map marker.</summary>
        public string Color;
        /// <summary>Extra automatic monthly yield of the tile.</summary>
        public Yields TileBonus;
        public ModuleType[] BoostedModules = new ModuleType[0];
        public double Multiplier = 1.0;
    }

    public static class DepositCatalog
    {
        static readonly Dictionary<Deposit, DepositDef> Defs = new Dictionary<Deposit, DepositDef>();

        static DepositCatalog()
        {
            Add(new DepositDef
            {
                Type = Deposit.Ilmenite, Kind = DepositKind.Bonus, Name = "Ilmenite", Color = "#7d93a6",
                Description = "Iron-titanium oxide. Oxygen Plants and Foundries built here produce 50% more.",
                TileBonus = new Yields(1, 0, 0, 0), Multiplier = 1.5,
                BoostedModules = new[] { ModuleType.OxygenPlant, ModuleType.Foundry },
            });
            Add(new DepositDef
            {
                Type = Deposit.Volatiles, Kind = DepositKind.Bonus, Name = "Cold-Trapped Volatiles", Color = "#b48cff",
                Description = "Ice laced with CO₂, ammonia and methane, as LCROSS found. Ice Mines built here produce 50% more.",
                TileBonus = new Yields(0, 0, 0, 1), Multiplier = 1.5,
                BoostedModules = new[] { ModuleType.IceMine },
            });
            Add(new DepositDef
            {
                Type = Deposit.RareEarths, Kind = DepositKind.Strategic, Name = "Rare-Earth Elements", Color = "#e0609e",
                Description = "KREEP-rich crust from the South Pole–Aitken basin. Strategic: needed for electronics and prized by Earth.",
                TileBonus = new Yields(0, 0, 0, 1),
            });
            Add(new DepositDef
            {
                Type = Deposit.PlatinumMetals, Kind = DepositKind.Strategic, Name = "Platinum-Group Metals", Color = "#e8e8f2",
                Description = "Remnants of a metallic asteroid impactor. Strategic: catalysts for fuel cells and electrolysis.",
                TileBonus = new Yields(1, 0, 0, 0),
            });
        }

        static void Add(DepositDef def) => Defs[def.Type] = def;

        public static DepositDef Get(Deposit d) => d == Deposit.None ? null : Defs[d];

        public static double Multiplier(Deposit d, ModuleType module)
        {
            var def = Get(d);
            return def != null && Array.IndexOf(def.BoostedModules, module) >= 0 ? def.Multiplier : 1.0;
        }
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
        /// <summary>Automatic monthly yield of a claimed tile (Civ-style), no workers needed.</summary>
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
            Add(TerrainType.ShadowedCrater, "Permanently Shadowed Crater", "#2c303a", new Yields(0, 0, 0, 1), 3,
                "Has not seen sunlight in two billion years. −230 °C and full of water ice.");
            Add(TerrainType.LunarMassif, "Lunar Massif", "#8e8a83", new Yields(2, 0, 0, 0), 4,
                "Kilometres-high mountain. Hard going, but the summits catch the low polar Sun.");
            Add(TerrainType.EjectaBlanket, "Fresh Ejecta", "#cfcbc3", new Yields(2, 0, 0, 1), 2,
                "Bright debris thrown out by a young impact. Rough, but rich in exposed minerals.");
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
