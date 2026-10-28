using UnityEngine.UIElements;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>Station status across the top: credits, reputation, fuel, traffic stats, windows, clock and speed.</summary>
    public sealed class TopBar
    {
        readonly GameController _game;
        readonly Label _money, _trend, _rep, _fuel, _fuelNote, _power, _powerNote, _orbit, _orbitNote, _traffic, _atc, _clock;
        readonly Bar _repBar, _fuelBar, _batteryBar, _orbitBar;
        readonly Button[] _speedButtons = new Button[GameController.SpeedMultipliers.Length];

        public VisualElement Root { get; }

        public TopBar(GameController game, UiRoot ui)
        {
            _game = game;
            Root = Ui.El("ws-panel", "ws-topbar");

            var brand = Ui.El().AddTo(Root);
            Ui.Text("WAYSTATION", "ws-brand").AddTo(brand);
            Ui.Text("DOCK CONTROL", "ws-brand-sub").AddTo(brand);
            Ui.El("ws-divider").AddTo(Root);

            var credits = Stat("CREDITS", out _money);
            _trend = Ui.Text("", "ws-small").AddTo(credits);
            ui.Tooltip(credits, () => "Net result of the last full minute. Open the ledger (M) for the breakdown.");
            Ui.El("ws-divider").AddTo(Root);

            var rep = Gauge("REPUTATION", out _rep, out _repBar);
            ui.Tooltip(rep, () => "Higher reputation brings more traffic. Served ships raise it; angry or waved-off ships lower it.");
            Ui.El("ws-divider").AddTo(Root);

            var fuel = Gauge("FUEL", out _fuel, out _fuelBar);
            _fuelNote = Ui.Text("", "ws-small", "ws-muted").AddTo(fuel);
            ui.Tooltip(fuel, () => $"A tanker is ordered automatically below {Rules.RestockThreshold:P0} of capacity, at {Rules.FuelBuyPrice:0.0} cr per unit. Ships buy fuel at {Rules.FuelSellPrice:0.0} cr.");
            Ui.El("ws-divider").AddTo(Root);

            var power = Gauge("POWER", out _power, out _batteryBar);
            _powerNote = Ui.Text("", "ws-small", "ws-muted").AddTo(power);
            ui.Tooltip(power, PowerTooltip);
            Ui.El("ws-divider").AddTo(Root);

            var orbit = Gauge("ORBIT", out _orbit, out _orbitBar);
            _orbitNote = Ui.Text("", "ws-small", "ws-muted").AddTo(orbit);
            ui.Tooltip(orbit, () => $"The station circles the planet every {Orbit.PeriodSeconds / 60:0} minutes and spends {Orbit.ShadowFraction:P0} of each orbit in its shadow. Solar arrays only work in sunlight; batteries carry you through the eclipse.");
            Ui.El("ws-divider").AddTo(Root);

            var traffic = Stat("TRAFFIC", out _traffic);
            _atc = Ui.Text("", "ws-small").AddTo(traffic);
            ui.Tooltip(traffic, () => _game.Station.AutoClearance
                ? "Traffic control clears ships to free pads automatically."
                : "No traffic control online: you clear every landing by hand (Comms panel, or click a hailing ship). Build a Traffic Control tower to automate it.");

            Ui.El("ws-spacer").AddTo(Root);

            Ui.Button("LOG  L", () => ui.ToggleWindow(UiWindow.Log)).AddTo(Root);
            Ui.Button("LEDGER  M", () => ui.ToggleWindow(UiWindow.Ledger)).AddTo(Root);
            Ui.El("ws-divider").AddTo(Root);

            _clock = Ui.Text("", "ws-stat-value").AddTo(Root);
            _clock.style.minWidth = 70;
            string[] names = { "II", "1×", "2×", "4×" };
            for (int i = 0; i < _speedButtons.Length; i++)
            {
                int speed = i;
                _speedButtons[i] = Ui.Button(names[i], () => _game.SetSpeed(speed)).AddTo(Root);
            }
            ui.Tooltip(_speedButtons[0], () => "Pause (Space). Speed: 1 / 2 / 3.");
        }

        string PowerTooltip()
        {
            var s = _game.Station;
            string text = $"Making {s.PowerSupply:0.#} kW (solar {s.SolarPower:0.#}), using {s.PowerDemand:0.#} kW.\n" +
                          $"Batteries {s.BatteryCharge / 1000:0.0} / {s.BatteryCapacity / 1000:0.0} MJ.";
            if (s.GeneratorsOn) text += "\nFuel cell generators are running.";
            if (s.Brownout) text += $"\nBROWNOUT: only {s.PowerSatisfaction:P0} of demand is met. Pumps, repairs and refineries slow down; below 50% lounges, comms and traffic control shut down.";
            return text;
        }

        VisualElement Stat(string label, out Label value)
        {
            var box = Ui.El().AddTo(Root);
            Ui.Text(label, "ws-stat-label").AddTo(box);
            value = Ui.Text("", "ws-stat-value").AddTo(box);
            return box;
        }

        VisualElement Gauge(string label, out Label value, out Bar bar)
        {
            var box = Ui.El("ws-gauge").AddTo(Root);
            var row = Ui.El("ws-row").AddTo(box);
            Ui.Text(label, "ws-stat-label", "ws-grow").AddTo(row);
            value = Ui.Text("", "ws-small").AddTo(row);
            bar = new Bar().AddTo(box);
            return box;
        }

        public void Update()
        {
            var s = _game.Station;

            Ui.Set(_money, Ui.Money(s.Money));
            Ui.Tone(_money, s.Money < 0 ? "ws-bad" : null);
            double net = s.Ledger.LastMinuteNet;
            Ui.Set(_trend, $"{Ui.Signed(net)} /min");
            Ui.Tone(_trend, net > 0 ? "ws-good" : net < 0 ? "ws-bad" : "ws-muted");

            double rep = s.Reputation / 100.0;
            Ui.Set(_rep, $"{s.Reputation:0}");
            _repBar.Set(rep, Bar.Health(rep));

            double capacity = s.FuelCapacity;
            double fuel = capacity > 0 ? s.FuelStock / capacity : 0;
            Ui.Set(_fuel, $"{s.FuelStock:0} / {capacity:0}");
            _fuelBar.Set(fuel, fuel < Rules.RestockThreshold ? Bar.Bad : Bar.Warn);
            Ui.Set(_fuelNote, s.FuelDeliveryTick >= 0
                ? $"Tanker in {(s.FuelDeliveryTick - s.Tick) / (double)Station.TicksPerSecond:0}s"
                : "");

            double power = s.NetPower;
            Ui.Set(_power, $"{(power >= 0 ? "+" : "−")}{System.Math.Abs(power):0.#} kW");
            Ui.Tone(_power, s.Brownout ? "ws-bad" : power < 0 ? "ws-warn" : "ws-good");
            double battery = s.BatteryFraction;
            _batteryBar.Set(battery, s.Brownout ? Bar.Bad : Bar.Health(battery));
            Ui.Set(_powerNote, s.Brownout ? $"BROWNOUT {s.PowerSatisfaction:P0}" : s.GeneratorsOn ? $"Generator on · {battery:P0}" : $"Battery {battery:P0}");
            Ui.Tone(_powerNote, s.Brownout ? "ws-bad" : s.GeneratorsOn ? "ws-warn" : "ws-muted");

            double toChange = Orbit.SecondsToChange(s.Tick, out bool sunset);
            Ui.Set(_orbit, sunset ? "SUNLIGHT" : "ECLIPSE");
            Ui.Tone(_orbit, sunset ? "ws-warn" : "ws-comms");
            _orbitBar.Set(Orbit.Phase(s.Tick), sunset ? Bar.Warn : Bar.Comms);
            Ui.Set(_orbitNote, $"{(sunset ? "sunset" : "sunrise")} in {Ui.Clock(toChange).Substring(2)}");

            Ui.Set(_traffic, $"{s.ShipsServed} served · {s.ShipsLostAngry + s.ShipsDiverted} lost");
            Ui.Set(_atc, s.AutoClearance
                ? $"ATC AUTO · wait {s.AverageHailWaitSeconds:0}s"
                : $"ATC MANUAL · wait {s.AverageHailWaitSeconds:0}s");
            Ui.Tone(_atc, s.AutoClearance ? "ws-good" : "ws-comms");

            Ui.Set(_clock, Ui.Clock(s.Seconds));
            for (int i = 0; i < _speedButtons.Length; i++)
                _speedButtons[i].EnableInClassList("ws-button--on", _game.Speed == i);
        }
    }
}
