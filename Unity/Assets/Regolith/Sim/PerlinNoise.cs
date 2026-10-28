using System;

namespace Regolith.Sim
{
    /// <summary>
    /// Seeded 2D Perlin noise. Lives in the sim (not UnityEngine.Mathf.PerlinNoise)
    /// so map generation is deterministic and testable outside Unity.
    /// </summary>
    public sealed class PerlinNoise
    {
        readonly int[] _perm = new int[512];

        public PerlinNoise(int seed)
        {
            var p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;

            var rng = new Random(seed);
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (p[i], p[j]) = (p[j], p[i]);
            }

            for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
        }

        /// <summary>Single octave, roughly in [-1, 1].</summary>
        public double Sample(double x, double y)
        {
            double fx = Math.Floor(x), fy = Math.Floor(y);
            int xi = (int)fx & 255, yi = (int)fy & 255;
            double xf = x - fx, yf = y - fy;
            double u = Fade(xf), v = Fade(yf);

            int aa = _perm[_perm[xi] + yi];
            int ab = _perm[_perm[xi] + yi + 1];
            int ba = _perm[_perm[xi + 1] + yi];
            int bb = _perm[_perm[xi + 1] + yi + 1];

            double x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
            double x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
            return Lerp(x1, x2, v);
        }

        /// <summary>Fractal Brownian motion: several octaves summed, roughly in [-1, 1].</summary>
        public double Fbm(double x, double y, int octaves = 4, double lacunarity = 2.0, double gain = 0.5)
        {
            double sum = 0, amplitude = 1, frequency = 1, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += Sample(x * frequency, y * frequency) * amplitude;
                norm += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }
            return sum / norm;
        }

        static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
        static double Lerp(double a, double b, double t) => a + (b - a) * t;

        static double Grad(int hash, double x, double y)
        {
            switch (hash & 7)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }
    }
}
