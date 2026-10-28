using System.Linq;
using NUnit.Framework;
using Waystation.Sim;

namespace Waystation.Sim.Tests
{
    public class BuildingTests
    {
        [Test]
        public void ANewStationIsOnePadWithAFullTank()
        {
            var s = GameSetup.NewStation(1).Station;

            var pad = s.Buildings.Single();
            Assert.That(pad.Type, Is.EqualTo(BuildingType.StarterPad));
            Assert.That(pad.Connected);
            Assert.That(s.FuelStock, Is.EqualTo(s.FuelCapacity).And.GreaterThan(0));
            Assert.That(s.Money, Is.EqualTo(Rules.StartingMoney));
        }

        [Test]
        public void PlacementRules()
        {
            var sim = GameSetup.NewStation(1);
            var s = sim.Station;
            var hub = s.Hub;

            Assert.That(new PlaceBuilding(BuildingType.LandingPad, hub.Origin).Validate(s), Does.Contain("occupied"));
            Assert.That(new PlaceBuilding(BuildingType.LandingPad, new Cell(s.Width - 2, 0)).Validate(s), Does.Contain("Outside"));
            Assert.That(new PlaceBuilding(BuildingType.StarterPad, new Cell(0, 0)).Validate(s), Does.Contain("can't be built"));

            s.Money = 100;
            Assert.That(new PlaceBuilding(BuildingType.Hangar, new Cell(0, 0)).Validate(s), Does.Contain("credits"));

            s.Money = 5000;
            Assert.That(sim.Execute(new PlaceBuilding(BuildingType.Hangar, new Cell(0, 0)), out var error), Is.True, error);
            Assert.That(s.Money, Is.EqualTo(5000 - BuildingCatalog.Get(BuildingType.Hangar).Cost));
        }

        [Test]
        public void CorridorsConnectBuildingsToTheHub()
        {
            var sim = GameSetup.NewStation(1);
            var s = sim.Station;
            s.Money = 10000;
            var hub = s.Hub;

            // A pad two cells east of the hub, with a gap between them.
            int gapX = hub.Origin.X + 3;
            var padOrigin = new Cell(gapX + 1, hub.Origin.Y);
            sim.Execute(new PlaceBuilding(BuildingType.LandingPad, padOrigin), out _);
            var pad = s.BuildingAt(padOrigin);
            Assert.That(pad.Connected, Is.False);

            sim.Execute(new PlaceBuilding(BuildingType.Corridor, new Cell(gapX, hub.Origin.Y + 1)), out _);
            Assert.That(pad.Connected, Is.True);

            // Demolishing the corridor cuts it off again.
            var corridor = s.BuildingAt(new Cell(gapX, hub.Origin.Y + 1));
            sim.Execute(new Demolish(corridor.Id), out _);
            Assert.That(pad.Connected, Is.False);
        }

        [Test]
        public void CorridorLinkMasksFollowNeighbours()
        {
            var sim = GameSetup.NewStation(1);
            var s = sim.Station;
            s.Money = 10000;
            var hub = s.Hub;
            var c = new Cell(hub.Origin.X + 3, hub.Origin.Y + 1); // just east of the hub
            sim.Execute(new PlaceBuilding(BuildingType.Corridor, c), out _);
            sim.Execute(new PlaceBuilding(BuildingType.Corridor, c + Cell.Directions[1]), out _);

            int mask = s.LinkMask(c);
            Assert.That(mask & (1 << 3), Is.Not.Zero, "west: the hub");
            Assert.That(mask & (1 << 1), Is.Not.Zero, "east: the next corridor");
            Assert.That(mask & (1 << 0), Is.Zero, "north: empty");
        }

        [Test]
        public void DemolishRules()
        {
            var sim = GameSetup.NewStation(1);
            var s = sim.Station;
            Assert.That(new Demolish(s.Hub.Id).Validate(s), Does.Contain("heart"));

            sim.Execute(new PlaceBuilding(BuildingType.LandingPad, new Cell(0, 0)), out _);
            var pad = s.BuildingAt(new Cell(0, 0));
            double money = s.Money;
            sim.Execute(new Demolish(pad.Id), out _);
            Assert.That(s.Money, Is.EqualTo(money + pad.Def.Cost * Rules.DemolishRefund));
        }
    }

