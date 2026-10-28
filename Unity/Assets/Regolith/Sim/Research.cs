using System;
using System.Collections.Generic;
using System.Linq;

namespace Regolith.Sim
{
    public enum TechId
    {
        RegolithSintering,
        BatteryStorage,
        IceProspecting,
        ControlledAgriculture,
        ClosedLoopLifeSupport,
        KilopowerReactors,
        AdditiveConstruction,
    }

    public enum TechTier
    {
        Proven,
        Emerging,
        Speculative,
        Unknown,
    }

    public sealed class TechDef
    {
        public TechId Id;
        public TechTier Tier;
        public string Name;
        public string Description;
        public double Cost;
        public TechId[] Prerequisites = new TechId[0];
        /// <summary>Civ-style boost: doing this in play cuts the remaining cost.</summary>
        public string BoostText;
        public Func<GameState, Faction, bool> BoostCondition;
    }

    /// <summary>
    /// The tech tree. Act I is all Proven tier: real engineering that exists today.
    /// Effects live where they apply (<see cref="ModuleDef.RequiredTech"/>, <see cref="Modifiers"/>).
    /// </summary>
    public static class TechCatalog
    {
        public const double BoostDiscount = 0.4;

        static readonly Dictionary<TechId, TechDef> Defs = new Dictionary<TechId, TechDef>();

        static TechCatalog()
        {
            Add(new TechDef
            {
                Id = TechId.RegolithSintering, Name = "Regolith Sintering", Cost = 150,
                Description = "Fuse lunar soil into bricks and metal feedstock. Unlocks the Regolith Foundry.",
                BoostText = "Have 4 modules operating",
                BoostCondition = (s, f) => s.ColoniesOf(f).Any(c => c.Modules.Count(m => m.IsBuilt) >= 4),
            });
            Add(new TechDef
            {
                Id = TechId.BatteryStorage, Name = "Battery Storage", Cost = 200,
                Description = "Store daylight power for the two-week night. Unlocks the Battery Bank.",
                BoostText = "Suffer a power shortage",
                BoostCondition = (s, f) => s.ColoniesOf(f).Any(c => c.HadBrownout),
            });
            Add(new TechDef
            {
                Id = TechId.IceProspecting, Name = "Ice Prospecting", Cost = 200,
                Description = "Neutron spectrometers find the richest ice. Ice Mines produce 30% more.",
                BoostText = "Operate an Ice Mine",
                BoostCondition = (s, f) => HasBuilt(s, f, ModuleType.IceMine),
            });
            Add(new TechDef
            {
                Id = TechId.ControlledAgriculture, Name = "Controlled-Environment Agriculture", Cost = 250,
                Description = "Tuned LED spectra and nutrient films. Greenhouses produce 25% more food.",
                BoostText = "Operate a Greenhouse",
                BoostCondition = (s, f) => HasBuilt(s, f, ModuleType.Greenhouse),
            });
            Add(new TechDef
            {
                Id = TechId.ClosedLoopLifeSupport, Name = "Closed-Loop Life Support", Cost = 250,
                Description = "Recover water from air, urine and sweat. Crew use 25% less water.",
                BoostText = "Reach 8 crew",
                BoostCondition = (s, f) => s.ColoniesOf(f).Any(c => c.Pops >= 8),
            });
            Add(new TechDef
            {
                Id = TechId.KilopowerReactors, Name = "Kilopower Reactors", Cost = 300,
                Prerequisites = new[] { TechId.RegolithSintering },
                Description = "Compact fission reactors for steady power day and night. Unlocks the Fission Reactor.",
                BoostText = "Survive a full lunar night",
                BoostCondition = (s, f) => s.ColoniesOf(f).Any(c => c.IsAlive && s.Day - c.FoundedDay >= 30),
            });
            Add(new TechDef
            {
                Id = TechId.AdditiveConstruction, Name = "Additive Construction", Cost = 350,
                Prerequisites = new[] { TechId.RegolithSintering },
                Description = "Robots 3D-print structures from sintered regolith. All construction is 25% faster.",
                BoostText = "Operate a Regolith Foundry",
                BoostCondition = (s, f) => HasBuilt(s, f, ModuleType.Foundry),
            });
        }

