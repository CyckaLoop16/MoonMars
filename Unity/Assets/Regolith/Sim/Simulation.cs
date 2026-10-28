using System.Collections.Generic;

namespace Regolith.Sim
{
    /// <summary>
    /// Drives the game: executes commands and advances time one day per tick.
    /// The view decides how many ticks run per real second (pause / speeds); the sim never reads the clock.
    /// </summary>
    public sealed class Simulation
    {
        readonly List<(int Day, ICommand Command)> _commandLog = new List<(int, ICommand)>();

        public GameState State { get; }

        /// <summary>Every executed command with the day it ran. Replaying it from the same seed reproduces the game.</summary>
        public IReadOnlyList<(int Day, ICommand Command)> CommandLog => _commandLog;

        public Simulation(GameState state)
        {
            State = state;
        }

        public bool Execute(ICommand command, out string error)
        {
            error = command.Validate(State);
            if (error != null) return false;

            command.Apply(State);
            _commandLog.Add((State.Day, command));
            return true;
        }

        public void Tick()
        {
            State.Day++;
            Economy.RunDay(State);
            if (State.Date.Day == 1) Economy.RunMonth(State);
        }

        public void Advance(int days)
        {
            for (int i = 0; i < days; i++) Tick();
        }

        /// <summary>Turn-based mode: one turn runs the daily sim until the 1st of the next month.</summary>
        public void AdvanceToNextMonth()
        {
            do Tick(); while (State.Date.Day != 1);
        }

        /// <summary>Turn number in turn-based mode (1 = January 2040).</summary>
        public int Turn => (State.Date.Year - GameState.StartDate.Year) * 12 + State.Date.Month;
    }

    public enum MapSize
    {
        Small,
        Standard,
        Large,
        Huge,
    }

    public static class GameSetup
    {
        public const int PlayerFactionId = 1;

        /// <summary>Map dimensions in hexes (3 km each). Sized so 3–6 factions fit as the design calls for.</summary>
        public static (int width, int height) Dimensions(MapSize size)
        {
            switch (size)
            {
                case MapSize.Small: return (64, 44);
                case MapSize.Standard: return (96, 64);
                case MapSize.Large: return (128, 84);
                default: return (168, 110);
            }
        }

        public static Simulation NewLunarGame(int seed, MapSize size = MapSize.Large)
        {
            var (width, height) = Dimensions(size);
            var state = new GameState(LunarMapGenerator.Generate(width, height, seed));
            state.Factions.Add(new Faction { Id = PlayerFactionId, Name = "Atlantic Accord", Color = "#4f8fe6" });
            state.Report("Landing approved. Choose a site for the first base: near ice, near sunlight.");
            return new Simulation(state);
        }
    }
}