    public class ShipFlowTests
    {
        [Test]
        public void AShuttleLandsRefuelsPaysAndLeaves()
        {
            var sim = GameSetup.NewStation(3);
            var s = sim.Station;
            s.NextArrivalTick = long.MaxValue; // no random traffic
            var ship = Spawn(s, ShipClass.Shuttle, fuel: 30);
            double money = s.Money, fuel = s.FuelStock, rep = s.Reputation;

            sim.Advance(Rules.ApproachSeconds + 0.2);
            Assert.That(ship.State, Is.EqualTo(ShipState.Landing));
            Assert.That(s.Hub.ShipId, Is.EqualTo(ship.Id));

            sim.Advance(Rules.LandingSeconds + 30 / Rules.PumpRate + 0.5);
            Assert.That(ship.State, Is.EqualTo(ShipState.Departing));
            Assert.That(s.FuelStock, Is.EqualTo(fuel - 30).Within(1e-6));
            double bill = ShipCatalog.Get(ShipClass.Shuttle).LandingFee + 30 * Rules.FuelSellPrice;
            Assert.That(s.Money, Is.EqualTo(money + bill).Within(1e-6));
            Assert.That(s.Reputation, Is.GreaterThan(rep));

            sim.Advance(Rules.DepartingSeconds + 0.2);
            Assert.That(s.Ships, Is.Empty);
            Assert.That(s.Hub.ShipId, Is.Null);
            Assert.That(s.Hub.ShipsServed, Is.EqualTo(1));
        }

        [Test]
        public void ShipsQueueAndGiveUpWhenNoPadFreesUp()
        {
            var sim = GameSetup.NewStation(3);
            var s = sim.Station;
            s.NextArrivalTick = long.MaxValue;
            var first = Spawn(s, ShipClass.Shuttle, fuel: 30);
            var second = Spawn(s, ShipClass.Shuttle, fuel: 30);
            first.FuelNeeded = 100000; // hogs the only pad
            double rep = s.Reputation;

            sim.Advance(Rules.ApproachSeconds + 1);
            Assert.That(second.State, Is.EqualTo(ShipState.Holding));

            sim.Advance(ShipCatalog.Get(ShipClass.Shuttle).PatienceSeconds + 1);
            Assert.That(second.Angry, Is.True);
            Assert.That(s.Reputation, Is.LessThan(rep));
            Assert.That(s.ShipsLostAngry, Is.EqualTo(1));
        }

        [Test]
        public void HaulersNeedAHangarAndPayForRepairs()
        {
            var sim = GameSetup.NewStation(3);
            var s = sim.Station;
            s.NextArrivalTick = long.MaxValue;
            s.Money = 10000;
            var hub = s.Hub;

            var hauler = Spawn(s, ShipClass.Hauler, fuel: 60);
            hauler.NeedsRepair = true;
            sim.Advance(Rules.ApproachSeconds + 2);
            Assert.That(hauler.State, Is.EqualTo(ShipState.Holding), "the starter pad can't take haulers");

            // Hangar touching the hub's east side: connected without a corridor.
            sim.Execute(new PlaceBuilding(BuildingType.Hangar, new Cell(hub.Origin.X + 3, hub.Origin.Y)), out _);
            var hangar = s.BuildingAt(new Cell(hub.Origin.X + 3, hub.Origin.Y));
            Assert.That(hangar.Connected);

            double money = s.Money;
            sim.Advance(Rules.LandingSeconds + 60 / Rules.PumpRate + Rules.RepairSeconds + 1);

            var def = ShipCatalog.Get(ShipClass.Hauler);
            Assert.That(hauler.Repaired);
            Assert.That(s.Money, Is.EqualTo(money + def.LandingFee + 60 * Rules.FuelSellPrice + def.RepairFee).Within(1e-6));
        }

        [Test]
        public void UnconnectedPadsAreNotUsed()
        {
            var sim = GameSetup.NewStation(3);
            var s = sim.Station;
            s.NextArrivalTick = long.MaxValue;
            s.Hub.ShipId = 999; // starter pad busy
            sim.Execute(new PlaceBuilding(BuildingType.LandingPad, new Cell(0, 0)), out _);
            var lonely = s.BuildingAt(new Cell(0, 0));

            var ship = Spawn(s, ShipClass.Shuttle, fuel: 20);
            sim.Advance(Rules.ApproachSeconds + 2);
            Assert.That(lonely.Connected, Is.False);
            Assert.That(ship.State, Is.EqualTo(ShipState.Holding));
        }

