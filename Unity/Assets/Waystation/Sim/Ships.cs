using System;
using System.Collections.Generic;

namespace Waystation.Sim
{
    public enum ShipState
    {
        /// <summary>Flying in from orbit.</summary>
        Approaching,
        /// <summary>Calling the station for a landing spot while circling. Patience runs down until it's assigned one.</summary>
        Hailing,
        Landing,
        /// <summary>On the pad: refuelling, then repairs if needed.</summary>
        Servicing,
        Departing,
        Gone,
    }

    public sealed class Ship
    {
        public int Id;
        public string Name;
        public ShipClass Class;
        /// <summary>Paint scheme index, for the view.</summary>
        public int Livery;
        public ShipState State;
        /// <summary>Ticks left in the current timed state (approach, landing, repair, departure).</summary>
        public int StateTicks;
        /// <summary>Length of the current timed state, so views can show progress.</summary>
        public int StateDuration;
        public int PatienceTicks;
        public int MaxPatienceTicks;
        public int? BuildingId;
        public double FuelNeeded;
        public double FuelLoaded;
        public bool NeedsRepair;
        public bool Repaired;
        /// <summary>Left without being served; hurts reputation.</summary>
        public bool Angry;
        public long ArrivedTick;
        /// <summary>When the ship started hailing, to measure how long clearance took.</summary>
        public long HailedTick;

        public ShipRole Role;
        /// <summary>People aboard (crew and passengers) who can go out onto the station.</summary>
        public int Crew = 1;
        /// <summary>What they want from the station: the need first (if any), then wishes.</summary>
        public List<Errand> Errands = new List<Errand>();
        /// <summary>The crew has gone out (once, on docking).</summary>
        public bool Disembarked;

        /// <summary>0 at the start of the current timed state, 1 at its end.</summary>
        public double StateProgress => StateDuration <= 0 ? 1.0 : 1.0 - StateTicks / (double)StateDuration;

        public void Enter(ShipState state, int ticks)
        {
            State = state;
            StateTicks = ticks;
            StateDuration = ticks;
        }
    }

    public sealed class ShipClassDef
    {
        public ShipClass Class;
        public string Name;
        public double MinFuel;
        public double MaxFuel;
        public double LandingFee;
        public double RepairChance;
        public double RepairFee;
        public double PatienceSeconds;
        public double ReputationOnServed;
    }

    public static class ShipCatalog
    {
        public static ShipClassDef Get(ShipClass c) => c == ShipClass.Shuttle ? Shuttle : Hauler;

        static readonly ShipClassDef Shuttle = new ShipClassDef
        {
            Class = ShipClass.Shuttle, Name = "Shuttle", MinFuel = 20, MaxFuel = 45, LandingFee = 25,
            RepairChance = 0, RepairFee = 0, PatienceSeconds = 60, ReputationOnServed = 0.6,
        };

        static readonly ShipClassDef Hauler = new ShipClassDef
        {
            Class = ShipClass.Hauler, Name = "Hauler", MinFuel = 70, MaxFuel = 130, LandingFee = 80,
            RepairChance = 0.45, RepairFee = 250, PatienceSeconds = 90, ReputationOnServed = 1.2,
        };

        static readonly string[] Prefixes =
        {
            "Ashen", "Rusted", "Silent", "Crimson", "Grim", "Hollow", "Pale", "Bleak", "Iron", "Broken",
            "Weeping", "Distant", "Cinder", "Fallen", "Gaunt", "Last", "Black", "Sorrowful", "Bitter", "Grave",
        };

        static readonly string[] Nouns =
        {
            "Penitent", "Pilgrim", "Mule", "Wretch", "Lantern", "Requiem", "Anvil", "Vigil", "Carrion", "Drifter",
            "Gallows", "Reliquary", "Mourner", "Ox", "Scavenger", "Censer", "Martyr", "Hulk", "Tithe", "Exile",
        };

        static readonly string[] Navy = { "Resolute", "Vigilant", "Bulwark", "Sentinel", "Halberd", "Steadfast", "Rampart", "Aegis", "Warden", "Lance" };
        static readonly string[] Guild = { "Far Horizon", "Long Dark", "Wayfinder", "Cartographer", "Lantern", "Pathless", "Deep Survey", "Last Light" };
        static readonly string[] Corps = { "Halvorsen-Kade", "Orbis Mercantile", "Tessaract Holdings", "Vantablack Ltd", "Meridian Group", "Kestrel Dynamics" };

        /// <summary>Grim registry names: "Ashen Penitent KR3", "USMS Bulwark 4471", "EGV Wayfinder 12".</summary>
        public static string RandomName(Random rng, ShipRole role = ShipRole.Traveller)
        {
            char a = (char)('A' + rng.Next(26)), b = (char)('A' + rng.Next(26));
            switch (role)
            {
                case ShipRole.Military: return $"USMS {Navy[rng.Next(Navy.Length)]} {rng.Next(1000, 9999)}";
                case ShipRole.Explorer: return $"EGV {Guild[rng.Next(Guild.Length)]} {rng.Next(2, 99)}";
                case ShipRole.Executive: return $"{Corps[rng.Next(Corps.Length)]} {a}{b}-{rng.Next(1, 10)}";
                default: return $"{Prefixes[rng.Next(Prefixes.Length)]} {Nouns[rng.Next(Nouns.Length)]} {a}{b}{rng.Next(1, 10)}";
            }
        }
    }
}
