namespace Regolith.Sim
{
    public enum BodyId
    {
        Moon,
        Mars,
    }

    /// <summary>Physical differences between worlds. Gameplay-tuned, but grounded in the real numbers.</summary>
    public static class Bodies
    {
        /// <summary>Sunrise to sunrise on the Moon, in Earth days.</summary>
        public const double LunarSynodicDays = 29.53;

        public static string Name(BodyId body) => body == BodyId.Moon ? "Luna" : "Mars";

        public static bool IsLunarDaylight(int day) => day % LunarSynodicDays < LunarSynodicDays / 2;

        /// <summary>Fraction (0..1) of a solar array's rated output on this tile on this day.</summary>
        public static double SolarAvailability(BodyId body, Tile tile, int day)
        {
            if (tile.Terrain == TerrainType.ShadowedCrater) return 0.0;

            switch (body)
            {
                case BodyId.Moon:
                    if (tile.Feature == TileFeature.PeakOfLight) return 0.9;
                    return IsLunarDaylight(day) ? 1.0 : 0.0;
                default:
                    return 0.45; // Mars gets ~43% of Earth's sunlight, and dust settles on panels
            }
        }
    }
}
