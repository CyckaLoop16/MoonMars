using System.Linq;
using NUnit.Framework;
using Waystation.Sim;

namespace Waystation.Sim.Tests
{
    public class OrbitTests
    {
        static long TickAt(double phase) =>
            (long)((phase - Orbit.StartPhase + 1) % 1 * Orbit.PeriodSeconds * Station.TicksPerSecond);

        [Test]
        public void SunlightComesAndGoesWithTheOrbit()
        {
            Assert.That(Orbit.SunExposure(TickAt(0.3)), Is.EqualTo(1));
            Assert.That(Orbit.SunExposure(TickAt(0.85)), Is.EqualTo(0));
            Assert.That(Orbit.SunExposure(TickAt(1 - Orbit.ShadowFraction)), Is.EqualTo(0.5).Within(0.01));
            Assert.That(Orbit.IsDay(0), Is.True, "a new station starts in sunlight");

            long period = (long)(Orbit.PeriodSeconds * Station.TicksPerSecond);
            int dark = Enumerable.Range(0, (int)period).Count(t => !Orbit.IsDay(t));
            Assert.That(dark / (double)period, Is.EqualTo(Orbit.ShadowFraction).Within(0.01));
        }

        [Test]
        public void SunriseAndSunsetAreReported()
        {
            var sim = GameSetup.NewStation(1);
            sim.Advance(Orbit.PeriodSeconds);
            var orbit = sim.Station.Log.Where(e => e.Kind == EventKind.Orbit).ToList();
            Assert.That(orbit.Count, Is.EqualTo(2));
            Assert.That(orbit[0].Text, Does.Contain("sunset"));
            Assert.That(orbit[1].Text, Does.Contain("sunrise"));
        }
    }

    public class FoundationTests
    {
        /// <summary>A 2×2 (or bigger) block of foundations east of the hub, touching it.</summary>
        internal static Cell Foundations(Simulation sim, int w, int h)
        {
            var s = sim.Station;
            var origin = new Cell(s.Hub.Origin.X + 3, s.Hub.Origin.Y);
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                Assert.That(sim.Execute(new PlaceBuilding(BuildingType.Foundation, new Cell(origin.X + x, origin.Y + y)), out var e), Is.True, e);
            return origin;
        }

        [Test]
        public void ModulesStandOnFoundations()
        {
            var sim = GameSetup.NewStation(1);
            var s = sim.Station;
            s.Money = 10000;

            Assert.That(new PlaceBuilding(BuildingType.SolarArray, new Cell(2, 2)).Validate(s), Does.Contain("foundations"));

            var origin = Foundations(sim, 2, 2);
            Assert.That(sim.Execute(new PlaceBuilding(BuildingType.SolarArray, origin), out var error), Is.True, error);

            var array = s.BuildingAt(origin);
            Assert.That(array.Type, Is.EqualTo(BuildingType.SolarArray), "the module is on top");
            Assert.That(s.FloorAt(origin).Type, Is.EqualTo(BuildingType.Foundation));
            Assert.That(array.Connected, "connected through its foundations");

            Assert.That(new PlaceBuilding(BuildingType.BatteryBank, origin).Validate(s), Does.Contain("occupied"));
            Assert.That(new PlaceBuilding(BuildingType.Walkway, origin).Validate(s), Does.Contain("occupied"));

            // Half on foundations isn't enough.
            Assert.That(new PlaceBuilding(BuildingType.SolarArray, new Cell(origin.X + 1, origin.Y)).Validate(s), Is.Not.Null);

            // The floor can't go while something stands on it.
            Assert.That(new Demolish(s.FloorAt(origin).Id).Validate(s), Does.Contain("on top"));
            Assert.That(sim.Execute(new Demolish(array.Id), out error), Is.True, error);
            Assert.That(sim.Execute(new Demolish(s.FloorAt(origin).Id), out error), Is.True, error);
        }

        [Test]
        public void FuelTanksAddCapacityOnlyWhenConnected()
        {
            var sim = GameSetup.NewStation(1);
            var s = sim.Station;
            s.Money = 10000;
            double before = s.FuelCapacity;

            var origin = Foundations(sim, 2, 2);
            sim.Execute(new PlaceBuilding(BuildingType.FuelTank, origin), out _);
            Assert.That(s.FuelCapacity, Is.EqualTo(before + BuildingCatalog.Get(BuildingType.FuelTank).FuelCapacity));
        }
    }

