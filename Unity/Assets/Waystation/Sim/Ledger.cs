using System;
using System.Collections.Generic;
using System.Linq;

namespace Waystation.Sim
{
    public enum LedgerLine
    {
        // Income
        LandingFees,
        FuelSales,
        Repairs,
        Refunds,
        // Expenses
        FuelPurchases,
        Upkeep,
        Construction,
    }

    /// <summary>Income and expenses for one game minute, by line.</summary>
    public sealed class LedgerMinute
    {
        public readonly long Minute;
        public readonly double[] Amounts = new double[Ledger.LineCount];

        public LedgerMinute(long minute)
        {
            Minute = minute;
        }

        public double Income => Ledger.IncomeLines.Sum(l => Amounts[(int)l]);
        public double Expenses => Ledger.ExpenseLines.Sum(l => Amounts[(int)l]);
        public double Net => Income - Expenses;
    }

    /// <summary>The station's books: lifetime totals per line plus a rolling per-minute history.</summary>
    public sealed class Ledger
    {
        public static readonly int LineCount = Enum.GetValues(typeof(LedgerLine)).Length;
        public static readonly LedgerLine[] IncomeLines = { LedgerLine.LandingFees, LedgerLine.FuelSales, LedgerLine.Repairs, LedgerLine.Refunds };
        public static readonly LedgerLine[] ExpenseLines = { LedgerLine.FuelPurchases, LedgerLine.Upkeep, LedgerLine.Construction };
        /// <summary>Minutes of history kept for charts.</summary>
        public const int HistoryMinutes = 30;

        public readonly double[] Totals = new double[LineCount];
        readonly List<LedgerMinute> _history = new List<LedgerMinute>();

        /// <summary>Oldest first; the last entry is the minute in progress.</summary>
        public IReadOnlyList<LedgerMinute> History => _history;

        public static bool IsIncome(LedgerLine line) => Array.IndexOf(IncomeLines, line) >= 0;

        public static string Name(LedgerLine line)
        {
            switch (line)
            {
                case LedgerLine.LandingFees: return "Landing fees";
                case LedgerLine.FuelSales: return "Fuel sales";
                case LedgerLine.Repairs: return "Repairs";
                case LedgerLine.Refunds: return "Salvage refunds";
                case LedgerLine.FuelPurchases: return "Fuel purchases";
                case LedgerLine.Upkeep: return "Upkeep";
                default: return "Construction";
            }
        }

        public void Record(long tick, LedgerLine line, double amount)
        {
            Totals[(int)line] += amount;
            Current(tick).Amounts[(int)line] += amount;
        }

        /// <summary>The bucket for the minute containing <paramref name="tick"/>, creating it (and trimming old ones).</summary>
        public LedgerMinute Current(long tick)
        {
            long minute = tick / (60 * Station.TicksPerSecond);
            if (_history.Count == 0 || _history[_history.Count - 1].Minute != minute)
            {
                _history.Add(new LedgerMinute(minute));
                if (_history.Count > HistoryMinutes) _history.RemoveAt(0);
            }
            return _history[_history.Count - 1];
        }

        /// <summary>Net result of the last complete minute (0 before the first minute ends).</summary>
        public double LastMinuteNet => _history.Count >= 2 ? _history[_history.Count - 2].Net : 0;
    }
}
