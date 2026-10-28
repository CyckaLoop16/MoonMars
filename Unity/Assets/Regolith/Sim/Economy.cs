using System;
using System.Linq;

namespace Regolith.Sim
{
    /// <summary>
    /// Daily colony simulation: construction, staffing, power, production chains and life support.
    /// Monthly: crew arrivals from Earth.
    /// </summary>
    public static class Economy
    {
        public const double ConsumptionPerPop = 1.0;
        public const double BatteryEfficiency = 0.9;
        public const double DaysPerMonth = 30.0;
        /// <summary>A colony needs this many days of supplies per crew member before Earth sends more people.</summary>
        public const double ArrivalReserveDays = 10.0;

        static readonly Resource[] LifeSupport = { Resource.Oxygen, Resource.Water, Resource.Food };

        /// <summary>Consecutive days without a resource before a crew member dies.</summary>
        public static int DaysToDeath(Resource r)
        {
            switch (r)
            {
                case Resource.Oxygen: return 3;
                case Resource.Water: return 10;
                case Resource.Food: return 20;
                default: return int.MaxValue;
            }
        }

        public static void RunDay(GameState state)
        {
            if (state.Body == BodyId.Moon && Bodies.IsLunarDaylight(state.Day) != Bodies.IsLunarDaylight(state.Day - 1))
            {
                state.Report(Bodies.IsLunarDaylight(state.Day)
                    ? "Sunrise. The two-week lunar day begins."
                    : "Sunset. The two-week lunar night begins, and solar arrays go dark.");
            }

            foreach (var f in state.Factions) f.SciencePerDay = 0;
            foreach (var colony in state.Colonies.Where(c => c.IsAlive))
                RunColonyDay(state, colony);

            ResearchSystem.CheckBoosts(state);
        }

        public static void RunMonth(GameState state)
        {
            foreach (var colony in state.Colonies.Where(c => c.IsAlive))
            {
                if (colony.Pops >= colony.Housing) continue;

                double reserve = colony.Pops * ConsumptionPerPop * ArrivalReserveDays;
                if (LifeSupport.All(r => colony.Stock[r] >= reserve))
                {
                    colony.Pops++;
                    state.Report($"{colony.Name}: a new crew member arrived from Earth.");
                }
            }
        }

