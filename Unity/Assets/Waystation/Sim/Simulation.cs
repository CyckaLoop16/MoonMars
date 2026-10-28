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

        /// <summary>Each comms array online shortens the gap between arrivals by this factor (up to three).</summary>
        public const double CommsTrafficFactor = 0.8;
        /// <summary>Extra holding-pattern slots per comms array online.</summary>
        public const int CommsHoldingSlots = 2;
        /// <summary>Reputation when a ship gets what it came for…</summary>
        public const double ReputationNeedMet = 0.4;
        /// <summary>…and when it leaves without it.</summary>
        public const double ReputationNeedFailed = 1.5;
        public const double ReputationPerWish = 0.2;
        /// <summary>Each kind of service on offer shortens the gap between arrivals by this factor.</summary>
        public const double ServiceTrafficFactor = 0.94;
        /// <summary>Generators start below this battery level (while the station is short of power)…</summary>
        public const double GeneratorStartCharge = 0.2;
        /// <summary>…and stop once the battery is back above this.</summary>
        public const double GeneratorStopCharge = 0.6;
        /// <summary>Refineries also run on battery power while it's above this level.</summary>
        public const double RefineryMinCharge = 0.35;
        public const double BatteryWarningCharge = 0.15;

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
                if (def.RequiresFoundation)
                {
                    if (s.ModuleAt(c) != null) return "Space is occupied";
                    if (s.FloorAt(c) == null) return $"{def.Name} must be built on foundations (F)";
                }
                else if (s.BuildingAt(c) != null)
                {
                    return "Space is occupied";
                }
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
            if (b.Def.IsFloor && s.ModuleAt(b.Origin) is Building top) return $"Remove the {top.Def.Name.ToLowerInvariant()} on top first";
            if (s.People.Any(p => p.BuildingId == b.Id && (p.State == PersonState.Visiting || p.State == PersonState.Queueing))) return "People are inside";
            return null;
        }

        public void Apply(Station s)
        {
            var b = s.GetBuilding(BuildingId);
            s.Buildings.Remove(b);
            s.Earn(b.Def.Cost * Rules.DemolishRefund, LedgerLine.Refunds);
            s.RecomputeConnections();
            s.FuelStock = Math.Min(s.FuelStock, s.FuelCapacity);
            s.Revision++;
        }
    }

    /// <summary>Switch a module on or off, e.g. to save power through the eclipse.</summary>
    public sealed class SetEnabled : ICommand
    {
        public readonly int BuildingId;
        public readonly bool Enabled;

        public SetEnabled(int buildingId, bool enabled)
        {
            BuildingId = buildingId;
            Enabled = enabled;
        }

        public string Validate(Station s)
        {
            var b = s.GetBuilding(BuildingId);
            if (b == null) return "Nothing there";
            if (!b.Def.CanToggle) return $"{b.Def.Name} has no power switch";
            if (!Enabled && b.ShipId != null) return "A ship is using it";
            return null;
        }

        public void Apply(Station s)
        {
            var b = s.GetBuilding(BuildingId);
            b.Enabled = Enabled;
            s.Revision++;
            s.Report($"{s.Label(b)} switched {(Enabled ? "on" : "off")}.", EventKind.Info, b.Id);
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

            Sun(s);
            Power(s);
            Traffic(s);
            foreach (var ship in s.Ships.ToList()) UpdateShip(s, ship);
            Crew.Step(s);
            Dock(s);
            s.Ships.RemoveAll(x => x.State == ShipState.Gone);
            Fuel(s);
            if (s.Tick % (60 * Station.TicksPerSecond) == 0) Upkeep(s);

            s.Reputation = Math.Max(0, Math.Min(100, s.Reputation));
        }

        static void Traffic(Station s)
        {
            if (s.Tick < s.NextArrivalTick) return;

            double interval = Rules.BaseArrivalSeconds * (1.6 - s.Reputation / 100.0) * (0.6 + 0.8 * s.Rng.NextDouble())
                * Math.Pow(Rules.CommsTrafficFactor, s.CommsArraysOnline)
                * Math.Pow(Rules.ServiceTrafficFactor, Math.Min(6, s.ServicesOffered.Count()));
            s.NextArrivalTick = s.Tick + Math.Max(1, Rules.Ticks(interval));

            var role = PickRole(s);
            var roleDef = ShipRoles.Get(role);
            var cls = roleDef.Hull;
            var def = ShipCatalog.Get(cls);
            var name = ShipCatalog.RandomName(s.Rng, role);

            int waiting = s.Ships.Count(x => x.State == ShipState.Hailing || x.State == ShipState.Approaching);
            if (waiting >= Rules.MaxHolding + Rules.CommsHoldingSlots * s.CommsArraysOnline)
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
                Role = role,
                Livery = s.Rng.Next(6),
                FuelNeeded = Math.Round(def.MinFuel + (def.MaxFuel - def.MinFuel) * s.Rng.NextDouble()),
                NeedsRepair = s.Rng.NextDouble() < def.RepairChance,
                MaxPatienceTicks = Rules.Ticks(def.PatienceSeconds),
                ArrivedTick = s.Tick,
                Crew = s.Rng.Next(roleDef.MinCrew, roleDef.MaxCrew + 1),
            };
            ship.Errands.AddRange(RollErrands(s, roleDef, ship.NeedsRepair));
            ship.PatienceTicks = ship.MaxPatienceTicks;
            ship.Enter(ShipState.Approaching, Rules.Ticks(Rules.ApproachSeconds));
            s.Ships.Add(ship);
        }

        /// <summary>Who's arriving: a weighted pick among the roles this station attracts right now.</summary>
        static ShipRole PickRole(Station s)
        {
            bool hangar = s.Buildings.Any(b => b.Connected && b.Def.CanServe(ShipClass.Hauler));
            var options = new List<(ShipRole role, double weight)>();
            foreach (var def in ShipRoles.All)
            {
                if (def.Hull == ShipClass.Hauler && !hangar) continue;
                if (def.AttractedBy is BuildingType attractor && !s.Buildings.Any(b => b.Type == attractor && b.Operating)) continue;
                double weight = def.Role == ShipRole.Freighter ? def.Weight * (0.5 + s.Reputation / 100.0) : def.Weight;
                options.Add((def.Role, weight));
            }

            double roll = s.Rng.NextDouble() * options.Sum(o => o.weight);
            foreach (var (role, weight) in options)
            {
                if (roll < weight) return role;
                roll -= weight;
            }
            return options[options.Count - 1].role;
        }

        /// <summary>The need (if the role has one), then a few distinct wishes from the role's list.</summary>
        static IEnumerable<Errand> RollErrands(Station s, ShipRoleDef def, bool damaged)
        {
            if (def.Need is ServiceType need) yield return new Errand { Service = need, IsNeed = true };

            var pool = def.Wishes.ToList();
            int count = s.Rng.Next(def.MinWishes, def.MaxWishes + 1);
            // A damaged ship wants parts if it can get them.
            if (damaged && pool.Remove(ServiceType.Parts))
            {
                yield return new Errand { Service = ServiceType.Parts };
                count--;
            }
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int k = s.Rng.Next(pool.Count);
                yield return new Errand { Service = pool[k] };
                pool.RemoveAt(k);
            }
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

            // 0. The crew heads out onto the station while the ship is seen to.
            if (!ship.Disembarked) Crew.Disembark(s, ship, building);

            // 1. Refuel from the station tank (the pumps slow down in a brownout).
            if (ship.FuelLoaded < ship.FuelNeeded)
            {
                double rate = Rules.PumpRate * s.PowerSatisfaction / Station.TicksPerSecond;
                double pump = Math.Min(rate, Math.Min(ship.FuelNeeded - ship.FuelLoaded, s.FuelStock));
                if (pump <= 1e-9 && s.FuelStock <= 1e-9)
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
                if (s.PowerCredit >= 1) ship.StateTicks--; // the crew's tools need power
                if (ship.StateTicks > 0) return;
                ship.Repaired = true;
            }

            // 3. Wait for everyone to come back aboard.
            if (!Crew.AllAboard(s, ship)) return;

            // 4. Pay and go. (Services were paid for at each building.)
            var def = ShipCatalog.Get(ship.Class);
            double fuel = ship.FuelLoaded * Rules.FuelSellPrice;
            double repairs = ship.Repaired ? def.RepairFee : 0;
            double bill = def.LandingFee + fuel + repairs;
            s.Earn(def.LandingFee, LedgerLine.LandingFees, building);
            s.Earn(fuel, LedgerLine.FuelSales, building);
            if (repairs > 0) s.Earn(repairs, LedgerLine.Repairs, building);
            s.ShipsServed++;
            s.Reputation += def.ReputationOnServed + Satisfaction(s, ship);
            if (building != null) building.ShipsServed++;
            s.Report($"{ship.Name} paid {bill:0} cr{(ship.Repaired ? " (refuel + repairs)" : "")}.", EventKind.Revenue, building?.Id, bill);
            ship.Enter(ShipState.Departing, Rules.Ticks(Rules.DepartingSeconds));
        }

        /// <summary>Reputation from what the ship got out of its visit; reports a missed need.</summary>
        static double Satisfaction(Station s, Ship ship)
        {
            double change = 0;
            foreach (var errand in ship.Errands)
            {
                if (errand.Outcome != ErrandOutcome.Met) errand.Outcome = ErrandOutcome.Failed;
                if (errand.IsNeed)
                {
                    if (errand.Outcome == ErrandOutcome.Met)
                    {
                        change += Rules.ReputationNeedMet;
                    }
                    else
                    {
                        change -= Rules.ReputationNeedFailed;
                        s.ShipsDisappointed++;
                        var provider = Services.Provider(errand.Service);
                        s.Report($"{ship.Name} left disappointed: no {provider.Name.ToLowerInvariant()} they could get to. Reputation −{Rules.ReputationNeedFailed:0.#}.", EventKind.Warning);
                    }
                }
                else if (errand.Outcome == ErrandOutcome.Met)
                {
                    change += Rules.ReputationPerWish;
                }
            }
            return change;
        }

        static void LeaveAngry(Station s, Ship ship, string why)
        {
            Crew.Recall(s, ship);
            ship.Angry = true;
            s.ShipsLostAngry++;
            s.Reputation -= Rules.ReputationLostWhenAngry;
            s.Report($"{ship.Name} {why}. Reputation −{Rules.ReputationLostWhenAngry:0}.", EventKind.Alert, ship.BuildingId);
            ship.Enter(ShipState.Departing, Rules.Ticks(Rules.DepartingSeconds));
        }

        /// <summary>Automatic clearance: only once the station has air traffic control.</summary>
        static void Dock(Station s)
        {
            if (!s.AutoClearance) return;
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
            double capacity = s.FuelCapacity;
            foreach (var b in s.Buildings)
                if (b.Running && b.Def.RefineRate > 0)
                    s.FuelStock = Math.Min(capacity, s.FuelStock + b.Def.RefineRate * s.PowerSatisfaction / Station.TicksPerSecond);

            if (s.FuelDeliveryTick >= 0)
            {
                if (s.Tick < s.FuelDeliveryTick) return;
                s.FuelStock = Math.Min(s.FuelCapacity, s.FuelStock + s.FuelDeliveryAmount);
                s.FuelDeliveryTick = -1;
                s.Report($"Fuel tanker delivered {s.FuelDeliveryAmount:0} units.");
                return;
            }

            if (capacity <= 0 || s.FuelStock >= capacity * Rules.RestockThreshold) return;

            double amount = Math.Floor(Math.Min(capacity - s.FuelStock, Math.Max(0, s.Money) / Rules.FuelBuyPrice));
            if (amount < 10) return; // can't afford a useful load

            s.Spend(amount * Rules.FuelBuyPrice, LedgerLine.FuelPurchases);
            s.FuelDeliveryAmount = amount;
            s.FuelDeliveryTick = s.Tick + Rules.Ticks(Rules.FuelDeliverySeconds);
            s.Report($"Fuel running low: tanker ordered ({amount:0} units, {amount * Rules.FuelBuyPrice:0} cr).", EventKind.Warning);
        }

        /// <summary>Orbital sunrise and sunset notices.</summary>
        static void Sun(Station s)
        {
            bool day = Orbit.IsDay(s.Tick);
            if (day == s.WasDay) return;
            s.WasDay = day;
            if (day) s.Report("Orbital sunrise. The solar arrays wake up.", EventKind.Orbit);
            else s.Report("Orbital sunset: the station enters the planet's shadow. Running on batteries.", EventKind.Orbit);
        }

        /// <summary>
        /// The power grid, once per tick: solar (by sun exposure) and steady generation against demand,
        /// with batteries buffering the difference. Refineries only take spare power; fuel cell generators
        /// start when the batteries run low. If the batteries are flat and demand still outstrips supply,
        /// the station browns out and everything that needs power slows down in proportion.
        /// </summary>
        static void Power(Station s)
        {
            const double dt = 1.0 / Station.TicksPerSecond;
            double exposure = Orbit.SunExposure(s.Tick);
            double solar = 0, steady = 0, demand = 0, capacity = 0;

            foreach (var b in s.Buildings)
            {
                b.Running = false;
                if (!b.Connected) continue;
                var def = b.Def;
                capacity += def.BatteryCapacity;
                if (!b.Enabled) continue;
                solar += def.SolarOutput * exposure;
                steady += def.PowerOutput;
                if (def.RefineRate <= 0) demand += def.PowerUse;
            }
            s.BatteryCapacity = capacity;
            s.BatteryCharge = Math.Min(s.BatteryCharge, capacity);
            double fraction = s.BatteryFraction;

            // Refineries: a deferrable load that runs on surplus power or a healthy battery.
            bool tankFull = s.FuelStock >= s.FuelCapacity - 1e-6;
            foreach (var b in s.Buildings)
            {
                if (!b.Operating || b.Def.RefineRate <= 0 || tankFull) continue;
                double surplus = solar + steady - demand;
                if (surplus < b.Def.PowerUse && fraction <= Rules.RefineryMinCharge) continue;
                b.Running = true;
                demand += b.Def.PowerUse;
            }

            // Fuel cell generators, with hysteresis so they don't chatter on and off.
            double net = solar + steady - demand;
            if (!s.GeneratorsOn && net < 0 && fraction < Rules.GeneratorStartCharge) s.GeneratorsOn = true;
            else if (s.GeneratorsOn && fraction >= Rules.GeneratorStopCharge) s.GeneratorsOn = false;
            double generated = 0;
            if (s.GeneratorsOn)
            {
                foreach (var b in s.Buildings)
                {
                    if (!b.Operating || b.Def.GeneratorOutput <= 0) continue;
                    double want = b.Def.FuelBurn * dt;
                    double burn = Math.Min(want, s.FuelStock);
                    if (burn <= 1e-9) continue;
                    s.FuelStock -= burn;
                    generated += b.Def.GeneratorOutput * burn / want;
                    b.Running = true;
                }
            }

            double supply = solar + steady + generated;
            double charge = s.BatteryCharge + (supply - demand) * dt;
            double satisfaction = 1;
            if (charge < 0)
            {
                // Flat batteries: loads only get what's being generated (plus the last of the charge).
                satisfaction = Math.Max(0, Math.Min(1, (supply * dt + s.BatteryCharge) / (demand * dt)));
                charge = 0;
            }

            s.BatteryCharge = Math.Min(capacity, charge);
            s.SolarPower = solar;
            s.PowerSupply = supply;
            s.PowerDemand = demand;
            s.PowerSatisfaction = satisfaction;
            s.PowerCredit += satisfaction;
            if (s.PowerCredit > 1) s.PowerCredit -= 1;

            ReportPower(s, supply, demand);
        }

        static void ReportPower(Station s, double supply, double demand)
        {
            bool brownout = s.PowerSatisfaction < 0.999;
            if (brownout != s.Brownout)
            {
                s.Brownout = brownout;
                // Don't spam the log if the grid hovers right at the edge.
                if (s.Tick - s.LastPowerReportTick > Rules.Ticks(20))
                {
                    s.LastPowerReportTick = s.Tick;
                    if (brownout)
                        s.Report($"Power failure: the batteries are flat and the station needs {demand:0} kW but makes {supply:0}. Everything slows down.", EventKind.Alert);
                    else
                        s.Report("Power restored.", EventKind.Info);
                }
            }

            double fraction = s.BatteryFraction;
            if (!s.BatteryWarned && fraction < Rules.BatteryWarningCharge && supply < demand && s.BatteryCapacity > 0)
            {
                s.BatteryWarned = true;
                s.Report($"Batteries at {fraction:P0} and draining. Build solar arrays, battery banks or a fuel cell generator, or switch something off.", EventKind.Warning);
            }
            else if (s.BatteryWarned && fraction > 0.5)
            {
                s.BatteryWarned = false;
            }
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
            s.RecomputeConnections();
            s.FuelStock = s.FuelCapacity;
            s.BatteryCapacity = s.Buildings.Sum(b => b.Def.BatteryCapacity);
            s.BatteryCharge = s.BatteryCapacity;
            s.WasDay = Orbit.IsDay(0);
            s.NextArrivalTick = Rules.Ticks(Rules.FirstArrivalSeconds);
            s.Report("Your waystation is open: one pad and one fuel tank, floating in a lot of empty space.");
            return new Simulation(s);
        }
    }
}
