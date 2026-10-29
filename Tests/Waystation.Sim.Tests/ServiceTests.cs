using System.Linq;
using NUnit.Framework;
using Waystation.Sim;

namespace Waystation.Sim.Tests
{
    public class ServiceTests
    {
        /// <summary>A station with a service building on foundations right next to the starter pad.</summary>
        static Simulation StationWith(BuildingType type, int seed = 3)
        {
            var sim = GameSetup.NewStation(seed);
            var s = sim.Station;
            s.Money = 100000;
            var def = BuildingCatalog.Get(type);
            var origin = FoundationTests.Foundations(sim, def.Width, def.Height);
            Assert.That(sim.Execute(new PlaceBuilding(type, origin), out var error), Is.True, error);
            return sim;
        }

        /// <summary>A ship of a given role, docked on the hub and about to be serviced.</summary>
        static Ship Dock(Station s, ShipRole role, params ServiceType[] errands)
        {
            var hub = s.Hub;
            var ship = new Ship
            {
                Id = s.NextShipId++, Name = "Test", Class = ShipRoles.Get(role).Hull, Role = role, Crew = 2,
                FuelNeeded = 6, MaxPatienceTicks = 600, PatienceTicks = 600, BuildingId = hub.Id,
            };
            foreach (var e in errands) ship.Errands.Add(new Errand { Service = e, IsNeed = e == ShipRoles.Get(role).Need });
            ship.Enter(ShipState.Servicing, 0);
            hub.ShipId = ship.Id;
            s.Ships.Add(ship);
            return ship;
        }

        [Test]
        public void EveryServiceHasABuildingAndACategory()
        {
            foreach (ServiceType service in System.Enum.GetValues(typeof(ServiceType)))
            {
                var provider = Services.Provider(service);
                Assert.That(provider.RequiresFoundation, service.ToString());
                Assert.That(provider.Category, Is.EqualTo(Services.Category(service)), service.ToString());
                Assert.That(provider.Capacity, Is.GreaterThan(0));
                Assert.That(provider.VisitFee, Is.GreaterThan(0));
            }
            foreach (var role in ShipRoles.All)
                if (role.Need is ServiceType need) Assert.That(Services.Category(need), Is.Not.EqualTo(BuildCategory.Landing));
        }

        [Test]
        public void CrewWalkToTheBuildingPayAndComeBackBeforeTheShipLeaves()
        {
            var sim = StationWith(BuildingType.Cantina);
            var s = sim.Station;
            var cantina = s.Buildings.Single(b => b.Type == BuildingType.Cantina);
            var ship = Dock(s, ShipRole.Traveller, ServiceType.Food);

            sim.Advance(0.1);
            Assert.That(Crew.Of(s, ship).Count(), Is.EqualTo(2), "both passengers went out");
            Assert.That(s.People.All(p => p.BuildingId == cantina.Id));

            sim.Advance(3);
            Assert.That(ship.State, Is.EqualTo(ShipState.Servicing), "refuelled, but waiting for its people");

            sim.Advance(cantina.Def.VisitSeconds + 10);
            Assert.That(s.People, Is.Empty, "everyone back aboard");
            Assert.That(ship.State, Is.AnyOf(ShipState.Departing, ShipState.Gone));
            Assert.That(ship.Errands[0].Outcome, Is.EqualTo(ErrandOutcome.Met));
            Assert.That(s.Ledger.Totals[(int)LedgerLine.Leisure], Is.EqualTo(2 * cantina.Def.VisitFee));
            Assert.That(cantina.Visits, Is.EqualTo(2));
        }

        [Test]
        public void ANeedWithNowhereToGoDisappoints()
        {
            var sim = GameSetup.NewStation(3);
            var s = sim.Station;
            var ship = Dock(s, ShipRole.Freighter, ServiceType.Trade);
            double reputation = s.Reputation;

            sim.Advance(12);
            Assert.That(s.People, Is.Empty, "nobody got off: there's nowhere to trade");
            Assert.That(ship.State, Is.AnyOf(ShipState.Departing, ShipState.Gone));
            Assert.That(ship.Errands[0].Outcome, Is.EqualTo(ErrandOutcome.Failed));
            Assert.That(s.ShipsDisappointed, Is.EqualTo(1));
            Assert.That(s.Reputation, Is.LessThan(reputation));
            Assert.That(s.Log.Any(e => e.Text.Contains("disappointed")));
        }