    public class PowerTests
    {
        [Test]
        public void TheHubPowersItselfFromDayOne()
        {
            var sim = GameSetup.NewStation(1);
            sim.Advance(1);
            var s = sim.Station;
            Assert.That(s.PowerSatisfaction, Is.EqualTo(1));
            Assert.That(s.NetPower, Is.GreaterThan(0));
            Assert.That(s.BatteryCharge, Is.EqualTo(s.BatteryCapacity).And.GreaterThan(0));
        }

        [Test]
        public void SolarOnlyWorksInSunlight()
        {
            var sim = GameSetup.NewStation(1);
            var s = sim.Station;
            s.Money = 10000;
            sim.Execute(new PlaceBuilding(BuildingType.SolarArray, FoundationTests.Foundations(sim, 2, 2)), out _);

            sim.Advance(1);
            Assert.That(s.SolarPower, Is.EqualTo(10).Within(1e-6));

            sim.Advance(Orbit.SecondsToChange(s.Tick, out _) + Orbit.TwilightSeconds);
            Assert.That(Orbit.IsDay(s.Tick), Is.False);
            Assert.That(s.SolarPower, Is.EqualTo(0));
        }

        [Test]
        public void TooMuchDemandDrainsTheBatteryThenBrownsOut()
        {
            var sim = GameSetup.NewStation(1);
            var s = sim.Station;
            s.Money = 100000;
            // Three hangars: 2 + 3 × 6 = 20 kW against the hub's 8.
            var hub = s.Hub;
            for (int i = 0; i < 3; i++)
                Assert.That(sim.Execute(new PlaceBuilding(BuildingType.Hangar, new Cell(hub.Origin.X + 3 + 4 * i, hub.Origin.Y)), out var e), Is.True, e);

            sim.Advance(5);
            Assert.That(s.BatteryCharge, Is.LessThan(s.BatteryCapacity));
            Assert.That(s.PowerSatisfaction, Is.EqualTo(1), "the battery covers it for now");

            sim.Advance(60);
            Assert.That(s.BatteryCharge, Is.EqualTo(0));
            Assert.That(s.PowerSatisfaction, Is.EqualTo(8.0 / 20).Within(1e-6));
            Assert.That(s.Log.Any(e => e.Kind == EventKind.Alert && e.Text.Contains("Power failure")));

            // Switching two hangars off brings the grid back.
            foreach (var hangar in s.Buildings.Where(b => b.Type == BuildingType.Hangar).Take(2).ToList())
                Assert.That(sim.Execute(new SetEnabled(hangar.Id, false), out var e), Is.True, e);
            sim.Advance(1);
            Assert.That(s.PowerSatisfaction, Is.EqualTo(1));
        }

        [Test]
        public void GeneratorsBurnFuelWhenTheBatteryRunsLow()
        {
            var sim = GameSetup.NewStation(1);
            var s = sim.Station;
            s.Money = 100000;
            var hub = s.Hub;
            for (int i = 0; i < 2; i++)
                sim.Execute(new PlaceBuilding(BuildingType.Hangar, new Cell(hub.Origin.X + 3 + 4 * i, hub.Origin.Y)), out _);
            // Foundations west of the hub for the generator.
            var west = new Cell(hub.Origin.X - 1, hub.Origin.Y);
            sim.Execute(new PlaceBuilding(BuildingType.Foundation, west), out _);
            sim.Execute(new PlaceBuilding(BuildingType.Foundation, new Cell(west.X, west.Y + 1)), out _);
            Assert.That(sim.Execute(new PlaceBuilding(BuildingType.FuelCellGenerator, west), out var error), Is.True, error);

            double fuel = s.FuelStock;
            sim.Advance(120);
            Assert.That(s.FuelStock, Is.LessThan(fuel), "the generator burned fuel");
            Assert.That(s.PowerSatisfaction, Is.EqualTo(1), "and kept the lights on");
        }

        [Test]
        public void RefineriesMakeFuelOnSparePower()
        {
            var sim = GameSetup.NewStation(1);
            var s = sim.Station;
            s.Money = 100000;
            var origin = FoundationTests.Foundations(sim, 6, 3);
            sim.Execute(new PlaceBuilding(BuildingType.IceRefinery, origin), out _);
            sim.Execute(new PlaceBuilding(BuildingType.SolarArray, new Cell(origin.X + 2, origin.Y)), out _);
            sim.Execute(new PlaceBuilding(BuildingType.SolarArray, new Cell(origin.X + 4, origin.Y)), out _);

            s.FuelStock = 500; // above the tanker threshold, below full
            sim.Advance(10);
            var refinery = s.Buildings.Single(b => b.Type == BuildingType.IceRefinery);
            Assert.That(refinery.Running);
            Assert.That(s.FuelStock, Is.EqualTo(510).Within(0.2));
        }
    }

