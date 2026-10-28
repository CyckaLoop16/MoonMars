using System;
using System.Collections.Generic;
using System.Linq;

namespace Regolith.Sim
{
    public enum Expertise
    {
        Engineer,
        Scientist,
        Explorer,
        Administrator,
    }

    public enum Trait
    {
        Meticulous,
        Bold,
        Botanist,
        IceDriller,
        Tinkerer,
        Diplomat,
        /// <summary>Earned by surviving a near miss.</summary>
        Survivor,
    }

    public enum AssignmentKind
    {
        Unassigned,
        Module,
        Administrator,
        Council,
    }

    /// <summary>HOI4-style advisor seats with faction-wide effects.</summary>
    public enum CouncilSeat
    {
        ChiefScientist,
        ChiefEngineer,
        EarthLiaison,
    }

    public readonly struct Assignment : IEquatable<Assignment>
    {
        public readonly AssignmentKind Kind;
        public readonly int ColonyId;
        public readonly HexCoord Module;
        public readonly CouncilSeat Seat;

        Assignment(AssignmentKind kind, int colonyId, HexCoord module, CouncilSeat seat)
        {
            Kind = kind;
            ColonyId = colonyId;
            Module = module;
            Seat = seat;
        }

        public static Assignment None => default;
        public static Assignment ToModule(int colonyId, HexCoord module) => new Assignment(AssignmentKind.Module, colonyId, module, default);
        public static Assignment AsAdministrator(int colonyId) => new Assignment(AssignmentKind.Administrator, colonyId, default, default);
        public static Assignment OnCouncil(CouncilSeat seat) => new Assignment(AssignmentKind.Council, 0, default, seat);

        public bool Equals(Assignment o) => Kind == o.Kind && ColonyId == o.ColonyId && Module == o.Module && Seat == o.Seat;
        public override bool Equals(object obj) => obj is Assignment o && Equals(o);
        public override int GetHashCode() => unchecked(((int)Kind * 397) ^ ColonyId ^ Module.GetHashCode() ^ ((int)Seat << 8));
    }

    /// <summary>A named character who carries the civilization forward.</summary>
    public sealed class Pioneer
    {
        public int Id;
        public int FactionId;
        public string Name;
        public int BirthYear;
        public Expertise Expertise;
        /// <summary>1..5.</summary>
        public int Skill;
        public readonly List<Trait> Traits = new List<Trait>();
        public Assignment Assignment;
        /// <summary>Day the current assignment began, for experience.</summary>
        public int AssignedDay;
        /// <summary>Colony they live in; where they stand when unassigned.</summary>
        public int HomeColonyId;
        public bool IsAlive = true;
        public int? DiedDay;
        public string CauseOfDeath;

        public int Age(GameState s) => s.Date.Year - BirthYear;
        public bool Has(Trait t) => Traits.Contains(t);
    }

    public static class PioneerCatalog
    {
        public const int MaxSkill = 5;

        public static string Describe(Trait t)
        {
            switch (t)
            {
                case Trait.Meticulous: return "Meticulous: half the accident risk";
                case Trait.Bold: return "Bold: +10% output where assigned, double the accident risk";
                case Trait.Botanist: return "Botanist: double bonus in a Greenhouse";
                case Trait.IceDriller: return "Ice Driller: double bonus in an Ice Mine";
                case Trait.Tinkerer: return "Tinkerer: double bonus in an Oxygen Plant or Foundry";
                case Trait.Diplomat: return "Diplomat: double effect as Earth Liaison";
                default: return "Survivor: lived through a near miss; 25% less accident risk";
            }
        }

        public static string Describe(CouncilSeat seat)
        {
            switch (seat)
            {
                case CouncilSeat.ChiefScientist: return "Chief Scientist: +5% science per skill level (Scientists)";
                case CouncilSeat.ChiefEngineer: return "Chief Engineer: −5% construction time per skill level (Engineers)";
                default: return "Earth Liaison: +6% Earth funding per skill level (Administrators)";
            }
        }

        /// <summary>Which expertise each seat suits. Others count at half strength.</summary>
        public static Expertise BestFor(CouncilSeat seat) =>
            seat == CouncilSeat.ChiefScientist ? Expertise.Scientist
            : seat == CouncilSeat.ChiefEngineer ? Expertise.Engineer
            : Expertise.Administrator;

