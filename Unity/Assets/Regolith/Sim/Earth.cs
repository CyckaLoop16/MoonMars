using System;
using System.Collections.Generic;
using System.Linq;

namespace Regolith.Sim
{
    public enum CargoType
    {
        Crew,
        Materials,
        Supplies,
        Specialist,
    }

    public sealed class CargoDef
    {
        public CargoType Type;
        public string Name;
        public string Description;
        public double Cost;
    }

    /// <summary>A cargo flight from Earth on its way to a colony.</summary>
    public sealed class Shipment
    {
        public int FactionId;
        public int ColonyId;
        public CargoType Cargo;
        public Expertise Expertise;
        public int ArrivalDay;
    }

    public enum ObjectiveId
    {
        FirstLanding,
        LunarWater,
        LunarAir,
        LunarHarvest,
        SixMonths,
        PermanentPresence,
        ScienceReturn,
        LunarExports,
    }

    /// <summary>A goal set by your Earth sponsor. Completing it pays out and permanently raises funding.</summary>
    public sealed class ObjectiveDef
    {
        public ObjectiveId Id;
        public string Name;
        public string Description;
        public double Reward;
        public double FundingIncrease;
        public Func<GameState, Faction, bool> Condition;
    }

    public static class EarthCatalog
    {
        public const double BaseFunding = 100;
        public const double StartingCredits = 200;
        public const int CrewPerFlight = 2;

        /// <summary>Earth to the Moon. (Mars will use launch windows instead.)</summary>
        public static int TransitDays(BodyId body) => body == BodyId.Moon ? 3 : 210;

        public static double ExportPrice(Resource r) =>
            r == Resource.PlatinumMetals ? 20 : r == Resource.RareEarths ? 10 : 0;

        static readonly Dictionary<CargoType, CargoDef> Cargo = new Dictionary<CargoType, CargoDef>
        {
            { CargoType.Crew, new CargoDef { Type = CargoType.Crew, Name = "Crew rotation", Cost = 150, Description = $"{CrewPerFlight} more crew. Needs free housing." } },
            { CargoType.Materials, new CargoDef { Type = CargoType.Materials, Name = "Construction materials", Cost = 80, Description = "100 materials." } },
            { CargoType.Supplies, new CargoDef { Type = CargoType.Supplies, Name = "Life-support supplies", Cost = 60, Description = "60 oxygen, 60 water, 90 food." } },
            { CargoType.Specialist, new CargoDef { Type = CargoType.Specialist, Name = "Specialist", Cost = 200, Description = "A new Pioneer with the expertise you choose." } },
        };

        public static CargoDef Get(CargoType t) => Cargo[t];
        public static IEnumerable<CargoDef> AllCargo => Cargo.Values;

        static readonly List<ObjectiveDef> ObjectiveList = new List<ObjectiveDef>
        {
            new ObjectiveDef
            {
                Id = ObjectiveId.FirstLanding, Name = "Boots on the Regolith", Description = "Land your first base.",
                Reward = 50, FundingIncrease = 10, Condition = (s, f) => s.ColoniesOf(f).Any(),
            },
            new ObjectiveDef
            {
                Id = ObjectiveId.LunarWater, Name = "Lunar Water", Description = "Operate an Ice Mine.",
                Reward = 100, FundingIncrease = 20, Condition = (s, f) => Operates(s, f, ModuleType.IceMine),
            },
            new ObjectiveDef
            {
                Id = ObjectiveId.LunarAir, Name = "Breathing Lunar Air", Description = "Operate an Oxygen Plant.",
                Reward = 100, FundingIncrease = 20, Condition = (s, f) => Operates(s, f, ModuleType.OxygenPlant),
            },
            new ObjectiveDef
            {
                Id = ObjectiveId.LunarHarvest, Name = "First Lunar Harvest", Description = "Operate a Greenhouse.",
                Reward = 100, FundingIncrease = 20, Condition = (s, f) => Operates(s, f, ModuleType.Greenhouse),
            },
            new ObjectiveDef
            {
                Id = ObjectiveId.SixMonths, Name = "Six Months on the Moon", Description = "Keep a base alive for 6 months.",
                Reward = 150, FundingIncrease = 20,
                Condition = (s, f) => s.ColoniesOf(f).Any(c => c.IsAlive && s.Day - c.FoundedDay >= 180),
            },
            new ObjectiveDef
            {
                Id = ObjectiveId.PermanentPresence, Name = "A Permanent Presence", Description = "Reach 12 crew in one colony.",
                Reward = 200, FundingIncrease = 40, Condition = (s, f) => s.ColoniesOf(f).Any(c => c.Pops >= 12),
            },
            new ObjectiveDef
            {
                Id = ObjectiveId.ScienceReturn, Name = "Science Return", Description = "Complete 3 research projects.",
                Reward = 150, FundingIncrease = 30, Condition = (s, f) => f.Researched.Count >= 3,
            },
            new ObjectiveDef
            {
                Id = ObjectiveId.LunarExports, Name = "Lunar Exports", Description = "Export 20 units of rare earths or platinum to Earth.",
                Reward = 200, FundingIncrease = 40, Condition = (s, f) => f.UnitsExported >= 20,
            },
        };

        public static IEnumerable<ObjectiveDef> Objectives => ObjectiveList;
        public static ObjectiveDef Get(ObjectiveId id) => ObjectiveList.First(o => o.Id == id);

        static bool Operates(GameState s, Faction f, ModuleType type) =>
            s.ColoniesOf(f).Any(c => c.IsAlive && c.Modules.Any(m => m.IsBuilt && m.Type == type && m.Efficiency > 0));
    }

