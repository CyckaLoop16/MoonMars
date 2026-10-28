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
    }

    public static class GameSetup
    {
        public const int LunarMapWidth = 40;
        public const int LunarMapHeight = 28;
        public const int PlayerFactionId = 1;

        public static Simulation NewLunarGame(int seed)
        {
            var state = new GameState(LunarMapGenerator.Generate(LunarMapWidth, LunarMapHeight, seed));
            state.Factions.Add(new Faction { Id = PlayerFactionId, Name = "Atlantic Accord", Color = "#4f8fe6" });
            state.Report("Landing approved. Choose a site for the first base: near ice, near sunlight.");
            return new Simulation(state);
        }
    }
}