        /// <summary>Which expertise boosts a module fully. Others count at reduced strength.</summary>
        public static Expertise? BestFor(ModuleType type)
        {
            switch (type)
            {
                case ModuleType.SolarArray:
                case ModuleType.FissionReactor:
                case ModuleType.OxygenPlant:
                case ModuleType.Foundry:
                    return Expertise.Engineer;
                case ModuleType.Lab:
                case ModuleType.Greenhouse:
                    return Expertise.Scientist;
                case ModuleType.IceMine:
                case ModuleType.MineralExtractor:
                    return Expertise.Explorer;
                default:
                    return null;
            }
        }

        static readonly string[] FirstNames =
        {
            "Amara", "Kenji", "Sofia", "Tariq", "Ingrid", "Mateo", "Priya", "Oluwaseun", "Lena", "Rafael",
            "Mei", "Dmitri", "Aisha", "Callum", "Yuki", "Thandiwe", "Henrik", "Noor", "Santiago", "Freya",
            "Kwame", "Anika", "Joon", "Valentina", "Idris", "Hana", "Marcus", "Zainab", "Tomas", "Leilani",
            "Arjun", "Elif", "Bjorn", "Chiara", "Emeka", "Sakura", "Diego", "Astrid", "Ravi", "Nadia",
        };

        static readonly string[] LastNames =
        {
            "Okafor", "Tanaka", "Lindqvist", "Haddad", "Moreau", "Castillo", "Raman", "Adeyemi", "Novak", "Chen",
            "Petrov", "Mensah", "Sato", "Fitzgerald", "Kowalski", "Ndlovu", "Herrera", "Yilmaz", "Bergstrom", "Kapoor",
            "Silva", "Abara", "Park", "Rossi", "Mwangi", "Nakamura", "Duarte", "Eriksen", "Banerjee", "Volkova",
        };

        static readonly Trait[] StartingTraits =
            { Trait.Meticulous, Trait.Bold, Trait.Botanist, Trait.IceDriller, Trait.Tinkerer, Trait.Diplomat };

        public static Pioneer Create(GameState state, int factionId, int homeColonyId, Expertise expertise, int skill)
        {
            var rng = state.Rng;
            var p = new Pioneer
            {
                Id = state.NextPioneerId++,
                FactionId = factionId,
                HomeColonyId = homeColonyId,
                Name = $"{FirstNames[rng.Next(FirstNames.Length)]} {LastNames[rng.Next(LastNames.Length)]}",
                BirthYear = state.Date.Year - (28 + rng.Next(20)),
                Expertise = expertise,
                Skill = skill,
                AssignedDay = state.Day,
            };
            p.Traits.Add(StartingTraits[rng.Next(StartingTraits.Length)]);
            state.Pioneers.Add(p);
            return p;
        }
    }

    /// <summary>Pioneer effects, experience, and rare accidents.</summary>
    public static class PioneerSystem
    {
        /// <summary>Monthly chance of a fatal accident by assignment. Near misses are 4× as common.</summary>
        public const double FieldRisk = 0.004;
        public const double ModuleRisk = 0.0012;
        public const double BaseRisk = 0.0005;
        public const double CouncilRisk = 0.0002;
        public const double NearMissMultiplier = 4.0;
        public const int DaysPerSkillLevel = 360;

        public static IEnumerable<Pioneer> Living(GameState s, Faction f) => s.Pioneers.Where(p => p.IsAlive && p.FactionId == f.Id);

        public static Pioneer OnCouncil(GameState s, Faction f, CouncilSeat seat) =>
            Living(s, f).FirstOrDefault(p => p.Assignment.Kind == AssignmentKind.Council && p.Assignment.Seat == seat);

        public static Pioneer AdministratorOf(GameState s, Colony c) =>
            s.Pioneers.FirstOrDefault(p => p.IsAlive && p.Assignment.Kind == AssignmentKind.Administrator && p.Assignment.ColonyId == c.Id);

        public static Pioneer AtModule(GameState s, Colony c, HexCoord module) =>
            s.Pioneers.FirstOrDefault(p => p.IsAlive && p.Assignment.Kind == AssignmentKind.Module
                && p.Assignment.ColonyId == c.Id && p.Assignment.Module == module);