        static void Add(TechDef def)
        {
            def.Tier = TechTier.Proven;
            Defs[def.Id] = def;
        }

        static bool HasBuilt(GameState s, Faction f, ModuleType type) =>
            s.ColoniesOf(f).Any(c => c.Modules.Any(m => m.IsBuilt && m.Type == type));

        public static TechDef Get(TechId id) => Defs[id];
        public static IEnumerable<TechDef> All => Defs.Values;
    }

    /// <summary>Gameplay effects of researched techs, queried where they apply.</summary>
    public static class Modifiers
    {
        public static double OutputMultiplier(Faction f, ModuleType type)
        {
            if (type == ModuleType.IceMine && f.Has(TechId.IceProspecting)) return 1.3;
            if (type == ModuleType.Greenhouse && f.Has(TechId.ControlledAgriculture)) return 1.25;
            return 1.0;
        }

        public static double CrewUse(Faction f, Resource r) =>
            r == Resource.Water && f.Has(TechId.ClosedLoopLifeSupport) ? 0.75 : 1.0;

        public static int BuildDays(Faction f, ModuleDef def) =>
            f.Has(TechId.AdditiveConstruction) ? (int)Math.Ceiling(def.BuildDays * 0.75) : def.BuildDays;
    }

    public static class ResearchSystem
    {
        public static double EffectiveCost(Faction f, TechId id)
        {
            var cost = TechCatalog.Get(id).Cost;
            return f.Boosted.Contains(id) ? cost * (1.0 - TechCatalog.BoostDiscount) : cost;
        }

        /// <summary>Techs whose prerequisites are met and that aren't researched yet.</summary>
        public static IEnumerable<TechDef> Available(Faction f) =>
            TechCatalog.All.Where(t => !f.Has(t.Id) && t.Prerequisites.All(f.Has));

        public static void AddScience(GameState state, Faction f, double amount)
        {
            if (f.CurrentResearch == null)
            {
                f.ScienceBank += amount;
                return;
            }

            var id = f.CurrentResearch.Value;
            f.Progress.TryGetValue(id, out var progress);
            progress += amount;

            double cost = EffectiveCost(f, id);
            if (progress < cost)
            {
                f.Progress[id] = progress;
                return;
            }

            f.Progress.Remove(id);
            f.Researched.Add(id);
            f.CurrentResearch = null;
            f.ScienceBank += progress - cost; // overflow carries into the next pick
            state.Revision++;
            state.Report($"{f.Name}: research complete: {TechCatalog.Get(id).Name}.");
        }

        public static void CheckBoosts(GameState state)
        {
            foreach (var f in state.Factions)
            foreach (var tech in TechCatalog.All)
            {
                if (f.Has(tech.Id) || f.Boosted.Contains(tech.Id) || tech.BoostCondition == null) continue;
                if (!tech.BoostCondition(state, f)) continue;

                f.Boosted.Add(tech.Id);
                state.Report($"{f.Name}: breakthrough! {tech.BoostText.ToLowerInvariant()} boosts {tech.Name} (−{TechCatalog.BoostDiscount:P0} cost).");

                if (f.CurrentResearch == tech.Id) AddScience(state, f, 0); // may complete immediately
            }
        }
    }

    public sealed class SetResearch : ICommand
    {
        public readonly int FactionId;
        public readonly TechId Tech;

        public SetResearch(int factionId, TechId tech)
        {
            FactionId = factionId;
            Tech = tech;
        }

        public string Validate(GameState state)
        {
            var f = state.GetFaction(FactionId);
            if (f == null) return "Unknown faction";
            if (f.Has(Tech)) return "Already researched";

            var missing = TechCatalog.Get(Tech).Prerequisites.Where(p => !f.Has(p)).ToList();
            if (missing.Count > 0) return "Requires " + string.Join(", ", missing.Select(p => TechCatalog.Get(p).Name));
            return null;
        }

        public void Apply(GameState state)
        {
            var f = state.GetFaction(FactionId);
            f.CurrentResearch = Tech;
            double banked = f.ScienceBank;
            f.ScienceBank = 0;
            if (banked > 0) ResearchSystem.AddScience(state, f, banked);
        }
    }
}
