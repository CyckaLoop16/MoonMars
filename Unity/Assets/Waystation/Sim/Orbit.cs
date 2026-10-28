using System;

namespace Waystation.Sim
{
    /// <summary>
    /// The station circles the planet below. For most of each orbit it's in sunlight; for the rest the
    /// planet blocks the sun (the eclipse). Sunrise and sunset are quick, as they are in low orbit.
    /// Pure function of the tick, so it's deterministic and saves for free.
    /// </summary>
    public static class Orbit
    {
        public const double PeriodSeconds = 360;
        /// <summary>Share of each orbit spent in the planet's shadow.</summary>
        public const double ShadowFraction = 0.35;
        /// <summary>How long the sun takes to slip behind the planet's limb (or climb out from behind it).</summary>
        public const double TwilightSeconds = 10;
        /// <summary>A new station starts in mid-morning, so the first eclipse arrives after a few minutes.</summary>
        public const double StartPhase = 0.12;

        public static double DaySeconds => PeriodSeconds * (1 - ShadowFraction);

        /// <summary>0..1 around the orbit; 0 is sunrise, <c>1 - ShadowFraction</c> is sunset.</summary>
        public static double Phase(long tick)
        {
            double p = tick / (PeriodSeconds * Station.TicksPerSecond) + StartPhase;
            return p - Math.Floor(p);
        }

        /// <summary>How much sunlight reaches the station: 1 in full sun, 0 in shadow, eased in between.</summary>
        public static double SunExposure(long tick)
        {
            double t = Phase(tick) * PeriodSeconds;
            double half = TwilightSeconds / 2;
            if (t > PeriodSeconds - half) t -= PeriodSeconds; // just before sunrise
            if (t < half) return Smooth((t + half) / TwilightSeconds);
            if (t < DaySeconds - half) return 1;
            if (t < DaySeconds + half) return 1 - Smooth((t - (DaySeconds - half)) / TwilightSeconds);
            return 0;
        }

        public static bool IsDay(long tick) => SunExposure(tick) >= 0.5;

        /// <summary>Seconds until the next sunset (if it's day) or sunrise (if it's night).</summary>
        public static double SecondsToChange(long tick, out bool sunset)
        {
            double t = Phase(tick) * PeriodSeconds;
            sunset = t < DaySeconds;
            return sunset ? DaySeconds - t : PeriodSeconds - t;
        }

        static double Smooth(double x)
        {
            x = Math.Max(0, Math.Min(1, x));
            return x * x * (3 - 2 * x);
        }
    }
}
