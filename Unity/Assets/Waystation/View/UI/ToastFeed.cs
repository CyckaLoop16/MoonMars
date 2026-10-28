using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>
    /// Short-lived notices under the top bar for station events that matter (hails, warnings,
    /// alerts, construction) and for refused commands. Payments float over the pad instead.
    /// </summary>
    public sealed class ToastFeed
    {
        const int MaxToasts = 5;
        const float Lifetime = 5f;
        const float FadeTime = 0.6f;

        readonly GameController _game;
        readonly WorldLabels _worldLabels;
        readonly List<(VisualElement label, float born)> _toasts = new List<(VisualElement, float)>();
        int _logCount;
        float _lastErrorTime;

        public VisualElement Root { get; }

        public ToastFeed(GameController game, WorldLabels worldLabels)
        {
            _game = game;
            _worldLabels = worldLabels;
            Root = Ui.El("ws-toasts");
            Root.pickingMode = PickingMode.Ignore;
        }

        public void Reset()
        {
            Root.Clear();
            _toasts.Clear();
            _logCount = _game.Station.Log.Count; // don't replay the welcome message as a toast
            _lastErrorTime = _game.LastErrorTime;
        }

        public void Update()
        {
            var log = _game.Station.Log;
            if (_logCount > log.Count) _logCount = 0;
            for (; _logCount < log.Count; _logCount++)
            {
                var e = log[_logCount];
                switch (e.Kind)
                {
                    case EventKind.Revenue:
                        if (e.BuildingId is int id) _worldLabels.SpawnMoney(id, e.Amount);
                        break;
                    case EventKind.Hail: Add(e.Text, "hail", "comms"); break;
                    case EventKind.Warning: Add(e.Text, "warning", "warn"); break;
                    case EventKind.Alert: Add(e.Text, "warning", "bad", "ws-toast--alert"); break;
                    case EventKind.Construction: Add(e.Text, "build", "good"); break;
                    case EventKind.Orbit: Add(e.Text, Orbit.IsDay(e.Tick) ? "sun" : "eclipse", "warn"); break;
                }
            }

            if (_game.LastErrorTime != _lastErrorTime)
            {
                _lastErrorTime = _game.LastErrorTime;
                if (!string.IsNullOrEmpty(_game.LastError)) Add(_game.LastError, "warning", "bad", "ws-toast--alert");
            }

            float now = Time.unscaledTime;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                float age = now - _toasts[i].born;
                if (age >= Lifetime)
                {
                    _toasts[i].label.RemoveFromHierarchy();
                    _toasts.RemoveAt(i);
                    continue;
                }
                _toasts[i].label.style.opacity = Mathf.Clamp01((Lifetime - age) / FadeTime);
            }
        }

        void Add(string text, string icon, string tone, string cls = null)
        {
            var label = Ui.El("ws-toast").AddTo(Root);
            if (cls != null) label.AddToClassList(cls);
            Ui.IconTone(Ui.Icon(icon).AddTo(label), tone);
            Ui.Text(text, "ws-toast-text").AddTo(label);
            Ui.IgnorePointer(label);
            _toasts.Add((label, Time.unscaledTime));
            while (_toasts.Count > MaxToasts)
            {
                _toasts[0].label.RemoveFromHierarchy();
                _toasts.RemoveAt(0);
            }
        }
    }
}
