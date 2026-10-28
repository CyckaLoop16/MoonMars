using System;
using System.Collections.Generic;
using System.Linq;

namespace Waystation.Sim
{
    public readonly struct StationEvent
    {
        public readonly long Tick;
        public readonly string Text;

        public StationEvent(long tick, string text)
        {
            Tick = tick;
            Text = text;
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

        public Station(int width, int height, int seed)
        {
            Width = width;
            Height = height;
            Seed = seed;
            Rng = new Random(seed);
        }

        public double Seconds => Tick / (double)TicksPerSecond;
        public double FuelCapacity => Buildings.Sum(b => b.Def.FuelCapacity);
        public Building Hub => Buildings.FirstOrDefault(b => b.Def.IsHub);

        public bool InBounds(Cell c) => c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height;
        public Building BuildingAt(Cell c) => Buildings.FirstOrDefault(b => b.Contains(c));
        public Building GetBuilding(int id) => Buildings.FirstOrDefault(b => b.Id == id);
        public Ship GetShip(int id) => Ships.FirstOrDefault(s => s.Id == id);

        public IEnumerable<Ship> Holding => Ships.Where(s => s.State == ShipState.Holding).OrderBy(s => s.ArrivedTick).ThenBy(s => s.Id);

        public void Report(string text) => Log.Add(new StationEvent(Tick, text));

        public void Earn(double amount, Building building)
        {
            Money += amount;
            Revenue += amount;
            if (building != null) building.Earned += amount;
        }

        public void Spend(double amount)
        {
            Money -= amount;
            Expenses += amount;
        }

        /// <summary>
        /// Flood-fills from the hub through every occupied cell (corridors and buildings alike).
        /// Anything reached is connected; anything else can't get fuel or power.
        /// </summary>
        public void RecomputeConnections()
        {
            foreach (var b in Buildings) b.Connected = false;
            var hub = Hub;
            if (hub == null) return;

            var occupied = new Dictionary<Cell, Building>();
            foreach (var b in Buildings)
            foreach (var c in b.Footprint())
                occupied[c] = b;

            var seen = new HashSet<Cell>();
            var frontier = new Queue<Cell>(hub.Footprint());
            foreach (var c in hub.Footprint()) seen.Add(c);

            while (frontier.Count > 0)
            {
                var c = frontier.Dequeue();
                occupied[c].Connected = true;
                foreach (var n in c.Neighbors())
                    if (occupied.ContainsKey(n) && seen.Add(n))
                        frontier.Enqueue(n);
            }
        }

        /// <summary>Which of the four neighbours of a cell hold something a corridor should link to (N, E, S, W bit mask).</summary>
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
