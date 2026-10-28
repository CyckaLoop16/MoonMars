using System;

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
        /// <summary>What the crew spends in a pilot lounge while docked.</summary>
        public double ServicesFee;
    }

    public static class ShipCatalog
    {
        public static ShipClassDef Get(ShipClass c) => c == ShipClass.Shuttle ? Shuttle : Hauler;

        static readonly ShipClassDef Shuttle = new ShipClassDef
        {
            Class = ShipClass.Shuttle, Name = "Shuttle", MinFuel = 20, MaxFuel = 45, LandingFee = 25,
            RepairChance = 0, RepairFee = 0, PatienceSeconds = 60, ReputationOnServed = 0.6, ServicesFee = 20,
        };

        static readonly ShipClassDef Hauler = new ShipClassDef
        {
            Class = ShipClass.Hauler, Name = "Hauler", MinFuel = 70, MaxFuel = 130, LandingFee = 80,
            RepairChance = 0.45, RepairFee = 250, PatienceSeconds = 90, ReputationOnServed = 1.2, ServicesFee = 45,
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

        /// <summary>Grim registry names: "Ashen Penitent KR3".</summary>
        public static string RandomName(Random rng)
        {
            char a = (char)('A' + rng.Next(26)), b = (char)('A' + rng.Next(26));
            return $"{Prefixes[rng.Next(Prefixes.Length)]} {Nouns[rng.Next(Nouns.Length)]} {a}{b}{rng.Next(1, 10)}";
        }
    }
}
