using System;
using System.Collections.Generic;
using System.Linq;

namespace Waystation.Sim
{
    /// <summary>Tuning constants for the station economy and ship flow.</summary>
    public static class Rules
    {
        public const double StartingMoney = 1200;
        public const int PlotSize = 32;

        public const double ApproachSeconds = 4;
        public const double LandingSeconds = 4;
        public const double DepartingSeconds = 4;
        public const double RepairSeconds = 12;
        /// <summary>Fuel units per second a pad pumps into a ship.</summary>
        public const double PumpRate = 6;

        public const double FuelSellPrice = 3;
        public const double FuelBuyPrice = 1.8;
        /// <summary>A tanker is ordered when the tank falls below this fraction.</summary>
        public const double RestockThreshold = 0.4;
        public const double FuelDeliverySeconds = 15;

        /// <summary>Ships that can circle the station at once while hailing.</summary>
        public const int MaxHolding = 6;
        /// <summary>Reputation cost of waving a ship off (less than letting it give up).</summary>
        public const double ReputationLostWhenWavedOff = 1;
        public const double FirstArrivalSeconds = 5;
        public const double BaseArrivalSeconds = 16;

        public const double ReputationLostWhenAngry = 4;
        public const double DemolishRefund = 0.5;

        public static int Ticks(double seconds) => (int)Math.Round(seconds * Station.TicksPerSecond);
    }

    public interface ICommand
    {
        /// <summary>Why this can't be done right now, or null if it can.</summary>
        string Validate(Station station);

        void Apply(Station station);
    }

    public sealed class PlaceBuilding : ICommand
    {
        public readonly BuildingType Type;
        public readonly Cell Origin;

        public PlaceBuilding(BuildingType type, Cell origin)
        {
            Type = type;
            Origin = origin;
        }

        public string Validate(Station s)
        {
            var def = BuildingCatalog.Get(Type);
            if (!def.Buildable) return $"{def.Name} can't be built";

            for (int x = 0; x < def.Width; x++)
            for (int y = 0; y < def.Height; y++)
            {
                var c = new Cell(Origin.X + x, Origin.Y + y);
                if (!s.InBounds(c)) return "Outside the build area";
                if (s.BuildingAt(c) != null) return "Space is occupied";
            }

            if (s.Money < def.Cost) return $"Needs {def.Cost:0} credits";
            return null;
        }

        public void Apply(Station s)
        {
            var def = BuildingCatalog.Get(Type);
            s.Spend(def.Cost, LedgerLine.Construction);
            s.Buildings.Add(new Building { Id = s.NextBuildingId++, Type = Type, Origin = Origin });
            s.RecomputeConnections();
            s.Revision++;
            if (!def.IsPaintable) s.Report($"{def.Name} built.", EventKind.Construction);
        }
    }

    public sealed class Demolish : ICommand
    {
        public readonly int BuildingId;

        public Demolish(int buildingId)
        {
            BuildingId = buildingId;
        }

        public string Validate(Station s)
        {
            var b = s.GetBuilding(BuildingId);
            if (b == null) return "Nothing to demolish";
            if (b.Def.IsHub) return "The Waystation Pad is the heart of the station";
            if (b.ShipId != null) return "A ship is using it";
            return null;
        }

        public void Apply(Station s)
        {
            var b = s.GetBuilding(BuildingId);
            s.Buildings.Remove(b);
            s.Earn(b.Def.Cost * Rules.DemolishRefund, LedgerLine.Refunds);
            s.RecomputeConnections();
            s.Revision++;
        }
    }

    /// <summary>The player clears a hailing ship to land on a specific pad or hangar.</summary>
    public sealed class AssignLanding : ICommand
    {
        public readonly int ShipId;
        public readonly int BuildingId;

        public AssignLanding(int shipId, int buildingId)
        {
            ShipId = shipId;
            BuildingId = buildingId;
        }

        public string Validate(Station s) => s.LandingError(s.GetShip(ShipId), s.GetBuilding(BuildingId));

        public void Apply(Station s)
        {
            var ship = s.GetShip(ShipId);
            var b = s.GetBuilding(BuildingId);
            Simulation.ClearToLand(ship, b);
            s.HailsAnswered++;
            s.HailWaitTicks += s.Tick - ship.HailedTick;
            s.Report($"{ship.Name} cleared to land on {s.Label(b)}.", EventKind.Info, b.Id);
        }
    }

    /// <summary>Turn a hailing ship away: a small reputation hit, but better than letting it give up.</summary>
    public sealed class WaveOff : ICommand
    {
        public readonly int ShipId;

        public WaveOff(int shipId)
        {
            ShipId = shipId;
        }

        public string Validate(Station s) =>
            s.GetShip(ShipId)?.State == ShipState.Hailing ? null : "That ship isn't waiting for clearance";

        public void Apply(Station s)
        {
            var ship = s.GetShip(ShipId);
            s.Reputation -= Rules.ReputationLostWhenWavedOff;
            s.ShipsDiverted++;
            s.Report($"{ship.Name} waved off. Reputation −{Rules.ReputationLostWhenWavedOff:0}.", EventKind.Warning);
            ship.Enter(ShipState.Departing, Rules.Ticks(Rules.DepartingSeconds));
        }
    }

