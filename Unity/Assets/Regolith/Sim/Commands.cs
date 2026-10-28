using System.Linq;

namespace Regolith.Sim
{
    /// <summary>
    /// Everything a player or AI does goes through a command. That keeps the sim deterministic
    /// and gives us replays, save files and (later) multiplayer for free.
    /// </summary>
    public interface ICommand
    {
        /// <summary>Why the command can't run right now, or null if it can.</summary>
        string Validate(GameState state);

        void Apply(GameState state);
    }

    public sealed class FoundColony : ICommand
    {
        public const int ClaimRadius = 2;
        public const int MinDistanceBetweenColonies = 6;
        public const int StartingPops = 4;

        public readonly int FactionId;
        public readonly HexCoord At;
        public readonly string Name;

        public FoundColony(int factionId, HexCoord at, string name)
        {
            FactionId = factionId;
            At = at;
            Name = name;
        }

        public string Validate(GameState state)
        {
            var tile = state.Map.GetTile(At);
            if (tile == null) return "Outside the map";
            if (state.GetFaction(FactionId) == null) return "Unknown faction";
            if (state.Colonies.Any(c => c.FactionId == FactionId)) return "You already have a base (more colonies come later)";
            if (tile.Terrain == TerrainType.ShadowedCrater) return "Too cold and dark to land here";

            var tooClose = state.Colonies.FirstOrDefault(c => c.Center.DistanceTo(At) < MinDistanceBetweenColonies);
            if (tooClose != null) return $"Too close to {tooClose.Name}";

            return null;
        }

        public void Apply(GameState state)
        {
            var colony = new Colony
            {
                Id = state.NextColonyId++,
                Name = Name,
                FactionId = FactionId,
                Center = At,
                Pops = StartingPops,
                FoundedDay = state.Day,
            };

            foreach (var c in At.WithinRange(ClaimRadius))
                if (state.Map.Contains(c) && state.ColonyOwning(c) == null)
                    colony.Territory.Add(c);

            colony.Modules.Add(new Module(ModuleType.HabCore, At, 0));
            colony.Stock[Resource.Oxygen] = 150;
            colony.Stock[Resource.Water] = 150;
            colony.Stock[Resource.Food] = 300;
            colony.Stock[Resource.Materials] = 200;

            state.Colonies.Add(colony);
            state.Revision++;
            state.Report($"{Name} has landed. {StartingPops} crew on the surface.");
        }
    }

    public sealed class PlaceModule : ICommand
    {
        public readonly int ColonyId;
        public readonly HexCoord At;
        public readonly ModuleType Type;

        public PlaceModule(int colonyId, HexCoord at, ModuleType type)
        {
            ColonyId = colonyId;
            At = at;
            Type = type;
        }

        public string Validate(GameState state)
        {
            var colony = state.GetColony(ColonyId);
            if (colony == null || !colony.IsAlive) return "No such colony";
            if (!colony.Territory.Contains(At)) return "Outside colony territory";

            var def = ModuleCatalog.Get(Type);
            if (!def.Buildable) return $"{def.Name} can't be built";
            if (def.RequiredTech.HasValue && !state.GetFaction(colony.FactionId).Has(def.RequiredTech.Value))
                return $"Requires {TechCatalog.Get(def.RequiredTech.Value).Name}";
            if (colony.ModuleAt(At) != null) return "Tile already has a module";

            var placementError = ModuleCatalog.PlacementError(Type, state.Map.GetTile(At));
            if (placementError != null) return placementError;

            if (colony.Stock[Resource.Materials] < def.MaterialCost) return $"Needs {def.MaterialCost} materials";
            return null;
        }

        public void Apply(GameState state)
        {
            var colony = state.GetColony(ColonyId);
            var def = ModuleCatalog.Get(Type);
            colony.Stock[Resource.Materials] -= def.MaterialCost;
            int days = Modifiers.BuildDays(state.GetFaction(colony.FactionId), def);
            colony.Modules.Add(new Module(Type, At, days));
            state.Revision++;
            state.Report($"{colony.Name}: construction of {def.Name} queued ({days} days).");
        }
    }
}
