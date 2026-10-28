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
            var image = Ui.El("ws-inspector-image").AddTo(Root);
            var thumbnail = Thumbnails.Get(_game.Buildings, def.Type);
            if (thumbnail != null) image.style.backgroundImage = new StyleBackground(thumbnail);
            else Ui.Show(image, false);
            _ui.Tooltip(image, () => def.Description);

            var header = Ui.El("ws-window-header").AddTo(Root);
            var title = Ui.Heading("").AddTo(header);
            title.AddToClassList("ws-grow");
            Ui.IconButton("close", _game.ClearSelection).AddTo(header);
            Live(title, () => s.Label(b));

            var status = Ui.Text("", "ws-small").AddTo(Root);
            _updaters.Add(() =>
            {
                Ui.Set(status, !b.Connected ? "Not connected" : !b.Enabled ? "Switched off" : "Online");
                Ui.Tone(status, !b.Connected ? "ws-bad" : !b.Enabled ? "ws-warn" : "ws-good");
            });
            _ui.Tooltip(status, () => b.Connected ? def.Description : "Link it to the station with walkways or foundations.");

            if (def.Serves.Length > 0)
            {
                Ui.Text("Service", "ws-subheading").AddTo(Root);
                Row("Takes", () => string.Join(", ", def.Serves.Select(c => ShipCatalog.Get(c).Name.ToLowerInvariant() + "s")) + (def.Repairs ? " · repairs" : ""));

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
                    Ui.Set(occupant, ship != null ? $"{ship.Name} · {ship.State.ToString().ToLowerInvariant()}" : "Free");
                    Ui.Tone(occupant, ship != null ? "ws-comms" : "ws-good");
                });

                Row("Ships served", () => b.ShipsServed.ToString());
                Row("Earned", () => Ui.Money(b.Earned));
            }

            if (def.FuelCapacity > 0)
            {
                Ui.Text("Fuel tank", "ws-subheading").AddTo(Root);
                var bar = new Bar().AddTo(Root);
                Row("Station stock", () => $"{s.FuelStock:0} / {s.FuelCapacity:0}");
                _updaters.Add(() =>
                {
                    double f = s.FuelCapacity > 0 ? s.FuelStock / s.FuelCapacity : 0;
                    bar.Set(f, f < Rules.RestockThreshold ? Bar.Bad : Bar.Warn);
                });
            }

            BuildPower(b);
            BuildOperation(b);

            Ui.El("ws-rule").AddTo(Root);
            Row("Upkeep", () => $"{def.UpkeepPerMinute:0.#} cr/min");

            var actions = Ui.El("ws-row").AddTo(Root);
            actions.style.marginTop = 8;
            _ui.Tooltip(Ui.IconButton("focus", () => _game.FocusOnBuilding(b)).AddTo(actions), () => "Centre the view");
            Ui.El("ws-spacer").AddTo(actions);
            if (def.CanToggle)
            {
                var toggle = Ui.IconButton("toggle", () => _game.TryExecute(new SetEnabled(b.Id, !b.Enabled))).AddTo(actions);
                _ui.Tooltip(toggle, () => b.Enabled ? "Switch off (saves power, still costs upkeep)" : "Switch on");
                _updaters.Add(() => toggle.EnableInClassList("ws-button--on", b.Enabled));
            }
            if (!def.IsHub)
            {
                var demolish = Ui.IconButton("demolish", () => _game.TryExecute(new Demolish(b.Id)), "ws-button--danger").AddTo(actions);
                _ui.Tooltip(demolish, () => b.ShipId != null ? "A ship is using it" : $"Demolish (+{def.Cost * Rules.DemolishRefund:N0} cr)");
            }
        }

        void BuildPower(Building b)
        {
            var s = _game.Station;
            var def = b.Def;
            if (def.PowerUse <= 0 && def.PowerOutput <= 0 && def.SolarOutput <= 0 && def.GeneratorOutput <= 0 && def.BatteryCapacity <= 0) return;

            Ui.Text("Power", "ws-subheading").AddTo(Root);
            if (def.PowerOutput > 0) Row("Radioisotope generator", () => $"+{def.PowerOutput:0.#} kW");
            if (def.SolarOutput > 0)
                Row("Solar output", () => b.Operating ? $"+{def.SolarOutput * s.SunExposure:0.#} of {def.SolarOutput:0} kW" : "off");
            if (def.GeneratorOutput > 0)
                Row("Generator", () => b.Running ? $"running · +{def.GeneratorOutput:0} kW · {def.FuelBurn * 60:0} fuel/min"
                    : !b.Operating ? "off"
                    : s.GeneratorsOn ? "no fuel!"
                    : $"standby (starts below {Rules.GeneratorStartCharge:P0} battery)");
            if (def.BatteryCapacity > 0)
            {
                Row("Capacity", () => $"{def.BatteryCapacity / 1000:0.#} MJ");
                var bar = new Bar().AddTo(Root);
                Row("Station charge", () => $"{s.BatteryCharge / 1000:0.0} / {s.BatteryCapacity / 1000:0.0} MJ");
                _updaters.Add(() => bar.Set(s.BatteryFraction, Bar.Health(s.BatteryFraction)));
            }
            if (def.PowerUse > 0)
                Row("Draw", () => b.Operating && (def.RefineRate <= 0 || b.Running) ? $"{def.PowerUse:0.#} kW" : "0 kW");
            Row("Station grid", () => s.Brownout ? $"brownout · {s.PowerSatisfaction:P0}" : $"{s.PowerSupply:0.#} kW made · {s.PowerDemand:0.#} kW used");
        }

        /// <summary>What a module is doing right now.</summary>
        void BuildOperation(Building b)
        {
            var s = _game.Station;
            var def = b.Def;
            if (def.RefineRate > 0)
            {
                Ui.Text("Refinery", "ws-subheading").AddTo(Root);
                Row("Status", () => b.Running ? $"cracking ice · +{def.RefineRate * 60 * s.PowerSatisfaction:0} fuel/min"
                    : !b.Operating ? "off"
                    : s.FuelStock >= s.FuelCapacity - 0.5 ? "idle: tanks full"
                    : "waiting for spare power");
                Row("Station fuel", () => $"{s.FuelStock:0} / {s.FuelCapacity:0}");
            }
            if (def.FuelCapacity > 0 && !def.IsHub)
            {
                Ui.Text("Fuel", "ws-subheading").AddTo(Root);
                Row("Holds", () => $"{def.FuelCapacity:0} units");
                Row("Station fuel", () => $"{s.FuelStock:0} / {s.FuelCapacity:0}");
            }
            if (def.Provides is ServiceType service)
            {
                Ui.Text(Services.Name(service), "ws-subheading").AddTo(Root);
                Row("Inside", () => $"{Crew.Visitors(s, b)} / {def.Capacity}");
                Row("Waiting", () => s.People.Count(p => p.BuildingId == b.Id && p.State == PersonState.Queueing).ToString());
                Row("Each visit", () => $"{def.VisitFee:0} cr · {def.VisitSeconds:0}s");
                Row("Visits", () => b.Visits.ToString());
                Row("Earned", () => Ui.Money(b.Earned));
                var brings = ShipRoles.All.Where(r => r.AttractedBy == def.Type).Select(r => r.Name).ToList();
                if (brings.Count > 0) Row("Brings", () => string.Join(", ", brings));
                var status = Ui.Text("", "ws-small").AddTo(Root);
                _updaters.Add(() =>
                {
                    bool open = b.Operating && s.SystemsOnline;
                    Ui.Show(status, !open);
                    Ui.Set(status, !b.Connected ? "Closed: not connected" : !b.Enabled ? "Closed: switched off" : "Closed: not enough power");
                    Ui.Tone(status, "ws-bad");
                });
            }
            if (def.Comms)
            {
                Ui.Text("Beacon", "ws-subheading").AddTo(Root);
                Row("Status", () =>
                {
                    int n = s.CommsArraysOnline;
                    if (n == 0) return b.Operating ? "offline: not enough power" : "off";
                    return $"{1 / Math.Pow(Rules.CommsTrafficFactor, n):0.00}× traffic · +{Rules.CommsHoldingSlots * n} holding slots";
                });
            }
            if (def.Atc)
            {
                Ui.Text("Traffic control", "ws-subheading").AddTo(Root);
                var status = Ui.Text("", "ws-small").AddTo(Root);
                _updaters.Add(() =>
                {
                    Ui.Set(status, s.AtcOnline ? "Online: clearing hailing ships to free pads." : b.Operating ? "Offline: not enough power." : "Switched off: clear ships by hand.");
                    Ui.Tone(status, s.AtcOnline ? "ws-good" : "ws-warn");
                });
            }
        }

        // ------------------------------------------------------------------ ship

        void BuildShip(Ship ship)
        {
            var s = _game.Station;
            var cls = ShipCatalog.Get(ship.Class);
            var header = Ui.El("ws-window-header").AddTo(Root);
            var role = ShipRoles.Get(ship.Role);
            Ui.Icon(Icons.For(ship.Role), "ws-icon--big", "ws-icon--comms").AddTo(header).style.marginRight = 6;
            Ui.Heading(ship.Name).AddTo(header).AddToClassList("ws-grow");
            Ui.IconButton("close", _game.ClearSelection).AddTo(header);

            var subtitle = Ui.Text($"{role.Name} · {cls.Name.ToLowerInvariant()} · {ship.Crew} aboard", "ws-small", "ws-muted").AddTo(Root);
            _ui.Tooltip(subtitle, () => role.Description);
            var state = Ui.Text("", "ws-small").AddTo(Root);
            _updaters.Add(() =>
            {
                Ui.Set(state, Describe(s, ship));
                Ui.Tone(state, ship.Angry ? "ws-bad" : ship.State == ShipState.Hailing ? "ws-comms" : null);
            });

            var patienceBox = Ui.El().AddTo(Root);
            Ui.Text("Patience", "ws-subheading").AddTo(patienceBox);
            var patience = new Bar().AddTo(patienceBox);
            _updaters.Add(() =>
            {
                bool waiting = ship.State == ShipState.Hailing || (ship.State == ShipState.Servicing && s.FuelStock <= 0 && ship.FuelLoaded < ship.FuelNeeded);
                Ui.Show(patienceBox, waiting);
                double p = ship.MaxPatienceTicks > 0 ? ship.PatienceTicks / (double)ship.MaxPatienceTicks : 1;
                patience.Set(p, Bar.Health(p));
            });

            if (ship.Errands.Count > 0)
            {
                Ui.Text("Wants", "ws-subheading").AddTo(Root);
                foreach (var errand in ship.Errands)
                {
                    var e = errand;
                    var row = Ui.El("ws-table-row").AddTo(Root);
                    var left = Ui.El("ws-row").AddTo(row);
                    var icon = Ui.Icon(Icons.For(e.Service)).AddTo(left);
                    icon.style.marginRight = 6;
                    Ui.Text(Services.Name(e.Service) + (e.IsNeed ? " (need)" : ""), "ws-small").AddTo(left);
                    var status = Ui.Text("", "ws-small", "ws-muted").AddTo(row);
                    _updaters.Add(() =>
                    {
                        Ui.IconTone(icon, ShipUi.Tone(s, ship, e));
                        Ui.Set(status, ShipUi.Status(s, ship, e));
                    });
                }
                Row("Out on the station", () => Crew.Of(s, ship).Count().ToString());
            }

            Ui.Text("Fuel", "ws-subheading").AddTo(Root);
            var fuel = new Bar().AddTo(Root);
            Row("Loaded", () => $"{ship.FuelLoaded:0} / {ship.FuelNeeded:0}");
            _updaters.Add(() => fuel.Set(ship.FuelNeeded > 0 ? ship.FuelLoaded / ship.FuelNeeded : 1, Bar.Warn));

            if (ship.NeedsRepair)
                Row("Repairs", () => ship.Repaired ? "done" : $"needed ({Ui.Money(cls.RepairFee)})");

            var actions = Ui.El("ws-row").AddTo(Root);
            actions.style.marginTop = 8;
            _ui.Tooltip(Ui.IconButton("focus", () => _game.FocusOnShip(ship)).AddTo(actions), () => "Centre the view");
            Ui.El("ws-spacer").AddTo(actions);
            var clear = Ui.Button("Clear to land", () => _game.BeginClearance(ship), "ws-button--comms").AddTo(actions);
            var wave = Ui.IconButton("close", () => _game.TryExecute(new WaveOff(ship.Id)), "ws-button--danger").AddTo(actions);
            _ui.Tooltip(wave, () => "Wave off");
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
