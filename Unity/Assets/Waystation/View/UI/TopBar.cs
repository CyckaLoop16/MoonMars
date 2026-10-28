using UnityEngine.UIElements;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>Station status across the top: credits, reputation, fuel, traffic stats, windows, clock and speed.</summary>
    public sealed class TopBar
    {
        readonly GameController _game;
        readonly Label _money, _trend, _rep, _fuel, _fuelNote, _traffic, _atc, _clock;
        readonly Bar _repBar, _fuelBar;
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

            var traffic = Stat("TRAFFIC", out _traffic);
            _atc = Ui.Text("", "ws-small").AddTo(traffic);
            ui.Tooltip(traffic, () => _game.Station.AutoAssign
                ? "Air traffic control clears ships to free pads automatically."
                : "No air traffic control yet: you clear every landing by hand (Comms panel, or click a hailing ship).");

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

            Ui.Set(_traffic, $"{s.ShipsServed} served · {s.ShipsLostAngry + s.ShipsDiverted} lost");
            Ui.Set(_atc, s.AutoAssign
                ? $"ATC AUTO · wait {s.AverageHailWaitSeconds:0}s"
                : $"ATC MANUAL · wait {s.AverageHailWaitSeconds:0}s");
            Ui.Tone(_atc, s.AutoAssign ? "ws-good" : "ws-comms");

            Ui.Set(_clock, Ui.Clock(s.Seconds));
            for (int i = 0; i < _speedButtons.Length; i++)
                _speedButtons[i].EnableInClassList("ws-button--on", _game.Speed == i);
        }
    }
}
