using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>
    /// Right-hand comms panel: a card per hailing ship (patience, needs, one-click pad buttons, pick on
    /// map, wave off) and a traffic board of everything else in the sky or on the pads.
    /// </summary>
    public sealed class CommsPanel
    {
        sealed class HailCard
        {
            public VisualElement Root;
            public Label Name, Info, Patience;
            public Bar PatienceBar;
            public VisualElement Pads;
            public string PadKey;
        }

        readonly GameController _game;
        readonly UiRoot _ui;
        readonly Label _header, _empty, _trafficHeader;
        readonly VisualElement _hails, _traffic;
        readonly Dictionary<int, HailCard> _cards = new Dictionary<int, HailCard>();
        readonly List<(VisualElement row, Label name, Label status)> _rows = new List<(VisualElement, Label, Label)>();
        readonly List<Ship> _rowShips = new List<Ship>();

        public VisualElement Root { get; }

        public CommsPanel(GameController game, UiRoot ui)
        {
            _game = game;
            _ui = ui;
            Root = Ui.El("ws-panel", "ws-comms-panel");

            _header = Ui.Heading("COMMS").AddTo(Root);
            var scroll = new ScrollView(ScrollViewMode.Vertical).AddTo(Root);
            scroll.AddToClassList("ws-scroll");
            _hails = Ui.El().AddTo(scroll);
            _empty = Ui.Text("No ships calling. Channel open.", "ws-small", "ws-muted").AddTo(scroll);
            _trafficHeader = Ui.Text("TRAFFIC", "ws-subheading").AddTo(scroll);
            _traffic = Ui.El().AddTo(scroll);
        }

        public void Update()
        {
            var s = _game.Station;
            var hailing = s.Hailing.ToList();

            int inbound = s.Ships.Count(x => x.State == ShipState.Approaching);
            Ui.Set(_header, hailing.Count > 0 ? $"COMMS · {hailing.Count} HAILING · {inbound} INBOUND" : $"COMMS · {inbound} INBOUND");
            Ui.Show(_empty, hailing.Count == 0);

            // Hail cards: drop the ones that stopped hailing, add new ones in order, update the rest.
            foreach (var id in _cards.Keys.Where(id => hailing.All(h => h.Id != id)).ToList())
            {
                _cards[id].Root.RemoveFromHierarchy();
                _cards.Remove(id);
            }
            for (int i = 0; i < hailing.Count; i++)
            {
                var ship = hailing[i];
                if (!_cards.TryGetValue(ship.Id, out var card))
                {
                    card = CreateCard(ship);
                    _cards[ship.Id] = card;
                }
                if (_hails.IndexOf(card.Root) != i) _hails.Insert(i, card.Root);
                UpdateCard(ship, card);
            }

            UpdateTraffic(s);
        }

        HailCard CreateCard(Ship ship)
        {
            var card = new HailCard { Root = Ui.El("ws-card-hail") };

            var top = Ui.El("ws-row").AddTo(card.Root);
            card.Name = Ui.Text(ship.Name, "ws-comms", "ws-grow").AddTo(top);
            card.Name.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            card.Name.RegisterCallback<ClickEvent>(_ =>
            {
                _game.SelectShip(ship);
                _game.FocusOnShip(ship);
            });
            _ui.Tooltip(card.Name, () => "Select and look at this ship");
            card.Patience = Ui.Text("", "ws-small").AddTo(top);

            card.Info = Ui.Text("", "ws-small").AddTo(card.Root);
            card.PatienceBar = new Bar().AddTo(card.Root);
            card.Pads = Ui.El("ws-row").AddTo(card.Root);
            card.Pads.style.flexWrap = Wrap.Wrap;

            var actions = Ui.El("ws-row").AddTo(card.Root);
            Ui.Button("PICK ON MAP", () => _game.BeginClearance(ship), "ws-button--comms").AddTo(actions);
            Ui.El("ws-spacer").AddTo(actions);
            var wave = Ui.Button("WAVE OFF", () => _game.TryExecute(new WaveOff(ship.Id)), "ws-button--danger").AddTo(actions);
            _ui.Tooltip(wave, () => $"Turn the ship away. Reputation −{Rules.ReputationLostWhenWavedOff:0}, but less than letting it give up (−{Rules.ReputationLostWhenAngry:0}).");
            return card;
        }

        void UpdateCard(Ship ship, HailCard card)
        {
            var s = _game.Station;
            double patience = ship.MaxPatienceTicks > 0 ? ship.PatienceTicks / (double)ship.MaxPatienceTicks : 1;
            Ui.Set(card.Patience, $"{ship.PatienceTicks / (double)Station.TicksPerSecond:0}s");
            Ui.Tone(card.Patience, patience < 0.33 ? "ws-bad" : patience < 0.66 ? "ws-warn" : "ws-muted");
            card.PatienceBar.Set(patience, Bar.Health(patience));
            Ui.Set(card.Info, $"{ShipCatalog.Get(ship.Class).Name} · {ship.FuelNeeded:0} fuel{(ship.NeedsRepair ? " · needs repairs" : "")}");
            card.Root.EnableInClassList("ws-card-hail--urgent", patience < 0.33);
            card.Root.EnableInClassList("ws-card-hail--picking", _game.ClearingShip == ship);

            // One button per pad that can take this ship right now; rebuilt only when that set changes.
            var free = s.Buildings.Where(b => s.LandingError(ship, b) == null).ToList();
            string key = string.Join(",", free.Select(b => b.Id));
            if (key == card.PadKey) return;
            card.PadKey = key;
            card.Pads.Clear();
            if (free.Count == 0)
            {
                string why = ship.Class == ShipClass.Hauler && !s.Buildings.Any(b => b.Def.CanServe(ShipClass.Hauler))
                    ? "No hangar: haulers can't land here."
                    : "No free spot. Hold, or wave off.";
                Ui.Text(why, "ws-small", "ws-warn").AddTo(card.Pads);
                return;
            }
            foreach (var b in free)
            {
                var target = b;
                var button = Ui.Button(s.Label(b).ToUpperInvariant(), () => _game.TryExecute(new AssignLanding(ship.Id, target.Id)), "ws-button--comms").AddTo(card.Pads);
                _ui.Tooltip(button, () => $"Clear to {s.Label(target)}");
            }
        }

        void UpdateTraffic(Station s)
        {
            _rowShips.Clear();
            _rowShips.AddRange(s.Ships.Where(x => x.State != ShipState.Hailing && x.State != ShipState.Gone).OrderBy(x => x.State).ThenBy(x => x.Id));
            Ui.Show(_trafficHeader, _rowShips.Count > 0);

            while (_rows.Count < _rowShips.Count)
            {
                int index = _rows.Count;
                var row = Ui.El("ws-traffic-row").AddTo(_traffic);
                var name = Ui.Text("", "ws-small").AddTo(row);
                var status = Ui.Text("", "ws-small", "ws-muted").AddTo(row);
                name.pickingMode = status.pickingMode = PickingMode.Ignore;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    if (index >= _rowShips.Count) return;
                    _game.SelectShip(_rowShips[index]);
                    _game.FocusOnShip(_rowShips[index]);
                });
                _rows.Add((row, name, status));
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < _rowShips.Count;
                Ui.Show(_rows[i].row, used);
                if (!used) continue;
                var ship = _rowShips[i];
                Ui.Set(_rows[i].name, ship.Name);
                Ui.Tone(_rows[i].name, _game.SelectedShip == ship ? "ws-comms" : null);
                Ui.Set(_rows[i].status, Status(s, ship));
            }
        }

        static string Status(Station s, Ship ship)
        {
            var b = ship.BuildingId is int id ? s.GetBuilding(id) : null;
            string where = b != null ? s.Label(b) : "";
            switch (ship.State)
            {
                case ShipState.Approaching: return "inbound";
                case ShipState.Landing: return $"landing · {where}";
                case ShipState.Servicing:
                    if (ship.FuelLoaded < ship.FuelNeeded) return $"refuelling {ship.FuelLoaded / ship.FuelNeeded:P0} · {where}";
                    return ship.NeedsRepair && !ship.Repaired ? $"repairs · {where}" : $"servicing · {where}";
                case ShipState.Departing: return ship.Angry ? "leaving angry" : "departing";
                default: return ship.State.ToString().ToLowerInvariant();
            }
        }
    }
}
