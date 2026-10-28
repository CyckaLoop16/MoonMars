using System;
using System.Collections.Generic;
using System.Linq;

namespace Regolith.Sim
{
    public sealed class Faction
    {
        public int Id;
        public string Name;
        /// <summary>sRGB hex colour for territory.</summary>
        public string Color;
    }

    public sealed class Module
    {
        public ModuleType Type { get; }
        public HexCoord Coord { get; }
        public int BuildDaysRemaining;
        public int Workers;
        /// <summary>0..1 output achieved on the last simulated day, for the UI.</summary>
        public double Efficiency;

        public Module(ModuleType type, HexCoord coord, int buildDays)
        {
            Type = type;
            Coord = coord;
            BuildDaysRemaining = buildDays;
        }

        public ModuleDef Def => ModuleCatalog.Get(Type);
        public bool IsBuilt => BuildDaysRemaining <= 0;
    }

    /// <summary>What happened in a colony on the last simulated day, for the UI.</summary>
    public sealed class ColonyReport
    {
        public readonly double[] Net = new double[Stockpile.Count];
        public double EnergySupply;
        public double EnergyDemand;
        public int Jobs;
        public int Workers;

        public double NetOf(Resource r) => Net[(int)r];
    }

    public sealed class Colony
    {
        public int Id;
        public string Name;
        public int FactionId;
        public HexCoord Center;
        public int Pops;
        public readonly HashSet<HexCoord> Territory = new HashSet<HexCoord>();
        public readonly List<Module> Modules = new List<Module>();
        public readonly Stockpile Stock = new Stockpile();
        /// <summary>Consecutive days each resource has run out.</summary>
        public readonly int[] ShortageDays = new int[Stockpile.Count];
        public ColonyReport LastReport = new ColonyReport();

        public bool IsAlive => Pops > 0;
        public int Housing => Modules.Where(m => m.IsBuilt).Sum(m => m.Def.Housing);
        public Module ModuleAt(HexCoord c) => Modules.FirstOrDefault(m => m.Coord == c);
        public Module UnderConstruction => Modules.FirstOrDefault(m => !m.IsBuilt);
    }

    public readonly struct GameEvent
    {
        public readonly int Day;
        public readonly string Text;

        public GameEvent(int day, string text)
        {
            Day = day;
            Text = text;
        }
    }

    public sealed class GameState
    {
        public static readonly DateTime StartDate = new DateTime(2040, 1, 1);

        public int Day;
        public readonly WorldMap Map;
        public readonly List<Faction> Factions = new List<Faction>();
        public readonly List<Colony> Colonies = new List<Colony>();
        public readonly List<GameEvent> Log = new List<GameEvent>();
        /// <summary>Bumped whenever something the map shows changes (territory, modules), so views know to rebuild.</summary>
        public int Revision;
        public int NextColonyId = 1;

        public GameState(WorldMap map)
        {
            Map = map;
        }

        public DateTime Date => StartDate.AddDays(Day);
        public BodyId Body => Map.Body;

        public Faction GetFaction(int id) => Factions.FirstOrDefault(f => f.Id == id);
        public Colony GetColony(int id) => Colonies.FirstOrDefault(c => c.Id == id);
        public Colony ColonyOwning(HexCoord c) => Colonies.FirstOrDefault(col => col.Territory.Contains(c));

        public void Report(string text) => Log.Add(new GameEvent(Day, text));
    }
}
