using System;
using System.Collections.Generic;

namespace Regolith.Sim
{
    /// <summary>
    /// Axial hex coordinate, pointy-top orientation.
    /// Reference: https://www.redblobgames.com/grids/hexagons/
    /// </summary>
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public readonly int Q;
        public readonly int R;
        public int S => -Q - R;

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        static readonly HexCoord[] Directions =
        {
            new HexCoord(1, 0), new HexCoord(1, -1), new HexCoord(0, -1),
            new HexCoord(-1, 0), new HexCoord(-1, 1), new HexCoord(0, 1),
        };

        public static HexCoord operator +(HexCoord a, HexCoord b) => new HexCoord(a.Q + b.Q, a.R + b.R);
        public static HexCoord operator -(HexCoord a, HexCoord b) => new HexCoord(a.Q - b.Q, a.R - b.R);
        public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
        public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);

        /// <summary>Direction 0..5, counter-clockwise starting east.</summary>
        public HexCoord Neighbor(int direction) => this + Directions[direction];

        public IEnumerable<HexCoord> Neighbors()
        {
            for (int i = 0; i < 6; i++)
                yield return Neighbor(i);
        }

        public int DistanceTo(HexCoord other)
        {
            var d = this - other;
            return (Math.Abs(d.Q) + Math.Abs(d.R) + Math.Abs(d.S)) / 2;
        }

        /// <summary>"Odd-r" offset (col, row) → axial. Used to lay out rectangular maps.</summary>
        public static HexCoord FromOffset(int col, int row) => new HexCoord(col - (row - (row & 1)) / 2, row);

        public (int col, int row) ToOffset() => (Q + (R - (R & 1)) / 2, R);

        /// <summary>Round fractional axial coordinates to the nearest hex.</summary>
        public static HexCoord Round(double q, double r)
        {
            double s = -q - r;
            double rq = Math.Round(q), rr = Math.Round(r), rs = Math.Round(s);
            double dq = Math.Abs(rq - q), dr = Math.Abs(rr - r), ds = Math.Abs(rs - s);

            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;

            return new HexCoord((int)rq, (int)rr);
        }

        public bool Equals(HexCoord other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => unchecked((Q * 397) ^ R);
        public override string ToString() => $"({Q}, {R})";
    }

    /// <summary>Converts between hex coordinates and a 2D plane (X right, Y "down"/forward).</summary>
    public static class HexLayout
    {
        static readonly double Sqrt3 = Math.Sqrt(3.0);

        public static (double x, double y) ToPlane(HexCoord c, double size) =>
            (size * (Sqrt3 * c.Q + Sqrt3 / 2.0 * c.R), size * (1.5 * c.R));

        public static HexCoord FromPlane(double x, double y, double size)
        {
            double q = (Sqrt3 / 3.0 * x - 1.0 / 3.0 * y) / size;
            double r = (2.0 / 3.0 * y) / size;
            return HexCoord.Round(q, r);
        }

        /// <summary>Offset of corner i (0..5) from the hex centre.</summary>
        public static (double x, double y) CornerOffset(int i, double size)
        {
            double angle = Math.PI / 180.0 * (60.0 * i - 30.0);
            return (size * Math.Cos(angle), size * Math.Sin(angle));
        }
    }
}