        static void RunColonyDay(GameState state, Colony colony)
        {
            var faction = state.GetFaction(colony.FactionId);
            var report = new ColonyReport();
            var before = new double[Stockpile.Count];
            for (int i = 0; i < Stockpile.Count; i++) before[i] = colony.Stock[(Resource)i];

            // 1. Construction: one module at a time, in the order queued.
            var building = colony.UnderConstruction;
            if (building != null && --building.BuildDaysRemaining <= 0)
            {
                state.Revision++;
                state.Report($"{colony.Name}: {building.Def.Name} completed.");
            }

            // 2. Staffing: crew fill jobs in module priority order (life support first).
            var active = colony.Modules.Where(m => m.IsBuilt).OrderBy(m => m.Def.Priority).ToList();
            int freeCrew = colony.Pops;
            foreach (var m in active)
            {
                m.Workers = Math.Min(m.Def.Jobs, freeCrew);
                freeCrew -= m.Workers;
                report.Jobs += m.Def.Jobs;
                report.Workers += m.Workers;
            }

            // 3. Power: if demand exceeds supply, every consumer runs at the same reduced ratio.
            double supply = 0, demand = 0;
            foreach (var m in active)
            {
                double staffing = Staffing(m);
                if (m.Def.EnergyOutput > 0) supply += m.Def.EnergyOutput * staffing * Sunlight(state, m);
                demand += m.Def.EnergyDraw * staffing;
            }
            // Batteries charge from surplus and discharge into deficits.
            double capacity = colony.EnergyCapacity;
            colony.StoredEnergy = Math.Min(colony.StoredEnergy, capacity);
            if (supply < demand)
            {
                double discharge = Math.Min(demand - supply, colony.StoredEnergy);
                colony.StoredEnergy -= discharge;
                supply += discharge;
            }
            else
            {
                colony.StoredEnergy += Math.Min((supply - demand) * BatteryEfficiency, capacity - colony.StoredEnergy);
            }

            double powerRatio = demand <= 0 ? 1.0 : Math.Min(1.0, supply / demand);
            if (powerRatio < 1.0) colony.HadBrownout = true;

            // 4. Production chains, in priority order so upstream output feeds downstream input today.
            foreach (var m in active)
            {
                var def = m.Def;
                double efficiency = Staffing(m) * (def.EnergyDraw > 0 ? powerRatio : 1.0);
                if (def.IsSolar) efficiency *= Sunlight(state, m);

                foreach (var input in def.Inputs)
                {
                    double needed = input.PerDay * efficiency;
                    if (needed > 0 && colony.Stock[input.Resource] < needed)
                        efficiency *= colony.Stock[input.Resource] / needed;
                }

                foreach (var input in def.Inputs)
                    colony.Stock[input.Resource] -= input.PerDay * efficiency;

                double multiplier = OutputMultiplier(m, state.Map.GetTile(m.Coord)) * Modifiers.OutputMultiplier(faction, m.Type);
                foreach (var output in def.Outputs)
                    colony.Stock[output.Resource] += output.PerDay * efficiency * multiplier;

                report.Science += def.Science * efficiency;
                m.Efficiency = efficiency;
            }

            // Automatic tile yields (Civ-style): surface robots and survey work across the whole territory.
            // Tile yields are per month, spread evenly over the days.
            foreach (var c in colony.Territory)
            {
                var y = state.Map.GetTile(c).Yields;
                colony.Stock[Resource.Materials] += y.Materials / DaysPerMonth;
                report.Science += y.Science / DaysPerMonth;
            }

            // 5. The crew breathes, drinks and eats. Running out is lethal.
            foreach (var r in LifeSupport)
            {
                double need = colony.Pops * ConsumptionPerPop * Modifiers.CrewUse(faction, r);
                if (colony.Stock[r] >= need)
                {
                    colony.Stock[r] -= need;
                    colony.ShortageDays[(int)r] = 0;
                    continue;
                }

                colony.Stock[r] = 0;
                int days = ++colony.ShortageDays[(int)r];
                if (days == 1)
                    state.Report($"{colony.Name}: {r.ToString().ToUpperInvariant()} HAS RUN OUT. Crew will start dying in {DaysToDeath(r)} days.");

                if (days >= DaysToDeath(r) && colony.Pops > 0)
                {
                    colony.Pops--;
                    colony.ShortageDays[(int)r] = 0;
                    state.Report($"{colony.Name}: a crew member died of {CauseOfDeath(r)}.");
                    if (colony.Pops == 0)
                    {
                        state.Report($"{colony.Name} has been lost with all hands.");
                        state.Revision++;
                    }
                }
            }

            for (int i = 0; i < Stockpile.Count; i++) report.Net[i] = colony.Stock[(Resource)i] - before[i];
            report.EnergySupply = supply;
            report.EnergyDemand = demand;
            colony.LastReport = report;

            faction.SciencePerDay += report.Science;
            ResearchSystem.AddScience(state, faction, report.Science);
        }

        static double Staffing(Module m) => m.Def.Jobs == 0 ? 1.0 : m.Workers / (double)m.Def.Jobs;

        static double Sunlight(GameState state, Module m) =>
            m.Def.IsSolar ? Bodies.SolarAvailability(state.Body, state.Map.GetTile(m.Coord), state.Day) : 1.0;

        /// <summary>How the tile under a module (its ice, minerals and deposit) affects its output.</summary>
        public static double OutputMultiplier(Module m, Tile tile)
        {
            double deposit = DepositCatalog.Multiplier(tile.Deposit, m.Type);
            switch (m.Type)
            {
                case ModuleType.IceMine: return tile.Ice * deposit;
                case ModuleType.Foundry: return (1.0 + tile.Minerals) * deposit;
                default: return deposit;
            }
        }

        static string CauseOfDeath(Resource r)
        {
            switch (r)
            {
                case Resource.Oxygen: return "hypoxia";
                case Resource.Water: return "dehydration";
                default: return "starvation";
            }
        }
    }
}