        [Test]
        public void UnreachableBuildingsDontCount()
        {
            var sim = GameSetup.NewStation(3);
            var s = sim.Station;
            s.Money = 100000;
            // Foundations east of the hub, but the cantina's own cells are all there is: then a walkway
            // gap breaks the way (foundations two cells away, joined only by... nothing).
            var hub = s.Hub;
            var far = new Cell(hub.Origin.X + hub.Def.Width + 3, hub.Origin.Y);
            for (int x = 0; x < 5; x++) for (int y = 0; y < 5; y++)
                sim.Execute(new PlaceBuilding(BuildingType.Foundation, new Cell(far.X + x, far.Y + y)), out _);
            sim.Execute(new PlaceBuilding(BuildingType.Cantina, far), out _);
            Assert.That(Crew.FindProvider(s, Crew.HomeCell(hub), ServiceType.Food, null, null).building, Is.Null);

            // A walkway bridge makes it reachable.
            for (int x = hub.Origin.X + hub.Def.Width; x < far.X; x++)
                sim.Execute(new PlaceBuilding(BuildingType.Walkway, new Cell(x, hub.Origin.Y)), out _);
            var (cantina, path) = Crew.FindProvider(s, Crew.HomeCell(hub), ServiceType.Food, null, null);
            Assert.That(cantina, Is.Not.Null);
            Assert.That(path.First(), Is.EqualTo(Crew.HomeCell(hub)));
            Assert.That(path.All(s.IsWalkable));
        }

        [Test]
        public void FullBuildingsMakePeopleQueue()
        {
            var sim = StationWith(BuildingType.CorporateOffice); // capacity 2
            var s = sim.Station;
            var ship = Dock(s, ShipRole.Executive, ServiceType.Corporate);
            ship.Crew = 4;
            sim.Advance(6);
            Assert.That(s.People.Count(p => p.State == PersonState.Visiting), Is.EqualTo(2));
            Assert.That(s.People.Count(p => p.State == PersonState.Queueing), Is.EqualTo(2));
        }

        [Test]
        public void ServiceBuildingsAttractTheirShips()
        {
            var sim = GameSetup.NewStation(4);
            var s = sim.Station;
            s.AutoAssign = true;
            sim.Advance(6 * 60);
            Assert.That(s.Log.Any(e => e.Text.StartsWith("USMS")), Is.False, "no barracks, no USM patrols");

            sim = StationWith(BuildingType.MilitaryBarracks, 4);
            s = sim.Station;
            s.AutoAssign = true;
            sim.Advance(6 * 60);
            Assert.That(s.Log.Any(e => e.Kind == EventKind.Hail && e.Text.StartsWith("USMS")), "USM patrols come once there's a barracks");
            Assert.That(s.Ledger.Totals[(int)LedgerLine.Business], Is.GreaterThan(0));
        }

        [Test]
        public void YouCantDemolishABuildingWithPeopleInside()
        {
            var sim = StationWith(BuildingType.Cantina);
            var s = sim.Station;
            var cantina = s.Buildings.Single(b => b.Type == BuildingType.Cantina);
            Dock(s, ShipRole.Traveller, ServiceType.Food);
            sim.Advance(3);
            Assert.That(s.People.Any(p => p.State == PersonState.Visiting));
            Assert.That(new Demolish(cantina.Id).Validate(s), Does.Contain("inside"));
        }

        [Test]
        public void PeopleWhoseWayIsCutGoBackAboard()
        {
            var sim = GameSetup.NewStation(3);
            var s = sim.Station;
            s.Money = 100000;
            var hub = s.Hub;
            var far = new Cell(hub.Origin.X + hub.Def.Width + 3, hub.Origin.Y);
            for (int x = 0; x < 5; x++) for (int y = 0; y < 5; y++)
                sim.Execute(new PlaceBuilding(BuildingType.Foundation, new Cell(far.X + x, far.Y + y)), out _);
            sim.Execute(new PlaceBuilding(BuildingType.Cantina, far), out _);
            for (int x = hub.Origin.X + hub.Def.Width; x < far.X; x++)
                sim.Execute(new PlaceBuilding(BuildingType.Walkway, new Cell(x, hub.Origin.Y)), out _);

            var ship = Dock(s, ShipRole.Traveller, ServiceType.Food);
            sim.Advance(0.5);
            Assert.That(s.People, Is.Not.Empty);
            var bridge = s.BuildingAt(new Cell(hub.Origin.X + hub.Def.Width + 1, hub.Origin.Y));
            sim.Execute(new Demolish(bridge.Id), out _);
            sim.Advance(10);
            Assert.That(s.People, Is.Empty);
            Assert.That(ship.Errands[0].Outcome, Is.Not.EqualTo(ErrandOutcome.Met));
        }

        [Test]
        public void ServicesStayDeterministic()
        {
            double Run()
            {
                var sim = StationWith(BuildingType.Bazaar, 9);
                sim.Station.AutoAssign = true;
                sim.Advance(8 * 60);
                return sim.Station.Money + sim.Station.People.Count * 1000 + sim.Station.Reputation;
            }
            Assert.That(Run(), Is.EqualTo(Run()));
        }
    }
}