        [Test]
        public void LowFuelTriggersATankerDelivery()
        {
            var sim = GameSetup.NewStation(3);
            var s = sim.Station;
            s.NextArrivalTick = long.MaxValue;
            s.FuelStock = 10;
            s.Money = 5000; // enough for a full refill
            double money = s.Money;

            sim.Advance(0.1);
            double ordered = s.FuelCapacity - 10;
            Assert.That(s.Money, Is.EqualTo(money - ordered * Rules.FuelBuyPrice).Within(1e-6));

            sim.Advance(Rules.FuelDeliverySeconds);
            Assert.That(s.FuelStock, Is.EqualTo(s.FuelCapacity).Within(1e-6));
        }

        [Test]
        public void TankersOnlyBuyWhatTheStationCanAfford()
        {
            var sim = GameSetup.NewStation(3);
            var s = sim.Station;
            s.NextArrivalTick = long.MaxValue;
            s.FuelStock = 10;
            s.Money = 180;

            sim.Advance(0.1);
            Assert.That(s.Money, Is.GreaterThanOrEqualTo(0));
            Assert.That(s.FuelDeliveryAmount, Is.EqualTo(100).Within(1e-9));
        }

        [Test]
        public void UpkeepIsChargedEveryMinute()
        {
            var sim = GameSetup.NewStation(3);
            var s = sim.Station;
            s.NextArrivalTick = long.MaxValue;
            double money = s.Money;

            sim.Advance(60);
            Assert.That(s.Money, Is.EqualTo(money - s.Hub.Def.UpkeepPerMinute).Within(1e-6));
        }

        [Test]
        public void TheHoldingPatternHasALimit()
        {
            var sim = GameSetup.NewStation(3);
            var s = sim.Station;
            s.Hub.ShipId = 999; // nowhere to land, so ships pile up
            for (int i = 0; i < 200 && s.ShipsDiverted == 0; i++)
            {
                s.Reputation = 100; // busy traffic
                foreach (var ship in s.Ships) ship.PatienceTicks = ship.MaxPatienceTicks; // nobody gives up
                sim.Advance(5);
            }
            Assert.That(s.ShipsDiverted, Is.GreaterThan(0));
            Assert.That(s.Ships.Count(x => x.State == ShipState.Holding || x.State == ShipState.Approaching), Is.LessThanOrEqualTo(Rules.MaxHolding));
        }

        [Test]
        public void SameSeedSameStation()
        {
            Station Run()
            {
                var sim = GameSetup.NewStation(42);
                sim.Station.Money = 5000;
                sim.Execute(new PlaceBuilding(BuildingType.LandingPad, new Cell(17, 14)), out _);
                sim.Advance(600);
                return sim.Station;
            }

            var a = Run();
            var b = Run();
            Assert.That(b.Money, Is.EqualTo(a.Money));
            Assert.That(b.ShipsServed, Is.EqualTo(a.ShipsServed));
            Assert.That(b.Reputation, Is.EqualTo(a.Reputation));
        }

        [Test]
        public void AOnePadStationTurnsAProfit()
        {
            var sim = GameSetup.NewStation(7);
            sim.Advance(10 * 60);
            var s = sim.Station;
            Assert.That(s.ShipsServed, Is.GreaterThan(10));
            Assert.That(s.Money, Is.GreaterThan(Rules.StartingMoney));
        }

        static Ship Spawn(Station s, ShipClass cls, double fuel)
        {
            var def = ShipCatalog.Get(cls);
            var ship = new Ship
            {
                Id = s.NextShipId++, Name = $"Test {cls}", Class = cls, FuelNeeded = fuel,
                MaxPatienceTicks = Rules.Ticks(def.PatienceSeconds), ArrivedTick = s.Tick,
            };
            ship.PatienceTicks = ship.MaxPatienceTicks;
            ship.Enter(ShipState.Approaching, Rules.Ticks(Rules.ApproachSeconds));
            s.Ships.Add(ship);
            return ship;
        }
    }
}
