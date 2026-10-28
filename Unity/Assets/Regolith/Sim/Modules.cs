using System.Collections.Generic;

namespace Regolith.Sim
{
    public enum Resource
    {
        Oxygen,
        Water,
        Food,
        Materials,
        RareEarths,
        PlatinumMetals,
    }

    public sealed class Stockpile
    {
        public const int Count = 6;
        readonly double[] _amounts = new double[Count];

        public double this[Resource r]
        {
            get => _amounts[(int)r];
            set => _amounts[(int)r] = value;
        }
    }

    public enum ModuleType
    {
        HabCore,
        Habitat,
        SolarArray,
        FissionReactor,
        IceMine,
        OxygenPlant,
        Greenhouse,
        Foundry,
        Lab,
        BatteryBank,
        MineralExtractor,
    }

    public readonly struct Flow
    {
        public readonly Resource Resource;
        public readonly double PerDay;

        public Flow(Resource resource, double perDay)
        {
            Resource = resource;
            PerDay = perDay;
        }
    }

    public sealed class ModuleDef
    {
        public ModuleType Type;
        public string Name;
        public string Description;
        public int MaterialCost;
        public int BuildDays;
        public int Jobs;
        public int Housing;
        /// <summary>Lower runs first: gets workers first and produces first each day.</summary>
        public int Priority;
        public bool Buildable = true;
        public double EnergyOutput;
        public double EnergyDraw;
        public bool IsSolar;
        public double Science;
        public double EnergyStorage;
        /// <summary>Tech needed before this can be built, if any.</summary>
        public TechId? RequiredTech;
        /// <summary>Mines the strategic deposit under it at this rate per day, instead of fixed outputs.</summary>
        public double ExtractionRate;
        public Flow[] Inputs = new Flow[0];
        public Flow[] Outputs = new Flow[0];
    }

    /// <summary>
    /// Colony modules (Civ6-style districts: one per hex). Hard-coded while prototyping.
    /// Numbers are per day at full staffing and full power.
    /// </summary>
    public static class ModuleCatalog
    {
        static readonly Dictionary<ModuleType, ModuleDef> Defs = new Dictionary<ModuleType, ModuleDef>();

        static ModuleCatalog()
        {
            Add(new ModuleDef
            {
                Type = ModuleType.HabCore, Name = "Hab Core", Buildable = false, Priority = 0,
                Description = "The landed base: crew quarters, fuel cells and life-support recyclers.",
                Housing = 6, EnergyOutput = 3, EnergyDraw = 2, Science = 1,
                Outputs = new[] { new Flow(Resource.Oxygen, 1), new Flow(Resource.Water, 3) },
            });
            Add(new ModuleDef
            {
                Type = ModuleType.IceMine, Name = "Ice Mine", MaterialCost = 30, BuildDays = 10, Jobs = 2, Priority = 1,
                Description = "Excavates and heats icy regolith. Output scales with the tile's ice.",
                EnergyDraw = 3, Outputs = new[] { new Flow(Resource.Water, 10) },
            });
            Add(new ModuleDef
            {
                Type = ModuleType.OxygenPlant, Name = "Oxygen Plant", MaterialCost = 35, BuildDays = 12, Jobs = 2, Priority = 2,
                Description = "Extracts oxygen from regolith, which is about 40% oxygen by mass.",
                EnergyDraw = 4, Outputs = new[] { new Flow(Resource.Oxygen, 8) },
            });
            Add(new ModuleDef
            {
                Type = ModuleType.Greenhouse, Name = "Greenhouse", MaterialCost = 30, BuildDays = 12, Jobs = 2, Priority = 3,
                Description = "Hydroponic farm under grow lights.",
                EnergyDraw = 3,
                Inputs = new[] { new Flow(Resource.Water, 2) },
                Outputs = new[] { new Flow(Resource.Food, 8) },
            });
            Add(new ModuleDef
            {
                Type = ModuleType.SolarArray, Name = "Solar Array", MaterialCost = 20, BuildDays = 6, Priority = 4,
                Description = "Cheap power, but only while the Sun is up.",
                EnergyOutput = 6, IsSolar = true,
            });
            Add(new ModuleDef
            {
                Type = ModuleType.FissionReactor, Name = "Fission Reactor", MaterialCost = 80, BuildDays = 25, Jobs = 1, Priority = 5,
                RequiredTech = TechId.KilopowerReactors,
                Description = "Compact reactor shipped from Earth. Steady power through the lunar night.",
                EnergyOutput = 12,
            });
            Add(new ModuleDef
            {
                Type = ModuleType.Foundry, Name = "Regolith Foundry", MaterialCost = 30, BuildDays = 10, Jobs = 2, Priority = 7,
                RequiredTech = TechId.RegolithSintering,
                Description = "Sinters and smelts regolith into building materials. Better on mineral-rich ground.",
                EnergyDraw = 3, Outputs = new[] { new Flow(Resource.Materials, 2) },
            });
            Add(new ModuleDef
            {
                Type = ModuleType.Habitat, Name = "Habitat", MaterialCost = 25, BuildDays = 10, Priority = 8,
                Description = "Regolith-shielded living space for more crew.",
                Housing = 6, EnergyDraw = 1,
            });
            Add(new ModuleDef
            {
                Type = ModuleType.Lab, Name = "Research Lab", MaterialCost = 30, BuildDays = 10, Jobs = 2, Priority = 6,
                Description = "Geology, life-support and engineering research. Produces Science.",
                EnergyDraw = 2, Science = 4,
            });
            Add(new ModuleDef
            {
                Type = ModuleType.BatteryBank, Name = "Battery Bank", MaterialCost = 30, BuildDays = 8, Priority = 9,
                RequiredTech = TechId.BatteryStorage,
                Description = "Charges from surplus daylight power and carries the colony through the night.",
                EnergyStorage = 60,
            });
            Add(new ModuleDef
            {
                Type = ModuleType.MineralExtractor, Name = "Mineral Extractor", MaterialCost = 40, BuildDays = 12, Jobs = 2, Priority = 10,
                RequiredTech = TechId.MineralBeneficiation,
                Description = "Mines and concentrates a strategic deposit (rare earths or platinum) for export to Earth.",
                EnergyDraw = 3, ExtractionRate = 0.5,
            });
        }

        static void Add(ModuleDef def) => Defs[def.Type] = def;

        public static ModuleDef Get(ModuleType type) => Defs[type];

        public static IEnumerable<ModuleDef> All => Defs.Values;

        /// <summary>Why this module can't go on this tile, or null if it can.</summary>
        public static string PlacementError(ModuleType type, Tile tile)
        {
            if (type == ModuleType.IceMine)
                return tile.Ice >= 0.4f ? null : "Not enough ice here";

            if (type == ModuleType.MineralExtractor)
                return DepositCatalog.Get(tile.Deposit)?.Kind == DepositKind.Strategic ? null : "Needs a strategic deposit (rare earths or platinum)";

            if (tile.Terrain == TerrainType.ShadowedCrater)
                return "Too cold and dark for anything but an ice mine";

            return null;
        }
    }
}
