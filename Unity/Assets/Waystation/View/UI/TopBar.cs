using UnityEngine.UIElements;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>Resource chips across the top (credits, reputation, fuel, power, orbit, traffic), then windows, clock and speed.</summary>
    public sealed class TopBar
    {
        readonly GameController _game;
        readonly Label _money, _trend, _rep, _repSub, _fuel, _fuelSub, _power, _powerSub, _orbit, _orbitSub, _traffic, _trafficSub, _clock;
        readonly Bar _repBar, _fuelBar, _batteryBar;
        readonly VisualElement _orbitIcon, _powerIcon;
        readonly Button _grid;
        readonly Button[] _speedButtons = new Button[GameController.SpeedMultipliers.Length];

        public VisualElement Root { get; }

        public TopBar(GameController game, UiRoot ui)
        {
            _game = game;
            Root = Ui.El("ws-topbar");

            Chip(ui, "credits", out _money, out _trend, out _, () => "Credits. Below: net result of the last minute (ledger: M).", "ws-icon--accent");
            Chip(ui, "reputation", out _rep, out _repSub, out _repBar, () => "Reputation. Higher means more traffic.", "ws-icon--accent");
            Chip(ui, "fuel", out _fuel, out _fuelSub, out _fuelBar,
                () => $"Fuel. Tankers restock below {Rules.RestockThreshold:P0} at {Rules.FuelBuyPrice:0.0} cr/unit; ships pay {Rules.FuelSellPrice:0.0}.");
            _powerIcon = Chip(ui, "power", out _power, out _powerSub, out _batteryBar, PowerTooltip);
            _orbitIcon = Chip(ui, "sun", out _orbit, out _orbitSub, out _,
                () => $"Orbit: {Orbit.PeriodSeconds / 60:0} min, {Orbit.ShadowFraction:P0} in the planet's shadow. No solar power in the eclipse.");
            Chip(ui, "ship", out _traffic, out _trafficSub, out _, () => _game.Station.AutoClearance
                ? "Ships served. Traffic control is clearing landings."
                : "Ships served. You clear every landing (Comms panel) until a Traffic Control tower is online.");

            Ui.El("ws-spacer").AddTo(Root);

            _grid = Ui.IconButton("grid", _game.ToggleGrid).AddTo(Root);
            ui.Tooltip(_grid, () => "Build grid (G)");
            ui.Tooltip(Ui.IconButton("log", () => ui.ToggleWindow(UiWindow.Log)).AddTo(Root), () => "Station log (L)");
            ui.Tooltip(Ui.IconButton("ledger", () => ui.ToggleWindow(UiWindow.Ledger)).AddTo(Root), () => "Ledger (M)");
            Ui.El("ws-divider").AddTo(Root);

            _clock = Ui.Text("", "ws-clock").AddTo(Root);
            string[] icons = { "pause", "speed1", "speed2", "speed3" };
            string[] tips = { "Pause (Space)", "Normal speed (1)", "Fast (2)", "Fastest (3)" };
            for (int i = 0; i < _speedButtons.Length; i++)
            {
                int speed = i;
                _speedButtons[i] = Ui.IconButton(icons[i], () => _game.SetSpeed(speed)).AddTo(Root);
                ui.Tooltip(_speedButtons[i], () => tips[speed]);
            }
        }

        string PowerTooltip()
        {
            var s = _game.Station;
            string text = $"Power: making {s.PowerSupply:0.#} kW (solar {s.SolarPower:0.#}), using {s.PowerDemand:0.#} kW.\nBattery {s.BatteryCharge / 1000:0.0} / {s.BatteryCapacity / 1000:0.0} MJ.";
            if (s.GeneratorsOn) text += "\nGenerators running.";
            if (s.Brownout) text += $"\nBrownout: {s.PowerSatisfaction:P0} of demand met. Everything slows down.";
            return text;
        }

        /// <summary>Icon, a big value, and a small line (or a thin bar) underneath.</summary>
        VisualElement Chip(UiRoot ui, string icon, out Label value, out Label sub, out Bar bar, System.Func<string> tooltip, string iconTone = null)
        {
            var chip = Ui.El("ws-chip").AddTo(Root);
            var image = Ui.Icon(icon, "ws-icon--big").AddTo(chip);
            if (iconTone != null) image.AddToClassList(iconTone);
            var body = Ui.El("ws-chip-body").AddTo(chip);
            value = Ui.Text("", "ws-chip-value").AddTo(body);
            sub = Ui.Text("", "ws-chip-sub").AddTo(body);
            bar = new Bar().AddTo(body);
            Ui.Show(bar, false); // shown by Update for chips that use it
            ui.Tooltip(chip, tooltip);
            return image;
        }

        public void Update()
        {
            var s = _game.Station;

            Ui.Set(_money, $"{s.Money:N0}");
            Ui.Tone(_money, s.Money < 0 ? "ws-bad" : null);
            double net = s.Ledger.LastMinuteNet;
            Ui.Set(_trend, $"{Ui.Signed(net)}/min");
            Ui.Tone(_trend, net > 0 ? "ws-good" : net < 0 ? "ws-bad" : "ws-muted");

            double rep = s.Reputation / 100.0;
            Ui.Set(_rep, $"{s.Reputation:0}");
            Ui.Show(_repSub, false);
            Ui.Show(_repBar, true);
            _repBar.Set(rep, Bar.Health(rep));

            double capacity = s.FuelCapacity;
            double fuel = capacity > 0 ? s.FuelStock / capacity : 0;
            Ui.Set(_fuel, $"{s.FuelStock:0}");
            _fuelBar.Set(fuel, fuel < Rules.RestockThreshold ? Bar.Bad : Bar.Warn);
            bool tanker = s.FuelDeliveryTick >= 0;
            Ui.Show(_fuelSub, tanker);
            Ui.Show(_fuelBar, !tanker);
            if (tanker) Ui.Set(_fuelSub, $"tanker {(s.FuelDeliveryTick - s.Tick) / (double)Station.TicksPerSecond:0}s");

            double power = s.NetPower;
            Ui.Set(_power, $"{(power >= 0 ? "+" : "−")}{System.Math.Abs(power):0.#} kW");
            Ui.Tone(_power, s.Brownout ? "ws-bad" : power < 0 ? "ws-warn" : null);
            Ui.IconTone(_powerIcon, s.Brownout ? "bad" : s.GeneratorsOn ? "warn" : "good");
            double battery = s.BatteryFraction;
            _batteryBar.Set(battery, s.Brownout ? Bar.Bad : Bar.Health(battery));
            Ui.Show(_powerSub, s.Brownout || s.GeneratorsOn);
            Ui.Show(_batteryBar, !s.Brownout && !s.GeneratorsOn);
            Ui.Set(_powerSub, s.Brownout ? $"brownout {s.PowerSatisfaction:P0}" : "generator on");
            Ui.Tone(_powerSub, s.Brownout ? "ws-bad" : "ws-warn");

            double toChange = Orbit.SecondsToChange(s.Tick, out bool sunset);
            Ui.SetIcon(_orbitIcon, sunset ? "sun" : "eclipse");
            Ui.IconTone(_orbitIcon, sunset ? "warn" : "comms");
            Ui.Set(_orbit, Ui.Clock(toChange).Substring(2));
            Ui.Set(_orbitSub, sunset ? "to sunset" : "to sunrise");

            Ui.Set(_traffic, $"{s.ShipsServed}");
            int lost = s.ShipsLostAngry + s.ShipsDiverted;
            string sub = s.ShipsDisappointed > 0 ? $"{lost} lost · {s.ShipsDisappointed} let down" : $"{lost} lost";
            Ui.Set(_trafficSub, s.AutoClearance ? sub + " · auto" : sub);

            Ui.Set(_clock, Ui.Clock(s.Seconds));
            _grid.EnableInClassList("ws-button--on", _game.GridVisible);
            for (int i = 0; i < _speedButtons.Length; i++)
                _speedButtons[i].EnableInClassList("ws-button--on", _game.Speed == i);
        }
    }
}