        /// <summary>Strength of a council seat: skill, halved if the expertise doesn't match, doubled for a Diplomat liaison.</summary>
        public static double SeatStrength(GameState s, Faction f, CouncilSeat seat)
        {
            var p = OnCouncil(s, f, seat);
            if (p == null) return 0;
            double strength = p.Skill * (p.Expertise == PioneerCatalog.BestFor(seat) ? 1.0 : 0.5);
            if (seat == CouncilSeat.EarthLiaison && p.Has(Trait.Diplomat)) strength *= 2;
            return strength;
        }

        /// <summary>Output multiplier a Pioneer gives the module they work at.</summary>
        public static double ModuleBonus(GameState s, Colony c, Module m)
        {
            var p = AtModule(s, c, m.Coord);
            if (p == null) return 1.0;

            var best = PioneerCatalog.BestFor(m.Type);
            double perLevel = best == p.Expertise ? 0.08 : 0.03;
            bool specialist = (p.Has(Trait.Botanist) && m.Type == ModuleType.Greenhouse)
                || (p.Has(Trait.IceDriller) && m.Type == ModuleType.IceMine)
                || (p.Has(Trait.Tinkerer) && (m.Type == ModuleType.OxygenPlant || m.Type == ModuleType.Foundry));
            if (specialist) perLevel *= 2;

            double bonus = 1.0 + perLevel * p.Skill;
            if (p.Has(Trait.Bold)) bonus += 0.1;
            return bonus;
        }

        public static double ScienceMultiplier(GameState s, Faction f) => 1.0 + 0.05 * SeatStrength(s, f, CouncilSeat.ChiefScientist);

        public static double FundingMultiplier(GameState s, Faction f) => 1.0 + 0.06 * SeatStrength(s, f, CouncilSeat.EarthLiaison);

        /// <summary>Construction time multiplier from the colony's Administrator and the Chief Engineer.</summary>
        public static double BuildTimeMultiplier(GameState s, Faction f, Colony c)
        {
            double m = 1.0 - 0.05 * SeatStrength(s, f, CouncilSeat.ChiefEngineer);
            var admin = c != null ? AdministratorOf(s, c) : null;
            if (admin != null) m -= (admin.Expertise == Expertise.Administrator ? 0.06 : 0.03) * admin.Skill;
            return Math.Max(0.4, m);
        }

        public static double MonthlyRisk(GameState s, Pioneer p)
        {
            double risk;
            switch (p.Assignment.Kind)
            {
                case AssignmentKind.Council: risk = CouncilRisk; break;
                case AssignmentKind.Module:
                    var colony = s.GetColony(p.Assignment.ColonyId);
                    var type = colony?.ModuleAt(p.Assignment.Module)?.Type;
                    risk = type == ModuleType.IceMine || type == ModuleType.MineralExtractor ? FieldRisk : ModuleRisk;
                    break;
                default: risk = BaseRisk; break;
            }

            if (p.Has(Trait.Meticulous)) risk *= 0.5;
            if (p.Has(Trait.Bold)) risk *= 2.0;
            if (p.Has(Trait.Survivor)) risk *= 0.75;
            return risk;
        }

        public static void RunDay(GameState s)
        {
            foreach (var p in s.Pioneers.Where(p => p.IsAlive).ToList())
            {
                // A Pioneer whose colony died, or whose module vanished, falls back to unassigned.
                var home = s.GetColony(p.HomeColonyId);
                if (home != null && !home.IsAlive)
                {
                    Kill(s, p, "lost with their colony");
                    continue;
                }
                if (p.Assignment.Kind == AssignmentKind.Module &&
                    s.GetColony(p.Assignment.ColonyId)?.ModuleAt(p.Assignment.Module) == null)
                {
                    p.Assignment = Assignment.None;
                }

                double dailyRisk = MonthlyRisk(s, p) / 30.0;
                double roll = s.Rng.NextDouble();
                if (roll < dailyRisk)
                {
                    Kill(s, p, AccidentCause(s, p));
                }
                else if (roll < dailyRisk * (1 + NearMissMultiplier))
                {
                    var faction = s.GetFaction(p.FactionId);
                    s.Report($"{faction.Name}: near miss! {p.Name} survived {AccidentCause(s, p)}.");
                    if (!p.Has(Trait.Survivor)) p.Traits.Add(Trait.Survivor);
                }

                // Experience: a year in the same role raises skill.
                if (p.Assignment.Kind != AssignmentKind.Unassigned && p.Skill < PioneerCatalog.MaxSkill &&
                    s.Day - p.AssignedDay >= DaysPerSkillLevel)
                {
                    p.Skill++;
                    p.AssignedDay = s.Day;
                    s.Report($"{p.Name} has grown in their role (skill {p.Skill}).");
                }
            }
        }

