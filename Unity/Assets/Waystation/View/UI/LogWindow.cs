using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>The station log (L): every event, newest at the bottom, filterable by kind.</summary>
    public sealed class LogWindow
    {
        enum Filter { All, Hails, Warnings, Alerts }

        readonly GameController _game;
        readonly ScrollView _scroll;
        readonly Dictionary<Filter, Button> _filterButtons = new Dictionary<Filter, Button>();
        readonly List<(VisualElement row, EventKind kind)> _rows = new List<(VisualElement, EventKind)>();
        Filter _filter = Filter.All;
        int _logCount;

        public VisualElement Root { get; }

        public LogWindow(GameController game, UiRoot ui)
        {
            _game = game;
            Root = Ui.El("ws-panel", "ws-window");

            var header = Ui.El("ws-window-header").AddTo(Root);
            Ui.Heading("STATION LOG").AddTo(header).AddToClassList("ws-grow");
            foreach (Filter f in Enum.GetValues(typeof(Filter)))
            {
                var filter = f;
                _filterButtons[f] = Ui.Button(f.ToString().ToUpperInvariant(), () => SetFilter(filter)).AddTo(header);
            }
            Ui.Button("×", () => ui.CloseWindow()).AddTo(header);

            _scroll = new ScrollView(ScrollViewMode.Vertical).AddTo(Root);
            _scroll.AddToClassList("ws-scroll");
            SetFilter(Filter.All);
        }

        public void Reset()
        {
            _scroll.Clear();
            _rows.Clear();
            _logCount = 0;
        }

        void SetFilter(Filter filter)
        {
            _filter = filter;
            foreach (var pair in _filterButtons) pair.Value.EnableInClassList("ws-button--on", pair.Key == filter);
            foreach (var (row, kind) in _rows) Ui.Show(row, Passes(kind));
        }

        bool Passes(EventKind kind)
        {
            switch (_filter)
            {
                case Filter.Hails: return kind == EventKind.Hail;
                case Filter.Warnings: return kind == EventKind.Warning || kind == EventKind.Alert;
                case Filter.Alerts: return kind == EventKind.Alert;
                default: return true;
            }
        }

        public void Update()
        {
            var log = _game.Station.Log;
            if (_logCount > log.Count) Reset();
            if (_logCount == log.Count) return;

            for (; _logCount < log.Count; _logCount++)
            {
                var e = log[_logCount];
                var row = Ui.El("ws-row").AddTo(_scroll);
                row.style.alignItems = Align.FlexStart;
                var time = Ui.Text(Ui.Clock(e.Tick / (double)Station.TicksPerSecond), "ws-small", "ws-muted").AddTo(row);
                time.style.minWidth = 60;
                var text = Ui.Text(e.Text, "ws-small", "ws-grow").AddTo(row);
                Ui.Tone(text, Tone(e.Kind));
                Ui.Show(row, Passes(e.Kind));
                _rows.Add((row, e.Kind));
            }
            _scroll.schedule.Execute(() => _scroll.scrollOffset = new UnityEngine.Vector2(0, float.MaxValue));
        }

        static string Tone(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.Hail: return "ws-comms";
                case EventKind.Revenue: return "ws-good";
                case EventKind.Warning: return "ws-warn";
                case EventKind.Alert: return "ws-bad";
                default: return null;
            }
        }
    }
}
