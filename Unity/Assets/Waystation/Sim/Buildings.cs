using System;
using System.Collections.Generic;

namespace Waystation.Sim
{
    /// <summary>A square on the station's build grid (a flat lattice in space). X runs east, Y runs north.</summary>
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
        /// <summary>Structural deck plate; future buildings will stand on these.</summary>
        Foundation,
    }

    /// <summary>Build-menu tabs. New categories (services, utilities…) slot in here.</summary>
    public enum BuildCategory
    {
        Landing,
        Structure,
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

        public bool CanServe(ShipClass c) => Array.IndexOf(Serves, c) >= 0;
    }

    public static class BuildingCatalog
    {
        static readonly Dictionary<BuildingType, BuildingDef> Defs = new Dictionary<BuildingType, BuildingDef>();

        static BuildingCatalog()
        {
            Add(new BuildingDef
            {
                Type = BuildingType.StarterPad, Name = "Waystation Pad", Width = 3, Height = 3,
                Description = "Where it all started: a landing platform with the station's fuel tank and comms mast.",
                Label = "Pad", Buildable = false, IsHub = true, UpkeepPerMinute = 5, FuelCapacity = 800,
                Serves = new[] { ShipClass.Shuttle },
            });
            Add(new BuildingDef
            {
                Type = BuildingType.LandingPad, Name = "Landing Pad", Width = 3, Height = 3, Cost = 900,
                Description = "Another place for shuttles to set down and refuel. Must be connected to the station.",
                Label = "Pad", Category = BuildCategory.Landing,
                UpkeepPerMinute = 8, Serves = new[] { ShipClass.Shuttle },
            });
            Add(new BuildingDef
            {
                Type = BuildingType.Hangar, Name = "Hangar", Width = 4, Height = 4, Cost = 2600,
                Description = "Open repair bay with a gantry crane. Takes haulers (and shuttles). Must be connected to the station.",
                Label = "Hangar", Category = BuildCategory.Landing,
                UpkeepPerMinute = 25, Repairs = true, Serves = new[] { ShipClass.Hauler, ShipClass.Shuttle },
            });
            Add(new BuildingDef
            {
                Type = BuildingType.Walkway, Name = "Walkway", Width = 1, Height = 1, Cost = 25,
                Description = "Open-air grated catwalk with railings and fuel lines underneath. Links modules to the station.",
                UpkeepPerMinute = 0.3, IsPaintable = true, Category = BuildCategory.Structure,
            });
            Add(new BuildingDef
            {
                Type = BuildingType.Foundation, Name = "Foundation", Width = 1, Height = 1, Cost = 60,
                Description = "Heavy deck plate. Extends the station; future buildings will be placed on foundations.",
                UpkeepPerMinute = 0.5, IsPaintable = true, Category = BuildCategory.Structure,
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

        public BuildingDef Def => BuildingCatalog.Get(Type);

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
