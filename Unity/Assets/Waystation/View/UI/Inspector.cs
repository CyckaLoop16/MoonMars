using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>
    /// Left-hand panel for whatever is selected: a building (status, traffic, earnings, demolish)
    /// or a ship (state, patience, fuel, repairs, where it's docked). The layout is rebuilt only when
    /// the selection changes; the values refresh every frame through the updaters it registers.
    /// </summary>
    public sealed class Inspector
    {
        readonly GameController _game;
        readonly UiRoot _ui;
        readonly List<Action> _updaters = new List<Action>();
        object _shown;

        public VisualElement Root { get; }

        public Inspector(GameController game, UiRoot ui)
        {
            _game = game;
            _ui = ui;
            Root = Ui.El("ws-panel", "ws-inspector");
            Ui.Show(Root, false);
        }

        public void Update()
        {
            object selected = (object)_game.SelectedShip ?? _game.Selected;
            if (selected != _shown)
            {
                _shown = selected;
                Root.Clear();
                _updaters.Clear();
                if (selected is Building b) BuildBuilding(b);
                else if (selected is Ship s) BuildShip(s);
                Ui.Show(Root, selected != null);
            }
            foreach (var update in _updaters) update();
        }

        // ------------------------------------------------------------------ building

        void BuildBuilding(Building b)
        {
            var s = _game.Station;
            var def = b.Def;
            var header = Ui.El("ws-window-header").AddTo(Root);
            var title = Ui.Heading("").AddTo(header);
            title.AddToClassList("ws-grow");
            Ui.Button("×", _game.ClearSelection).AddTo(header);
            Live(title, () => s.Label(b).ToUpperInvariant());

            Ui.Text(def.Description, "ws-small", "ws-muted").AddTo(Root);

            var status = Ui.Text("", "ws-small").AddTo(Root);
            _updaters.Add(() =>
            {
                Ui.Set(status, b.Connected ? "● Connected to the station" : "● Not connected: link it with walkways or foundations");
                Ui.Tone(status, b.Connected ? "ws-good" : "ws-bad");
            });

            if (def.Serves.Length > 0)
            {
                Ui.Text("SERVICE", "ws-subheading").AddTo(Root);
                Ui.Text("Takes " + string.Join(" and ", def.Serves.Select(c => ShipCatalog.Get(c).Name.ToLowerInvariant() + "s")) +
                        (def.Repairs ? " · repair crew on duty" : ""), "ws-small").AddTo(Root);

                var occupant = Ui.Text("", "ws-small").AddTo(Root);
                occupant.RegisterCallback<ClickEvent>(_ =>
                {
                    var ship = b.ShipId is int id ? s.GetShip(id) : null;
                    if (ship != null)
                    {
                        _game.SelectShip(ship);
                        _game.FocusOnShip(ship);
                    }
                });
                _updaters.Add(() =>
                {
                    var ship = b.ShipId is int id ? s.GetShip(id) : null;
                    Ui.Set(occupant, ship != null ? $"In use: {ship.Name} ({ship.State.ToString().ToLowerInvariant()})" : "Free");
                    Ui.Tone(occupant, ship != null ? "ws-comms" : "ws-good");
                });

                Row("Ships served", () => b.ShipsServed.ToString());
                Row("Earned", () => Ui.Money(b.Earned));
            }

            if (def.FuelCapacity > 0)
            {
                Ui.Text("FUEL TANK", "ws-subheading").AddTo(Root);
                var bar = new Bar().AddTo(Root);
                Row("Station stock", () => $"{s.FuelStock:0} / {s.FuelCapacity:0}");
                _updaters.Add(() =>
                {
                    double f = s.FuelCapacity > 0 ? s.FuelStock / s.FuelCapacity : 0;
                    bar.Set(f, f < Rules.RestockThreshold ? Bar.Bad : Bar.Warn);
                });
            }

            Ui.Text("COSTS", "ws-subheading").AddTo(Root);
            Row("Upkeep", () => $"{def.UpkeepPerMinute:0.#} cr/min");

            var actions = Ui.El("ws-row").AddTo(Root);
            actions.style.marginTop = 8;
            Ui.Button("FOCUS", () => _game.FocusOnBuilding(b)).AddTo(actions);
            Ui.El("ws-spacer").AddTo(actions);
            if (!def.IsHub)
            {
                var demolish = Ui.Button($"DEMOLISH (+{def.Cost * Rules.DemolishRefund:N0})", () => _game.TryExecute(new Demolish(b.Id)), "ws-button--danger").AddTo(actions);
                _ui.Tooltip(demolish, () => b.ShipId != null ? "A ship is using it right now." : $"Refunds {Rules.DemolishRefund:P0} of the build cost.");
            }
        }

        // ------------------------------------------------------------------ ship

        void BuildShip(Ship ship)
        {
            var s = _game.Station;
            var cls = ShipCatalog.Get(ship.Class);
            var header = Ui.El("ws-window-header").AddTo(Root);
            Ui.Heading(ship.Name.ToUpperInvariant()).AddTo(header).AddToClassList("ws-grow");
            Ui.Button("×", _game.ClearSelection).AddTo(header);

            Ui.Text($"{cls.Name} · landing fee {Ui.Money(cls.LandingFee)}", "ws-small", "ws-muted").AddTo(Root);
            var state = Ui.Text("", "ws-small").AddTo(Root);
            _updaters.Add(() =>
            {
                Ui.Set(state, Describe(s, ship));
                Ui.Tone(state, ship.Angry ? "ws-bad" : ship.State == ShipState.Hailing ? "ws-comms" : null);
            });

            var patienceBox = Ui.El().AddTo(Root);
            Ui.Text("PATIENCE", "ws-subheading").AddTo(patienceBox);
            var patience = new Bar().AddTo(patienceBox);
            _updaters.Add(() =>
            {
                bool waiting = ship.State == ShipState.Hailing || (ship.State == ShipState.Servicing && s.FuelStock <= 0 && ship.FuelLoaded < ship.FuelNeeded);
                Ui.Show(patienceBox, waiting);
                double p = ship.MaxPatienceTicks > 0 ? ship.PatienceTicks / (double)ship.MaxPatienceTicks : 1;
                patience.Set(p, Bar.Health(p));
            });

            Ui.Text("FUEL", "ws-subheading").AddTo(Root);
            var fuel = new Bar().AddTo(Root);
            Row("Loaded", () => $"{ship.FuelLoaded:0} / {ship.FuelNeeded:0}");
            _updaters.Add(() => fuel.Set(ship.FuelNeeded > 0 ? ship.FuelLoaded / ship.FuelNeeded : 1, Bar.Warn));

            if (ship.NeedsRepair)
                Row("Repairs", () => ship.Repaired ? "done" : $"needed ({Ui.Money(cls.RepairFee)})");

            var actions = Ui.El("ws-row").AddTo(Root);
            actions.style.marginTop = 8;
            Ui.Button("FOCUS", () => _game.FocusOnShip(ship)).AddTo(actions);
            Ui.El("ws-spacer").AddTo(actions);
            var clear = Ui.Button("CLEAR TO LAND", () => _game.BeginClearance(ship), "ws-button--comms").AddTo(actions);
            var wave = Ui.Button("WAVE OFF", () => _game.TryExecute(new WaveOff(ship.Id)), "ws-button--danger").AddTo(actions);
            _updaters.Add(() =>
            {
                bool hailing = ship.State == ShipState.Hailing;
                Ui.Show(clear, hailing);
                Ui.Show(wave, hailing);
            });
        }

        static string Describe(Station s, Ship ship)
        {
            var b = ship.BuildingId is int id ? s.GetBuilding(id) : null;
            string where = b != null ? s.Label(b) : "?";
            switch (ship.State)
            {
                case ShipState.Approaching: return "Inbound from the dark.";
                case ShipState.Hailing: return $"Hailing for a landing spot · {ship.PatienceTicks / (double)Station.TicksPerSecond:0}s of patience left";
                case ShipState.Landing: return $"Landing on {where}.";
                case ShipState.Servicing:
                    if (ship.FuelLoaded < ship.FuelNeeded)
                        return s.FuelStock <= 0 ? $"On {where}, waiting for fuel: the tank is dry!" : $"Refuelling on {where}.";
                    return ship.NeedsRepair && !ship.Repaired ? $"Under repair on {where}." : $"Settling up on {where}.";
                case ShipState.Departing: return ship.Angry ? "Leaving angry." : "Departing. Paid in full.";
                default: return "Gone.";
            }
        }

        // ------------------------------------------------------------------ helpers

        void Row(string label, Func<string> value)
        {
            var row = Ui.El("ws-table-row").AddTo(Root);
            Ui.Text(label, "ws-small", "ws-muted").AddTo(row);
            var v = Ui.Text("", "ws-small").AddTo(row);
            Live(v, value);
        }

        void Live(Label label, Func<string> text) => _updaters.Add(() => Ui.Set(label, text()));
    }
}
