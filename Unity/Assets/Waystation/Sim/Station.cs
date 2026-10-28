using System;
using System.Collections.Generic;
using System.Linq;

namespace Waystation.Sim
{
    public enum EventKind
    {
        Info,
        /// <summary>A ship is calling for clearance.</summary>
        Hail,
        /// <summary>Money came in (carries the building, so the view can float the amount over it).</summary>
        Revenue,
        Construction,
        /// <summary>Something needs attention soon (low fuel, diversions, waving off).</summary>
        Warning,
        /// <summary>Something went wrong (a ship left angry, a power failure).</summary>
        Alert,
        /// <summary>Orbital sunrise and sunset.</summary>
        Orbit,
    }

    public readonly struct StationEvent
    {
        public readonly long Tick;
        public readonly string Text;
        public readonly EventKind Kind;
        public readonly int? BuildingId;
        public readonly double Amount;

        public StationEvent(long tick, string text, EventKind kind = EventKind.Info, int? buildingId = null, double amount = 0)
        {
            Tick = tick;
            Text = text;
            Kind = kind;
            BuildingId = buildingId;
            Amount = amount;
        }
    }

    /// <summary>All simulation state for one spaceport.</summary>
    public sealed class Station
    {
        public const int TicksPerSecond = 10;

        public readonly int Width;
        public readonly int Height;
        public readonly int Seed;
        public readonly Random Rng;

        public long Tick;
        public double Money;
        /// <summary>0–100. Drives how much traffic comes your way.</summary>
        public double Reputation = 50;
        public double FuelStock;

        public readonly List<Building> Buildings = new List<Building>();
        public readonly List<Ship> Ships = new List<Ship>();
        public readonly List<StationEvent> Log = new List<StationEvent>();

        public int NextBuildingId = 1;
        public int NextShipId = 1;
        /// <summary>Bumped when buildings change, so views know to rebuild.</summary>
        public int Revision;

        // Traffic and logistics bookkeeping.
        public long NextArrivalTick;
        public long FuelDeliveryTick = -1;
        public double FuelDeliveryAmount;

        // Lifetime stats.
        public int ShipsServed;
        public int ShipsLostAngry;
        public int ShipsDiverted;
        public double Revenue;
        public double Expenses;
        public readonly Ledger Ledger = new Ledger();
        /// <summary>Hails answered (cleared to land) and how long they waited in total, for the stats.</summary>
        public int HailsAnswered;
        public long HailWaitTicks;

        public double AverageHailWaitSeconds => HailsAnswered == 0 ? 0 : HailWaitTicks / (double)HailsAnswered / TicksPerSecond;

        // Power grid, recomputed every tick (kW; battery in kJ).
        public double SolarPower;
        public double PowerSupply;
        public double PowerDemand;
        public double BatteryCharge;
        public double BatteryCapacity;
        /// <summary>Share of demand actually met: 1 normally, less during a brownout.</summary>
        public double PowerSatisfaction = 1;
        public bool GeneratorsOn;
        /// <summary>Accumulates satisfaction so time-based work (repairs) slows down in proportion during brownouts.</summary>
        public double PowerCredit;
        public bool Brownout;
        public long LastPowerReportTick = -100000;
        public bool BatteryWarned;
        public bool WasDay = true;

        /// <summary>Lounges, comms and traffic control need at least half power to work.</summary>
        public bool SystemsOnline => PowerSatisfaction >= 0.5;
        public double NetPower => PowerSupply - PowerDemand;
        public double BatteryFraction => BatteryCapacity > 0 ? BatteryCharge / BatteryCapacity : 0;
        public double SunExposure => Orbit.SunExposure(Tick);
        /// <summary>A traffic control tower is connected, switched on and powered.</summary>
        public bool AtcOnline => SystemsOnline && Buildings.Any(b => b.Def.Atc && b.Operating);
        /// <summary>Hailing ships are cleared automatically (ATC, or <see cref="AutoAssign"/> in tests and tools).</summary>
        public bool AutoClearance => AutoAssign || AtcOnline;
        public int CommsArraysOnline => SystemsOnline ? Math.Min(3, Buildings.Count(b => b.Def.Comms && b.Operating)) : 0;
        /// <summary>Service types on offer right now (connected, switched on, powered).</summary>
        public IEnumerable<ServiceType> ServicesOffered => SystemsOnline
            ? Buildings.Where(b => b.Operating && b.Def.Provides != null).Select(b => b.Def.Provides.Value).Distinct()
            : Enumerable.Empty<ServiceType>();

        // People out on the station.
        public readonly List<Person> People = new List<Person>();
        public int NextPersonId = 1;
        public int ShipsDisappointed;

        readonly HashSet<Cell> _walkable = new HashSet<Cell>();
        int _walkRevision = -1;

        /// <summary>
        /// Can people walk on this cell? Walkways, bare foundations, pads and hangars yes; modules no
        /// (people walk up to them).
        /// </summary>
        public bool IsWalkable(Cell c)
        {
            if (_walkRevision != Revision)
            {
                _walkRevision = Revision;
                _walkable.Clear();
                foreach (var b in Buildings)
                {
                    bool deck = b.Type == BuildingType.Walkway || b.Def.Serves.Length > 0 || (b.Def.IsFloor && ModuleAt(b.Origin) == null);
                    if (!deck) continue;
                    foreach (var cell in b.Footprint()) _walkable.Add(cell);
                }
            }
            return _walkable.Contains(c);
        }