    /// <summary>Earth funding, cargo deliveries and sponsor objectives.</summary>
    public static class EarthSystem
    {
        public static double Funding(GameState s, Faction f)
        {
            double objectives = f.CompletedObjectives.Sum(id => EarthCatalog.Get(id).FundingIncrease);
            return (EarthCatalog.BaseFunding + objectives) * PioneerSystem.FundingMultiplier(s, f);
        }

        public static void RunDay(GameState s)
        {
            foreach (var shipment in s.Shipments.Where(x => x.ArrivalDay <= s.Day).ToList())
            {
                s.Shipments.Remove(shipment);
                Deliver(s, shipment);
            }

            foreach (var f in s.Factions)
            foreach (var objective in EarthCatalog.Objectives)
            {
                if (f.CompletedObjectives.Contains(objective.Id) || !objective.Condition(s, f)) continue;

                f.CompletedObjectives.Add(objective.Id);
                f.Credits += objective.Reward;
                s.Report($"Earth objective complete: {objective.Name}! +{objective.Reward:0} credits, funding +{objective.FundingIncrease:0} per turn.");
            }
        }

        public static void RunMonth(GameState s)
        {
            foreach (var f in s.Factions)
                f.Credits += Funding(s, f);
        }

        static void Deliver(GameState s, Shipment shipment)
        {
            var colony = s.GetColony(shipment.ColonyId);
            if (colony == null || !colony.IsAlive) return;

            switch (shipment.Cargo)
            {
                case CargoType.Crew:
                    int arriving = Math.Min(EarthCatalog.CrewPerFlight, Math.Max(0, colony.Housing - colony.Pops));
                    colony.Pops += arriving;
                    s.Report($"{colony.Name}: crew rotation landed. {arriving} new crew.");
                    break;
                case CargoType.Materials:
                    colony.Stock[Resource.Materials] += 100;
                    s.Report($"{colony.Name}: cargo lander delivered 100 materials.");
                    break;
                case CargoType.Supplies:
                    colony.Stock[Resource.Oxygen] += 60;
                    colony.Stock[Resource.Water] += 60;
                    colony.Stock[Resource.Food] += 90;
                    s.Report($"{colony.Name}: life-support supplies delivered.");
                    break;
                case CargoType.Specialist:
                    var p = PioneerCatalog.Create(s, shipment.FactionId, colony.Id, shipment.Expertise, 2);
                    s.Revision++;
                    s.Report($"{colony.Name}: {p.Name} ({shipment.Expertise.ToString().ToLowerInvariant()}) has arrived from Earth.");
                    break;
            }
        }
    }

    public sealed class OrderCargo : ICommand
    {
        public readonly int FactionId;
        public readonly int ColonyId;
        public readonly CargoType Cargo;
        public readonly Expertise Expertise;

        public OrderCargo(int factionId, int colonyId, CargoType cargo, Expertise expertise = Expertise.Engineer)
        {
            FactionId = factionId;
            ColonyId = colonyId;
            Cargo = cargo;
            Expertise = expertise;
        }

        public string Validate(GameState state)
        {
            var f = state.GetFaction(FactionId);
            var colony = state.GetColony(ColonyId);
            if (f == null || colony == null || colony.FactionId != FactionId || !colony.IsAlive) return "No such colony";

            var def = EarthCatalog.Get(Cargo);
            if (f.Credits < def.Cost) return $"Needs {def.Cost:0} credits";

            if (Cargo == CargoType.Crew)
            {
                int incoming = state.Shipments.Count(x => x.ColonyId == ColonyId && x.Cargo == CargoType.Crew) * EarthCatalog.CrewPerFlight;
                if (colony.Pops + incoming + EarthCatalog.CrewPerFlight > colony.Housing) return "Not enough free housing";
            }
            return null;
        }

        public void Apply(GameState state)
        {
            var f = state.GetFaction(FactionId);
            var def = EarthCatalog.Get(Cargo);
            f.Credits -= def.Cost;
            state.Shipments.Add(new Shipment
            {
                FactionId = FactionId,
                ColonyId = ColonyId,
                Cargo = Cargo,
                Expertise = Expertise,
                ArrivalDay = state.Day + EarthCatalog.TransitDays(state.Body),
            });
            state.Report($"{f.Name}: {def.Name.ToLowerInvariant()} ordered from Earth, arriving in {EarthCatalog.TransitDays(state.Body)} days.");
        }
    }

    /// <summary>Sells a colony's whole stock of a strategic resource to Earth.</summary>
    public sealed class ExportToEarth : ICommand
    {
        public readonly int FactionId;
        public readonly int ColonyId;
        public readonly Resource Resource;

        public ExportToEarth(int factionId, int colonyId, Resource resource)
        {
            FactionId = factionId;
            ColonyId = colonyId;
            Resource = resource;
        }

        public string Validate(GameState state)
        {
            var colony = state.GetColony(ColonyId);
            if (colony == null || colony.FactionId != FactionId) return "No such colony";
            if (EarthCatalog.ExportPrice(Resource) <= 0) return "Earth doesn't buy that";
            if (colony.Stock[Resource] < 1) return "Nothing to export";
            return null;
        }

        public void Apply(GameState state)
        {
            var f = state.GetFaction(FactionId);
            var colony = state.GetColony(ColonyId);
            double units = Math.Floor(colony.Stock[Resource]);
            double credits = units * EarthCatalog.ExportPrice(Resource);

            colony.Stock[Resource] -= units;
            f.Credits += credits;
            f.UnitsExported += units;
            state.Report($"{colony.Name}: exported {units:0} {Resource} to Earth for {credits:0} credits.");
        }
    }
}
