using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>The ledger (M): income vs expenses per minute, and the books by line.</summary>
    public sealed class LedgerWindow
    {
        const int ChartMinutes = 15;
        const float RefreshSeconds = 0.5f;

        readonly GameController _game;
        readonly VisualElement _chart;
        readonly List<(VisualElement income, VisualElement expense)> _columns = new List<(VisualElement, VisualElement)>();
        readonly Dictionary<LedgerLine, (Label now, Label last, Label total)> _lines = new Dictionary<LedgerLine, (Label, Label, Label)>();
        readonly Label _netNow, _netLast, _netTotal, _chartScale;
        float _nextRefresh;

        public VisualElement Root { get; }

        public LedgerWindow(GameController game, UiRoot ui)
        {
            _game = game;
            Root = Ui.El("ws-panel", "ws-window");

            var header = Ui.El("ws-window-header").AddTo(Root);
            Ui.Heading("LEDGER").AddTo(header).AddToClassList("ws-grow");
            Ui.Button("×", () => ui.CloseWindow()).AddTo(header);

            var chartHeader = Ui.El("ws-row").AddTo(Root);
            Ui.Text($"LAST {ChartMinutes} MINUTES", "ws-subheading", "ws-grow").AddTo(chartHeader);
            Ui.Text("■ income", "ws-small", "ws-good").AddTo(chartHeader).style.marginRight = 8;
            Ui.Text("■ expenses", "ws-small", "ws-bad").AddTo(chartHeader);
            _chartScale = Ui.Text("", "ws-small", "ws-muted").AddTo(Root);
            _chart = Ui.El("ws-chart").AddTo(Root);
            for (int i = 0; i < ChartMinutes; i++)
            {
                var column = Ui.El("ws-chart-column").AddTo(_chart);
                _columns.Add((Ui.El("ws-chart-in").AddTo(column), Ui.El("ws-chart-out").AddTo(column)));
            }
            ui.Tooltip(_chart, () => "Each pair of bars is one game minute; the rightmost is the minute in progress.");

            TableRow("", "THIS MIN", "LAST MIN", "LIFETIME", "ws-stat-label");
            Ui.Text("INCOME", "ws-subheading").AddTo(Root);
            foreach (var line in Ledger.IncomeLines) _lines[line] = TableRow(Ledger.Name(line), "", "", "", null);
            Ui.Text("EXPENSES", "ws-subheading").AddTo(Root);
            foreach (var line in Ledger.ExpenseLines) _lines[line] = TableRow(Ledger.Name(line), "", "", "", null);
            var rule = Ui.El().AddTo(Root);
            rule.style.height = 1;
            rule.style.marginTop = rule.style.marginBottom = 4;
            rule.style.backgroundColor = new Color(0.79f, 0.54f, 0.17f, 0.3f);
            (_netNow, _netLast, _netTotal) = TableRow("Net", "", "", "", null);
        }

        (Label, Label, Label) TableRow(string name, string a, string b, string c, string cls)
        {
            var row = Ui.El("ws-table-row").AddTo(Root);
            var first = Ui.Text(name, "ws-small", "ws-grow").AddTo(row);
            if (cls != null) first.AddToClassList(cls);
            Label Cell(string text)
            {
                var l = Ui.Text(text, "ws-small").AddTo(row);
                if (cls != null) l.AddToClassList(cls);
                l.style.width = 110;
                l.style.unityTextAlign = TextAnchor.MiddleRight;
                return l;
            }
            return (Cell(a), Cell(b), Cell(c));
        }

        public void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshSeconds;

            var s = _game.Station;
            var ledger = s.Ledger;
            var history = ledger.History;
            // Read-only: the view never creates buckets. Before any money moves this minute there's no bucket yet.
            long minute = s.Tick / (60 * Station.TicksPerSecond);
            var current = history.Count > 0 && history[history.Count - 1].Minute == minute ? history[history.Count - 1] : null;
            var last = history.LastOrDefault(m => m.Minute == minute - 1);

            // Chart: the last ChartMinutes buckets, right-aligned, scaled to the biggest bar.
            var shown = history.Skip(Math.Max(0, history.Count - ChartMinutes)).ToList();
            double max = Math.Max(1, shown.Select(m => Math.Max(m.Income, m.Expenses)).DefaultIfEmpty(0).Max());
            Ui.Set(_chartScale, $"scale: {max:N0} cr");
            int offset = ChartMinutes - shown.Count;
            for (int i = 0; i < ChartMinutes; i++)
            {
                var m = i >= offset ? shown[i - offset] : null;
                _columns[i].income.style.height = new Length(m == null ? 0 : (float)(m.Income / max * 100), LengthUnit.Percent);
                _columns[i].expense.style.height = new Length(m == null ? 0 : (float)(m.Expenses / max * 100), LengthUnit.Percent);
            }

            foreach (var pair in _lines)
            {
                int i = (int)pair.Key;
                Ui.Set(pair.Value.now, Ui.Money(current?.Amounts[i] ?? 0));
                Ui.Set(pair.Value.last, last == null ? "—" : Ui.Money(last.Amounts[i]));
                Ui.Set(pair.Value.total, Ui.Money(ledger.Totals[i]));
            }

            double totalNet = Ledger.IncomeLines.Sum(l => ledger.Totals[(int)l]) - Ledger.ExpenseLines.Sum(l => ledger.Totals[(int)l]);
            SetNet(_netNow, current?.Net ?? 0);
            SetNet(_netLast, last?.Net);
            SetNet(_netTotal, totalNet);
        }

        static void SetNet(Label label, double? value)
        {
            Ui.Set(label, value is double v ? Ui.Signed(v) + " cr" : "—");
            Ui.Tone(label, value is double n ? (n > 0 ? "ws-good" : n < 0 ? "ws-bad" : null) : "ws-muted");
        }
    }
}