        public Station(int width, int height, int seed)
        {
            Width = width;
            Height = height;
            Seed = seed;
            Rng = new Random(seed);
        }

        public double Seconds => Tick / (double)TicksPerSecond;
        public double FuelCapacity => Buildings.Where(b => b.Connected || b.Def.IsHub).Sum(b => b.Def.FuelCapacity);
        public Building Hub => Buildings.FirstOrDefault(b => b.Def.IsHub);

        public bool InBounds(Cell c) => c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height;
        /// <summary>The topmost building on a cell: a module rather than the foundation under it.</summary>
        public Building BuildingAt(Cell c) => Buildings.FirstOrDefault(b => !b.Def.IsFloor && b.Contains(c)) ?? FloorAt(c);
        public Building FloorAt(Cell c) => Buildings.FirstOrDefault(b => b.Def.IsFloor && b.Contains(c));
        /// <summary>The module standing on a foundation cell, if any.</summary>
        public Building ModuleAt(Cell c) => Buildings.FirstOrDefault(b => !b.Def.IsFloor && b.Contains(c));
        public Building GetBuilding(int id) => Buildings.FirstOrDefault(b => b.Id == id);
        public Ship GetShip(int id) => Ships.FirstOrDefault(s => s.Id == id);

        /// <summary>Ships calling in for a landing spot, oldest first.</summary>
        public IEnumerable<Ship> Hailing => Ships.Where(s => s.State == ShipState.Hailing).OrderBy(s => s.ArrivedTick).ThenBy(s => s.Id);

        /// <summary>
        /// Forces automatic clearance (tests and tools). In play, clearance is automatic only while a
        /// traffic control tower is online (<see cref="AtcOnline"/>); until then the player assigns every landing.
        /// </summary>
        public bool AutoAssign;

        /// <summary>"Pad 2", "Hangar 1": numbered per label in build order.</summary>
        public string Label(Building b)
        {
            if (b.Def.Label == null) return b.Def.Name;
            int n = Buildings.Where(x => x.Def.Label == b.Def.Label).OrderBy(x => x.Id).ToList().IndexOf(b) + 1;
            return $"{b.Def.Label} {n}";
        }

        /// <summary>Why this ship can't be cleared to this building right now, or null if it can.</summary>
        public string LandingError(Ship ship, Building b)
        {
            if (ship == null || ship.State != ShipState.Hailing) return "That ship isn't waiting for clearance";
            if (b == null || b.Def.Serves.Length == 0) return "Ships can't land there";
            if (!b.Def.CanServe(ship.Class)) return $"{b.Def.Name} can't take a {ship.Class.ToString().ToLowerInvariant()}";
            if (!b.Connected) return $"{Label(b)} isn't connected to the station";
            if (!b.Enabled) return $"{Label(b)} is switched off";
            if (b.ShipId != null) return $"{Label(b)} is taken";
            return null;
        }

        public void Report(string text, EventKind kind = EventKind.Info, int? buildingId = null, double amount = 0) =>
            Log.Add(new StationEvent(Tick, text, kind, buildingId, amount));

        public void Earn(double amount, LedgerLine line, Building building = null)
        {
            Money += amount;
            Revenue += amount;
            Ledger.Record(Tick, line, amount);
            if (building != null) building.Earned += amount;
        }

        public void Spend(double amount, LedgerLine line)
        {
            Money -= amount;
            Expenses += amount;
            Ledger.Record(Tick, line, amount);
        }

        /// <summary>
        /// Flood-fills from the hub through every occupied cell (walkways, foundations and modules alike).
        /// Anything reached is connected; anything else can't get fuel or power.
        /// </summary>
        public void RecomputeConnections()
        {
            foreach (var b in Buildings) b.Connected = false;
            var hub = Hub;
            if (hub == null) return;

            // A cell can hold a foundation and the module on top of it.
            var occupied = new Dictionary<Cell, List<Building>>();
            foreach (var b in Buildings)
            foreach (var c in b.Footprint())
            {
                if (!occupied.TryGetValue(c, out var list)) occupied[c] = list = new List<Building>(2);
                list.Add(b);
            }

            var seen = new HashSet<Cell>();
            var frontier = new Queue<Cell>(hub.Footprint());
            foreach (var c in hub.Footprint()) seen.Add(c);

            while (frontier.Count > 0)
            {
                var c = frontier.Dequeue();
                foreach (var b in occupied[c]) b.Connected = true;
                foreach (var n in c.Neighbors())
                    if (occupied.ContainsKey(n) && seen.Add(n))
                        frontier.Enqueue(n);
            }
        }

        /// <summary>Which of the four neighbours of a cell hold something a walkway should link to (N, E, S, W bit mask).</summary>
        public int LinkMask(Cell c)
        {
            int mask = 0;
            for (int i = 0; i < 4; i++)
                if (BuildingAt(c + Cell.Directions[i]) != null)
                    mask |= 1 << i;
            return mask;
        }
    }
}
