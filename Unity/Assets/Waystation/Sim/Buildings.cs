using System;
using System.Collections.Generic;

namespace Waystation.Sim
{
    /// <summary>
    /// A square on the station's build grid (a flat lattice in space). X runs east, Y runs north.
    /// One cell is 2 × 2 metres: a walkway is one cell wide, a landing pad 8 × 8.
    /// </summary>
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X;
        public readonly int Y;

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>North, east, south, west: the four directions walkways connect in.</summary>
        public static readonly Cell[] Directions = { new Cell(0, 1), new Cell(1, 0), new Cell(0, -1), new Cell(-1, 0) };

        public static Cell operator +(Cell a, Cell b) => new Cell(a.X + b.X, a.Y + b.Y);
        public static bool operator ==(Cell a, Cell b) => a.Equals(b);
        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);

        public IEnumerable<Cell> Neighbors()
        {
            foreach (var d in Directions) yield return this + d;
        }

        public bool Equals(Cell o) => X == o.X && Y == o.Y;
        public override bool Equals(object obj) => obj is Cell o && Equals(o);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => $"({X}, {Y})";
    }

    public enum ShipClass
    {
        /// <summary>Small personal craft. Lands on any pad.</summary>
        Shuttle,
        /// <summary>Bulky freighter. Needs a hangar, and often needs repairs.</summary>
        Hauler,
    }

    public enum BuildingType
    {
        /// <summary>The landing platform you start with; it also holds the station's fuel tank.</summary>
        StarterPad,
        LandingPad,
        Hangar,
        /// <summary>Open-air grated catwalk with railings; links modules to the station.</summary>
        Walkway,
        /// <summary>Structural deck plate; modules are built on top of these.</summary>
        Foundation,

        // Modules: built on foundations.
        SolarArray,
        BatteryBank,
        FuelCellGenerator,
        FuelTank,
        IceRefinery,
        PilotLounge,
        CommsArray,
        AtcTower,

        // Service buildings (on foundations): what ships' crews and passengers come for.
        Cantina,
        ObservationDeck,
        TradingOutpost,
        Bazaar,
        PartsDepot,
        MilitaryBarracks,
        ExplorersGuild,
        CorporateOffice,
    }

    /// <summary>Build-menu tabs. New categories (services, utilities…) slot in here.</summary>
    public enum BuildCategory
    {
        Landing,
        Structure,
        Power,
        Fuel,
        /// <summary>Running the station: comms and traffic control.</summary>
        Operations,
        /// <summary>Official business: the military, the Explorers' Guild, corporations.</summary>
        Business,
        /// <summary>Rest, food and fun.</summary>
        Leisure,
        /// <summary>Trade and shopping.</summary>
        Commerce,
    }

    public sealed class BuildingDef
    {
        public BuildingType Type;
        public string Name;
        public string Description;
        public BuildCategory Category;
        public int Width;
        public int Height;
        public double Cost;
        public double UpkeepPerMinute;
        public bool Buildable = true;
        /// <summary>The root of the station network: fuel and power flow out from here.</summary>
        public bool IsHub;
        /// <summary>1×1 network piece (walkway or foundation) that can be painted by dragging.</summary>
        public bool IsPaintable;
        /// <summary>Short label prefix for the map and the traffic board, e.g. "Pad" → "Pad 2".</summary>
        public string Label;
        public ShipClass[] Serves = new ShipClass[0];
        public bool Repairs;
        public double FuelCapacity;

        /// <summary>A floor (foundation) that modules stand on; it shares its cells with the module on top.</summary>
        public bool IsFloor;
        /// <summary>Must be placed entirely on foundations.</summary>
        public bool RequiresFoundation;

        // Power, in kW (energy in kJ, i.e. kW·s).
        /// <summary>Constant draw while connected and switched on.</summary>
        public double PowerUse;
        /// <summary>Constant output, sun or shadow (the hub's radioisotope generator).</summary>
        public double PowerOutput;
        /// <summary>Output in full sunlight; scales with sun exposure and drops to zero in the planet's shadow.</summary>
        public double SolarOutput;
        /// <summary>Backup output while running; burns <see cref="FuelBurn"/> station fuel per second.</summary>
        public double GeneratorOutput;
        public double FuelBurn;
        public double BatteryCapacity;

        /// <summary>Fuel made per second while running (refineries).</summary>
        public double RefineRate;
        /// <summary>The service people come here for (service buildings).</summary>
        public ServiceType? Provides;
        /// <summary>Visitors inside at once; more queue at the door.</summary>
        public int Capacity;
        public double VisitSeconds;
        /// <summary>What each visitor pays.</summary>
        public double VisitFee;
        /// <summary>Boosts traffic and the holding pattern (comms arrays).</summary>
        public bool Comms;
        /// <summary>Clears hailing ships to free pads automatically (air traffic control).</summary>
        public bool Atc;

        public bool CanServe(ShipClass c) => Array.IndexOf(Serves, c) >= 0;

        /// <summary>Can the player switch it off (to save power)?</summary>
        public bool CanToggle => !IsHub && (PowerUse > 0 || SolarOutput > 0 || GeneratorOutput > 0);
    }

    public static class BuildingCatalog
    {
        static readonly Dictionary<BuildingType, BuildingDef> Defs = new Dictionary<BuildingType, BuildingDef>();

        static BuildingCatalog()
        {
            Add(new BuildingDef
            {
                Type = BuildingType.StarterPad, Name = "Waystation Pad", Width = 8, Height = 8,
                Description = "Where it all started: a landing platform with the station's fuel tank and comms mast.",
                Label = "Pad", Buildable = false, IsHub = true, UpkeepPerMinute = 5, FuelCapacity = 800,
                Serves = new[] { ShipClass.Shuttle },
                // A radioisotope generator and a small battery keep the lights on from day one.
                PowerUse = 2, PowerOutput = 8, BatteryCapacity = 400,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.LandingPad, Name = "Landing Pad", Width = 8, Height = 8, Cost = 900,
                Description = "Another place for shuttles to set down and refuel. Must be connected to the station.",
                Label = "Pad", Category = BuildCategory.Landing,
                UpkeepPerMinute = 8, Serves = new[] { ShipClass.Shuttle }, PowerUse = 2,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.Hangar, Name = "Hangar", Width = 10, Height = 10, Cost = 2600,
                Description = "Open repair bay with a gantry crane. Takes haulers (and shuttles). Must be connected to the station.",
                Label = "Hangar", Category = BuildCategory.Landing,
                UpkeepPerMinute = 25, Repairs = true, Serves = new[] { ShipClass.Hauler, ShipClass.Shuttle }, PowerUse = 6,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.Walkway, Name = "Walkway", Width = 1, Height = 1, Cost = 10,
                Description = "Open-air grated catwalk with railings and fuel lines underneath. Links modules to the station.",
                UpkeepPerMinute = 0.1, IsPaintable = true, Category = BuildCategory.Structure,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.Foundation, Name = "Foundation", Width = 1, Height = 1, Cost = 12,
                Description = "Heavy deck plate. Extends the station, and every module (power, fuel, services) is built on top of foundations.",
                UpkeepPerMinute = 0.08, IsPaintable = true, IsFloor = true, Category = BuildCategory.Structure,
            });

            // ---- Modules (on foundations)
            Add(new BuildingDef
            {
                Type = BuildingType.SolarArray, Name = "Solar Array", Width = 5, Height = 5, Cost = 450,
                Description = "Sun-tracking photovoltaic wings. Free power in sunlight, nothing at all in the planet's shadow.",
                Category = BuildCategory.Power, RequiresFoundation = true, UpkeepPerMinute = 1, SolarOutput = 10,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.BatteryBank, Name = "Battery Bank", Width = 3, Height = 5, Cost = 350,
                Description = "Stores surplus power by day to carry the station through the eclipse.",
                Category = BuildCategory.Power, RequiresFoundation = true, UpkeepPerMinute = 1, BatteryCapacity = 900,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.FuelCellGenerator, Name = "Fuel Cell Generator", Width = 3, Height = 5, Cost = 700,
                Description = "Backup power. Starts by itself when the batteries run low, and burns station fuel while it runs.",
                Category = BuildCategory.Power, RequiresFoundation = true, UpkeepPerMinute = 4, GeneratorOutput = 12, FuelBurn = 0.3,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.FuelTank, Name = "Fuel Tank", Width = 5, Height = 5, Cost = 500,
                Description = "Pressurised propellant sphere. More fuel on hand, fewer tanker runs.",
                Category = BuildCategory.Fuel, RequiresFoundation = true, UpkeepPerMinute = 2, FuelCapacity = 600,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.IceRefinery, Name = "Ice Refinery", Width = 5, Height = 8, Cost = 2200,
                Description = "Cracks tethered comet ice into propellant. Power-hungry: it only runs on spare power or a healthy battery.",
                Category = BuildCategory.Fuel, RequiresFoundation = true, UpkeepPerMinute = 18, PowerUse = 14, RefineRate = 1,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.PilotLounge, Name = "Pilot Lounge", Width = 5, Height = 5, Cost = 1000,
                Description = "Bunks, showers and bad coffee. Somewhere to rest between jumps.",
                Category = BuildCategory.Leisure, RequiresFoundation = true, UpkeepPerMinute = 8, PowerUse = 3,
                Provides = ServiceType.Rest, Capacity = 4, VisitSeconds = 14, VisitFee = 18,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.CommsArray, Name = "Comms Array", Width = 5, Height = 5, Cost = 1800,
                Description = "Long-range dish broadcasting the station's beacon. More traffic, and room for more ships in the holding pattern.",
                Category = BuildCategory.Operations, RequiresFoundation = true, UpkeepPerMinute = 12, PowerUse = 4, Comms = true,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.AtcTower, Name = "Traffic Control", Width = 5, Height = 5, Cost = 4000,
                Description = "Air traffic control tower. Clears hailing ships to free pads automatically while it has power.",
                Category = BuildCategory.Operations, RequiresFoundation = true, UpkeepPerMinute = 30, PowerUse = 5, Atc = true,
            });

            // ---- Leisure
            Add(new BuildingDef
            {
                Type = BuildingType.Cantina, Name = "Cantina", Width = 5, Height = 5, Cost = 900,
                Description = "Noodles, rotgut and a jukebox nobody can turn off.",
                Category = BuildCategory.Leisure, RequiresFoundation = true, UpkeepPerMinute = 7, PowerUse = 3,
                Provides = ServiceType.Food, Capacity = 4, VisitSeconds = 10, VisitFee = 22,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.ObservationDeck, Name = "Observation Deck", Width = 8, Height = 5, Cost = 1300,
                Description = "A long armoured window onto the planet below. Travellers pay to watch the sunrise.",
                Category = BuildCategory.Leisure, RequiresFoundation = true, UpkeepPerMinute = 6, PowerUse = 2,
                Provides = ServiceType.Sightseeing, Capacity = 6, VisitSeconds = 12, VisitFee = 15,
            });

            // ---- Commerce
            Add(new BuildingDef
            {
                Type = BuildingType.TradingOutpost, Name = "Trading Outpost", Width = 8, Height = 8, Cost = 2200,
                Description = "Cargo brokers and a scales-and-scanners customs floor. Freighters and merchants need one; the station takes a cut.",
                Category = BuildCategory.Commerce, RequiresFoundation = true, UpkeepPerMinute = 14, PowerUse = 4,
                Provides = ServiceType.Trade, Capacity = 3, VisitSeconds = 14, VisitFee = 70,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.Bazaar, Name = "Bazaar", Width = 5, Height = 5, Cost = 1100,
                Description = "Stalls selling rations, trinkets and things that fell off a freighter.",
                Category = BuildCategory.Commerce, RequiresFoundation = true, UpkeepPerMinute = 7, PowerUse = 2,
                Provides = ServiceType.Shopping, Capacity = 5, VisitSeconds = 10, VisitFee = 25,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.PartsDepot, Name = "Parts Depot", Width = 5, Height = 5, Cost = 1400,
                Description = "Salvaged and new ship parts. Explorers and damaged freighters stock up here.",
                Category = BuildCategory.Commerce, RequiresFoundation = true, UpkeepPerMinute = 9, PowerUse = 4,
                Provides = ServiceType.Parts, Capacity = 2, VisitSeconds = 8, VisitFee = 60,
            });

            // ---- Business
            Add(new BuildingDef
            {
                Type = BuildingType.MilitaryBarracks, Name = "USM Barracks", Width = 8, Height = 5, Cost = 2600,
                Description = "A United Systems of Mankind outpost. Brings USM patrols to the station; the USM pays a stipend for every marine who reports in.",
                Category = BuildCategory.Business, RequiresFoundation = true, UpkeepPerMinute = 10, PowerUse = 5,
                Provides = ServiceType.Military, Capacity = 4, VisitSeconds = 16, VisitFee = 45,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.ExplorersGuild, Name = "Explorers' Guild", Width = 5, Height = 5, Cost = 2000,
                Description = "A chapter house of the Explorers' Guild. Brings guild surveyors, who pay to file their charts.",
                Category = BuildCategory.Business, RequiresFoundation = true, UpkeepPerMinute = 10, PowerUse = 3,
                Provides = ServiceType.Survey, Capacity = 3, VisitSeconds = 12, VisitFee = 55,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.CorporateOffice, Name = "Corporate Office", Width = 5, Height = 8, Cost = 2400,
                Description = "Glass, carpet and a receptionist. Brings executive shuttles, who pay well and expect a lot.",
                Category = BuildCategory.Business, RequiresFoundation = true, UpkeepPerMinute = 12, PowerUse = 4,
                Provides = ServiceType.Corporate, Capacity = 2, VisitSeconds = 14, VisitFee = 90,
            });
        }

        static void Add(BuildingDef def) => Defs[def.Type] = def;

        public static BuildingDef Get(BuildingType type) => Defs[type];
        public static IEnumerable<BuildingDef> All => Defs.Values;
    }

    public sealed class Building
    {
        public int Id;
        public BuildingType Type;
        /// <summary>South-west corner of the footprint.</summary>
        public Cell Origin;
        /// <summary>Linked to the hub through corridors or touching buildings. Unconnected buildings can't operate.</summary>
        public bool Connected;
        /// <summary>The ship currently using this building, if any.</summary>
        public int? ShipId;
        public double Earned;
        public int ShipsServed;
        /// <summary>Visitors served (service buildings).</summary>
        public int Visits;
        /// <summary>Switched on by the player (see <see cref="BuildingDef.CanToggle"/>).</summary>
        public bool Enabled = true;
        /// <summary>Doing its job this tick: a generator burning fuel, a refinery cracking ice.</summary>
        public bool Running;

        public BuildingDef Def => BuildingCatalog.Get(Type);

        /// <summary>Connected and switched on.</summary>
        public bool Operating => Connected && Enabled;

        public bool Contains(Cell c) =>
            c.X >= Origin.X && c.X < Origin.X + Def.Width && c.Y >= Origin.Y && c.Y < Origin.Y + Def.Height;

        public IEnumerable<Cell> Footprint()
        {
            for (int x = 0; x < Def.Width; x++)
            for (int y = 0; y < Def.Height; y++)
                yield return new Cell(Origin.X + x, Origin.Y + y);
        }

        /// <summary>Footprint centre in cell units (e.g. a 3×3 at origin 0,0 is centred on 1.5, 1.5).</summary>
        public (double x, double y) Center => (Origin.X + Def.Width / 2.0, Origin.Y + Def.Height / 2.0);
    }
}