    public sealed class Simulation
    {
        readonly List<(long Tick, ICommand Command)> _commandLog = new List<(long, ICommand)>();

        public Station Station { get; }
        public IReadOnlyList<(long Tick, ICommand Command)> CommandLog => _commandLog;

        public Simulation(Station station)
        {
            Station = station;
        }

        public bool Execute(ICommand command, out string error)
        {
            error = command.Validate(Station);
            if (error != null) return false;
            command.Apply(Station);
            _commandLog.Add((Station.Tick, command));
            return true;
        }

        public void Advance(double seconds)
        {
            int ticks = Rules.Ticks(seconds);
            for (int i = 0; i < ticks; i++) Step();
        }

        /// <summary>One simulation tick (1/10 of a game second).</summary>
        public void Step()
        {
            var s = Station;
            s.Tick++;

            Traffic(s);
            foreach (var ship in s.Ships.ToList()) UpdateShip(s, ship);
            Dock(s);
            s.Ships.RemoveAll(x => x.State == ShipState.Gone);
            Fuel(s);
            if (s.Tick % (60 * Station.TicksPerSecond) == 0) Upkeep(s);

            s.Reputation = Math.Max(0, Math.Min(100, s.Reputation));
        }

        static void Traffic(Station s)
        {
            if (s.Tick < s.NextArrivalTick) return;

            double interval = Rules.BaseArrivalSeconds * (1.6 - s.Reputation / 100.0) * (0.6 + 0.8 * s.Rng.NextDouble());
            s.NextArrivalTick = s.Tick + Math.Max(1, Rules.Ticks(interval));

            bool hangar = s.Buildings.Any(b => b.Connected && b.Def.CanServe(ShipClass.Hauler));
            var cls = hangar && s.Rng.NextDouble() < 0.15 + s.Reputation / 500.0 ? ShipClass.Hauler : ShipClass.Shuttle;
            var def = ShipCatalog.Get(cls);
            var name = ShipCatalog.RandomName(s.Rng);

            int waiting = s.Ships.Count(x => x.State == ShipState.Hailing || x.State == ShipState.Approaching);
            if (waiting >= Rules.MaxHolding)
            {
                s.ShipsDiverted++;
                s.Report($"{name} diverted: the holding pattern is full.", EventKind.Warning);
                return;
            }

            var ship = new Ship
            {
                Id = s.NextShipId++,
                Name = name,
                Class = cls,
                Livery = s.Rng.Next(6),
                FuelNeeded = Math.Round(def.MinFuel + (def.MaxFuel - def.MinFuel) * s.Rng.NextDouble()),
                NeedsRepair = s.Rng.NextDouble() < def.RepairChance,
                MaxPatienceTicks = Rules.Ticks(def.PatienceSeconds),
                ArrivedTick = s.Tick,
            };
            ship.PatienceTicks = ship.MaxPatienceTicks;
            ship.Enter(ShipState.Approaching, Rules.Ticks(Rules.ApproachSeconds));
            s.Ships.Add(ship);
        }

        static void UpdateShip(Station s, Ship ship)
        {
            switch (ship.State)
            {
                case ShipState.Approaching:
                    if (--ship.StateTicks <= 0)
                    {
                        ship.Enter(ShipState.Hailing, 0);
                        ship.HailedTick = s.Tick;
                        var def = ShipCatalog.Get(ship.Class);
                        s.Report($"{ship.Name} ({def.Name.ToLowerInvariant()}, {ship.FuelNeeded:0} fuel{(ship.NeedsRepair ? ", needs repairs" : "")}) is hailing for a landing spot.", EventKind.Hail);
                    }
                    break;

                case ShipState.Hailing:
                    if (--ship.PatienceTicks <= 0) LeaveAngry(s, ship, "gave up waiting for clearance");
                    break;

                case ShipState.Landing:
                    if (--ship.StateTicks <= 0) ship.Enter(ShipState.Servicing, 0);
                    break;

                case ShipState.Servicing:
                    Service(s, ship);
                    break;

                case ShipState.Departing:
                    if (--ship.StateTicks <= 0)
                    {
                        ship.State = ShipState.Gone;
                        var b = ship.BuildingId.HasValue ? s.GetBuilding(ship.BuildingId.Value) : null;
                        if (b != null && b.ShipId == ship.Id) b.ShipId = null;
                    }
                    break;
            }
        }