    public class ServiceModuleTests
    {
        [Test]
        public void TrafficControlClearsShipsAutomatically()
        {
            var sim = GameSetup.NewStation(1);
            var s = sim.Station;
            s.Money = 100000;
            Assert.That(s.AutoClearance, Is.False);

            sim.Execute(new PlaceBuilding(BuildingType.AtcTower, FoundationTests.Foundations(sim, 2, 2)), out _);
            sim.Advance(0.1);
            Assert.That(s.AtcOnline);

            sim.Advance(Rules.FirstArrivalSeconds + Rules.ApproachSeconds + 1);
            Assert.That(s.Ships.Any(x => x.State == ShipState.Landing || x.State == ShipState.Servicing), "cleared without the player");

            var tower = s.Buildings.Single(b => b.Def.Atc);
            sim.Execute(new SetEnabled(tower.Id, false), out _);
            sim.Advance(0.1);
            Assert.That(s.AutoClearance, Is.False);
        }

        [Test]
        public void LoungesEarnFromDockedCrews()
        {
            var sim = GameSetup.NewStation(3);
            var s = sim.Station;
            s.Money = 100000;
            sim.Execute(new PlaceBuilding(BuildingType.PilotLounge, FoundationTests.Foundations(sim, 2, 2)), out _);
            s.AutoAssign = true;
            sim.Advance(5 * 60);
            Assert.That(s.Ledger.Totals[(int)LedgerLine.Services], Is.GreaterThan(0));
        }

        [Test]
        public void SwitchedOffPadsTakeNoShips()
        {
            var sim = GameSetup.NewStation(1);
            var s = sim.Station;
            Assert.That(sim.Execute(new SetEnabled(s.Hub.Id, false), out var error), Is.False);
            Assert.That(error, Does.Contain("no power switch"));

            s.Money = 10000;
            var pad = new Cell(s.Hub.Origin.X + 3, s.Hub.Origin.Y);
            sim.Execute(new PlaceBuilding(BuildingType.LandingPad, pad), out _);
            var b = s.BuildingAt(pad);
            Assert.That(sim.Execute(new SetEnabled(b.Id, false), out error), Is.True, error);

            var ship = new Ship { Id = 99, State = ShipState.Hailing, Class = ShipClass.Shuttle };
            Assert.That(s.LandingError(ship, b), Does.Contain("switched off"));
        }

        [Test]
        public void TheBooksStillBalanceWithEverything()
        {
            var sim = GameSetup.NewStation(5);
            var s = sim.Station;
            s.Money = 100000;
            var origin = FoundationTests.Foundations(sim, 6, 4);
            sim.Execute(new PlaceBuilding(BuildingType.IceRefinery, origin), out _);
            sim.Execute(new PlaceBuilding(BuildingType.SolarArray, new Cell(origin.X + 2, origin.Y)), out _);
            sim.Execute(new PlaceBuilding(BuildingType.PilotLounge, new Cell(origin.X + 4, origin.Y)), out _);
            sim.Execute(new PlaceBuilding(BuildingType.CommsArray, new Cell(origin.X + 2, origin.Y + 2)), out _);
            sim.Execute(new PlaceBuilding(BuildingType.BatteryBank, new Cell(origin.X + 4, origin.Y + 2)), out _);
            sim.Execute(new PlaceBuilding(BuildingType.FuelCellGenerator, new Cell(origin.X + 5, origin.Y + 2)), out _);
            s.AutoAssign = true;
            double start = s.Money;
            double income = s.Revenue, expenses = s.Expenses;

            sim.Advance(3 * Orbit.PeriodSeconds);
            Assert.That(s.Money - start, Is.EqualTo(s.Revenue - income - (s.Expenses - expenses)).Within(1e-6));
            Assert.That(s.FuelStock, Is.LessThanOrEqualTo(s.FuelCapacity + 1e-6));
            Assert.That(s.BatteryCharge, Is.InRange(0, s.BatteryCapacity));
        }
    }
}