        static string AccidentCause(GameState s, Pioneer p)
        {
            var type = p.Assignment.Kind == AssignmentKind.Module
                ? s.GetColony(p.Assignment.ColonyId)?.ModuleAt(p.Assignment.Module)?.Type
                : null;
            switch (type)
            {
                case ModuleType.IceMine: return "a suit failure in the −230 °C cold trap";
                case ModuleType.MineralExtractor: return "a regolith collapse at the dig site";
                case ModuleType.FissionReactor: return "a radiation exposure incident";
                case ModuleType.SolarArray: return "a fall while servicing the arrays";
                default:
                    return s.Rng.NextDouble() < 0.5 ? "a habitat depressurisation" : "an unforecast solar particle storm";
            }
        }

        static void Kill(GameState s, Pioneer p, string cause)
        {
            p.IsAlive = false;
            p.DiedDay = s.Day;
            p.CauseOfDeath = cause;
            p.Assignment = Assignment.None;
            s.Revision++;
            s.Report($"{s.GetFaction(p.FactionId).Name} mourns {p.Name}, {p.Expertise.ToString().ToLowerInvariant()}, aged {p.Age(s)}: {cause}.");
        }

        /// <summary>A crowded, well-run colony occasionally produces a new Pioneer from its own crew.</summary>
        public static void RunMonth(GameState s)
        {
            foreach (var c in s.Colonies.Where(c => c.IsAlive && c.Pops >= 8))
            {
                if (s.Rng.NextDouble() >= 0.04) continue;
                var expertise = (Expertise)s.Rng.Next(4);
                var p = PioneerCatalog.Create(s, c.FactionId, c.Id, expertise, 1);
                s.Revision++;
                s.Report($"{c.Name}: {p.Name} has stood out among the crew and joins your Pioneers ({expertise.ToString().ToLowerInvariant()}).");
            }
        }
    }

    public sealed class AssignPioneer : ICommand
    {
        public readonly int FactionId;
        public readonly int PioneerId;
        public readonly Assignment Assignment;

        public AssignPioneer(int factionId, int pioneerId, Assignment assignment)
        {
            FactionId = factionId;
            PioneerId = pioneerId;
            Assignment = assignment;
        }

        public string Validate(GameState state)
        {
            var p = state.Pioneers.FirstOrDefault(x => x.Id == PioneerId);
            if (p == null || !p.IsAlive || p.FactionId != FactionId) return "No such Pioneer";
            if (p.Assignment.Equals(Assignment)) return "Already assigned there";

            var faction = state.GetFaction(FactionId);
            switch (Assignment.Kind)
            {
                case AssignmentKind.Module:
                {
                    var colony = state.GetColony(Assignment.ColonyId);
                    if (colony == null || colony.FactionId != FactionId) return "Not your colony";
                    var module = colony.ModuleAt(Assignment.Module);
                    if (module == null || !module.IsBuilt) return "No working module there";
                    if (PioneerCatalog.BestFor(module.Type) == null) return $"{module.Def.Name} has no use for a specialist";
                    var other = PioneerSystem.AtModule(state, colony, Assignment.Module);
                    if (other != null) return $"{other.Name} already works there";
                    return null;
                }
                case AssignmentKind.Administrator:
                {
                    var colony = state.GetColony(Assignment.ColonyId);
                    if (colony == null || colony.FactionId != FactionId) return "Not your colony";
                    var other = PioneerSystem.AdministratorOf(state, colony);
                    if (other != null) return $"{other.Name} already runs {colony.Name}";
                    return null;
                }
                case AssignmentKind.Council:
                {
                    var other = PioneerSystem.OnCouncil(state, faction, Assignment.Seat);
                    if (other != null) return $"{other.Name} already holds that seat";
                    return null;
                }
                default:
                    return null;
            }
        }

        public void Apply(GameState state)
        {
            var p = state.Pioneers.First(x => x.Id == PioneerId);
            p.Assignment = Assignment;
            p.AssignedDay = state.Day;
            if (Assignment.Kind == AssignmentKind.Module || Assignment.Kind == AssignmentKind.Administrator)
                p.HomeColonyId = Assignment.ColonyId;
            state.Revision++;
        }
    }
}