        static void Service(Station s, Ship ship)
        {
            var building = s.GetBuilding(ship.BuildingId ?? -1);

            // 1. Refuel from the station tank.
            if (ship.FuelLoaded < ship.FuelNeeded)
            {
                double pump = Math.Min(Rules.PumpRate / Station.TicksPerSecond, Math.Min(ship.FuelNeeded - ship.FuelLoaded, s.FuelStock));
                if (pump <= 1e-9)
                {
                    // Dry tank: the ship sits and fumes.
                    if (--ship.PatienceTicks <= 0) LeaveAngry(s, ship, "left with an empty tank: the station ran out of fuel");
                    return;
                }
                ship.FuelLoaded += pump;
                s.FuelStock -= pump;
                return;
            }

            // 2. Repairs, if the ship needs them and this bay has a crew.
            if (ship.NeedsRepair && !ship.Repaired && building != null && building.Def.Repairs)
            {
                if (ship.StateDuration == 0) ship.Enter(ShipState.Servicing, Rules.Ticks(Rules.RepairSeconds));
                if (--ship.StateTicks > 0) return;
                ship.Repaired = true;
            }

            // 3. Pay and go.
            var def = ShipCatalog.Get(ship.Class);
            double fuel = ship.FuelLoaded * Rules.FuelSellPrice;
            double repairs = ship.Repaired ? def.RepairFee : 0;
            double bill = def.LandingFee + fuel + repairs;
            s.Earn(def.LandingFee, LedgerLine.LandingFees, building);
            s.Earn(fuel, LedgerLine.FuelSales, building);
            if (repairs > 0) s.Earn(repairs, LedgerLine.Repairs, building);
            s.ShipsServed++;
            s.Reputation += def.ReputationOnServed;
            if (building != null) building.ShipsServed++;
            s.Report($"{ship.Name} paid {bill:0} cr{(ship.Repaired ? " (refuel + repairs)" : "")}.", EventKind.Revenue, building?.Id, bill);
            ship.Enter(ShipState.Departing, Rules.Ticks(Rules.DepartingSeconds));
        }

        static void LeaveAngry(Station s, Ship ship, string why)
        {
            ship.Angry = true;
            s.ShipsLostAngry++;
            s.Reputation -= Rules.ReputationLostWhenAngry;
            s.Report($"{ship.Name} {why}. Reputation −{Rules.ReputationLostWhenAngry:0}.", EventKind.Alert, ship.BuildingId);
            ship.Enter(ShipState.Departing, Rules.Ticks(Rules.DepartingSeconds));
        }

        /// <summary>Automatic clearance: only once the station has air traffic control.</summary>
        static void Dock(Station s)
        {
            if (!s.AutoAssign) return;
            foreach (var ship in s.Hailing.ToList())
            {
                var free = s.Buildings
                    .Where(b => b.Connected && b.ShipId == null && b.Def.CanServe(ship.Class))
                    // Shuttles prefer plain pads, keeping hangars free for haulers.
                    .OrderBy(b => b.Def.Repairs && ship.Class == ShipClass.Shuttle ? 1 : 0)
                    .ThenBy(b => b.Id)
                    .FirstOrDefault();
                if (free == null) continue;

                ClearToLand(ship, free);
            }
        }

        public static void ClearToLand(Ship ship, Building b)
        {
            b.ShipId = ship.Id;
            ship.BuildingId = b.Id;
            ship.Enter(ShipState.Landing, Rules.Ticks(Rules.LandingSeconds));
        }

        static void Fuel(Station s)
        {
            if (s.FuelDeliveryTick >= 0)
            {
                if (s.Tick < s.FuelDeliveryTick) return;
                s.FuelStock = Math.Min(s.FuelCapacity, s.FuelStock + s.FuelDeliveryAmount);
                s.FuelDeliveryTick = -1;
                s.Report($"Fuel tanker delivered {s.FuelDeliveryAmount:0} units.");
                return;
            }

            double capacity = s.FuelCapacity;
            if (capacity <= 0 || s.FuelStock >= capacity * Rules.RestockThreshold) return;

            double amount = Math.Floor(Math.Min(capacity - s.FuelStock, Math.Max(0, s.Money) / Rules.FuelBuyPrice));
            if (amount < 10) return; // can't afford a useful load

            s.Spend(amount * Rules.FuelBuyPrice, LedgerLine.FuelPurchases);
            s.FuelDeliveryAmount = amount;
            s.FuelDeliveryTick = s.Tick + Rules.Ticks(Rules.FuelDeliverySeconds);
            s.Report($"Fuel running low: tanker ordered ({amount:0} units, {amount * Rules.FuelBuyPrice:0} cr).", EventKind.Warning);
        }

        static void Upkeep(Station s)
        {
            double upkeep = s.Buildings.Sum(b => b.Def.UpkeepPerMinute);
            s.Spend(upkeep, LedgerLine.Upkeep);
        }
    }

    public static class GameSetup
    {
        public static Simulation NewStation(int seed)
        {
            var s = new Station(Rules.PlotSize, Rules.PlotSize, seed) { Money = Rules.StartingMoney };
            int mid = Rules.PlotSize / 2 - 1;
            s.Buildings.Add(new Building { Id = s.NextBuildingId++, Type = BuildingType.StarterPad, Origin = new Cell(mid - 1, mid - 1) });
            s.FuelStock = s.FuelCapacity;
            s.NextArrivalTick = Rules.Ticks(Rules.FirstArrivalSeconds);
            s.RecomputeConnections();
            s.Report("Your waystation is open: one pad and one fuel tank, floating in a lot of empty space.");
            return new Simulation(s);
        }
    }
}
