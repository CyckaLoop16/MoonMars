using System.Linq;
using NUnit.Framework;
using Waystation.Sim;

namespace Waystation.Sim.Tests
{
    public class LedgerTests
    {
        [Test]
        public void TheBooksBalanceAgainstMoney()
        {
            var sim = GameSetup.NewStation(21);
            var s = sim.Station;
            s.AutoAssign = true;
            double start = s.Money;
            sim.Execute(new PlaceBuilding(BuildingType.LandingPad, new Cell(s.Hub.Origin.X + 3, s.Hub.Origin.Y)), out _);
            sim.Execute(new PlaceBuilding(BuildingType.Walkway, new Cell(0, 0)), out _);
            sim.Execute(new Demolish(s.BuildingAt(new Cell(0, 0)).Id), out _);
            sim.Advance(8 * 60);

            double income = Ledger.IncomeLines.Sum(l => s.Ledger.Totals[(int)l]);
            double expenses = Ledger.ExpenseLines.Sum(l => s.Ledger.Totals[(int)l]);
            Assert.That(s.Money, Is.EqualTo(start + income - expenses).Within(1e-6));
            Assert.That(income, Is.EqualTo(s.Revenue).Within(1e-6));
            Assert.That(expenses, Is.EqualTo(s.Expenses).Within(1e-6));

            Assert.That(s.Ledger.Totals[(int)LedgerLine.Construction], Is.EqualTo(900 + 25));
            Assert.That(s.Ledger.Totals[(int)LedgerLine.Refunds], Is.EqualTo(25 * Rules.DemolishRefund));
            Assert.That(s.Ledger.Totals[(int)LedgerLine.LandingFees], Is.GreaterThan(0));
            Assert.That(s.Ledger.Totals[(int)LedgerLine.FuelSales], Is.GreaterThan(0));
            Assert.That(s.Ledger.Totals[(int)LedgerLine.Upkeep], Is.GreaterThan(0));
        }

        [Test]
        public void HistoryKeepsOneBucketPerMinute()
        {
            var sim = GameSetup.NewStation(21);
            var s = sim.Station;
            s.AutoAssign = true;
            sim.Advance((Ledger.HistoryMinutes + 5) * 60);

            var history = s.Ledger.History;
            Assert.That(history.Count, Is.EqualTo(Ledger.HistoryMinutes));
            for (int i = 1; i < history.Count; i++)
                Assert.That(history[i].Minute, Is.EqualTo(history[i - 1].Minute + 1));
            Assert.That(history.Sum(m => m.Income), Is.GreaterThan(0));
            Assert.That(s.Ledger.LastMinuteNet, Is.EqualTo(history[history.Count - 2].Net));
        }

        [Test]
        public void EventsCarryKindsAndPaymentsCarryTheirPad()
        {
            var sim = GameSetup.NewStation(21);
            var s = sim.Station;
            s.AutoAssign = true;
            sim.Advance(4 * 60);

            Assert.That(s.Log.Any(e => e.Kind == EventKind.Hail));
            var payment = s.Log.First(e => e.Kind == EventKind.Revenue);
            Assert.That(payment.BuildingId, Is.EqualTo(s.Hub.Id));
            Assert.That(payment.Amount, Is.GreaterThan(0));
        }

        [Test]
        public void AverageHailWaitIsTracked()
        {
            var sim = GameSetup.NewStation(21);
            var s = sim.Station;
            s.NextArrivalTick = long.MaxValue;
            var ship = new Ship { Id = s.NextShipId++, Name = "Test", Class = ShipClass.Shuttle, FuelNeeded = 10, MaxPatienceTicks = 10000, PatienceTicks = 10000 };
            ship.Enter(ShipState.Approaching, 1);
            s.Ships.Add(ship);

            sim.Advance(0.1);
            sim.Advance(5);
            sim.Execute(new AssignLanding(ship.Id, s.Hub.Id), out _);
            Assert.That(s.HailsAnswered, Is.EqualTo(1));
            Assert.That(s.AverageHailWaitSeconds, Is.EqualTo(5).Within(0.11));
        }

        [Test]
        public void CheatMoneyIsBookedOnItsOwnLine()
        {
            var sim = GameSetup.NewStation(1);
            var s = sim.Station;
            double before = s.Money;
            Assert.That(sim.Execute(new CheatMoney(10000), out var error), Is.True, error);
            Assert.That(s.Money, Is.EqualTo(before + 10000));
            Assert.That(s.Ledger.Totals[(int)LedgerLine.Cheats], Is.EqualTo(10000));
            Assert.That(Ledger.IsIncome(LedgerLine.Cheats));
        }
    }
}
