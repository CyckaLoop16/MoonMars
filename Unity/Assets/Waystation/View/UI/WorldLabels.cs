using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>
    /// Text pinned to the world: pad and hangar names (green/red while clearing a ship), status
    /// tags over hailing and docked ships, and "+N cr" floating up from a pad when a ship pays.
    /// </summary>
    public sealed class WorldLabels
    {
        const float MoneyLifetime = 1.8f;

        sealed class Floater
        {
            public Label Label;
            public Vector3 World;
            public float Age;
        }

        readonly GameController _game;
        readonly Dictionary<int, Label> _buildings = new Dictionary<int, Label>();
        readonly Dictionary<int, Label> _ships = new Dictionary<int, Label>();
        readonly List<Floater> _floaters = new List<Floater>();
        readonly HashSet<int> _seen = new HashSet<int>();

        public VisualElement Root { get; }

        public WorldLabels(GameController game)
        {
            _game = game;
            Root = Ui.El("ws-layer");
            Root.pickingMode = PickingMode.Ignore;
        }

        public void Reset()
        {
            Root.Clear();
            _buildings.Clear();
            _ships.Clear();
            _floaters.Clear();
        }

        /// <summary>Float an amount of money up from a building.</summary>
        public void SpawnMoney(int buildingId, double amount)
        {
            var b = _game.Station.GetBuilding(buildingId);
            if (b == null) return;
            var label = Ui.Text($"+{amount:N0} cr", "ws-float-money").AddTo(Root);
            label.pickingMode = PickingMode.Ignore;
            _floaters.Add(new Floater { Label = label, World = Center(b) + Vector3.up * 0.6f });
            Place(label, _floaters[_floaters.Count - 1].World);
        }

        public void Update()
        {
            if (Root.panel == null) return;
            var s = _game.Station;

            // Pad and hangar names.
            _seen.Clear();
            foreach (var b in s.Buildings.Where(b => b.Def.Label != null || !b.Enabled))
            {
                _seen.Add(b.Id);
                var label = Get(_buildings, b.Id, "ws-world-label");
                string name = b.Def.Label != null ? s.Label(b).ToUpperInvariant() : b.Def.Name.ToUpperInvariant();
                Ui.Set(label, b.Enabled ? name : name + " · OFF");

                string tone;
                if (_game.Tool == Tool.ClearToLand && _game.ClearingShip != null)
                    tone = s.LandingError(_game.ClearingShip, b) == null ? "ws-good" : "ws-bad";
                else if (!b.Operating) tone = "ws-muted";
                else tone = b.ShipId != null ? "ws-warn" : null;
                Ui.Tone(label, tone);
                Place(label, Center(b) + Vector3.up * 0.2f);
            }
            Prune(_buildings);

            // Ship tags.
            _seen.Clear();
            foreach (var ship in s.Ships)
            {
                string text = ShipTag(ship, out string tone);
                if (text == null || !_game.TryGetShipWorldPosition(ship.Id, out var world)) continue;
                _seen.Add(ship.Id);
                var label = Get(_ships, ship.Id, "ws-world-label");
                Ui.Set(label, text);
                Ui.Tone(label, tone);
                Place(label, world + Vector3.up * 1.1f);
            }
            Prune(_ships);

            // Money floaters rise and fade.
            for (int i = _floaters.Count - 1; i >= 0; i--)
            {
                var f = _floaters[i];
                f.Age += Time.unscaledDeltaTime;
                if (f.Age >= MoneyLifetime)
                {
                    f.Label.RemoveFromHierarchy();
                    _floaters.RemoveAt(i);
                    continue;
                }
                float t = f.Age / MoneyLifetime;
                Place(f.Label, f.World + Vector3.up * (t * 1.4f));
                f.Label.style.opacity = 1f - t * t;
            }
        }

        string ShipTag(Ship ship, out string tone)
        {
            tone = null;
            switch (ship.State)
            {
                case ShipState.Hailing:
                    tone = ship.PatienceTicks < ship.MaxPatienceTicks / 3 ? "ws-bad" : "ws-comms";
                    return $"HAILING · {ship.PatienceTicks / (double)Station.TicksPerSecond:0}s";
                case ShipState.Servicing:
                    if (ship.FuelLoaded < ship.FuelNeeded)
                    {
                        if (_game.Station.FuelStock <= 0)
                        {
                            tone = "ws-bad";
                            return "NO FUEL";
                        }
                        return $"FUEL {ship.FuelLoaded / ship.FuelNeeded:P0}";
                    }
                    if (ship.NeedsRepair && !ship.Repaired)
                    {
                        tone = "ws-warn";
                        return $"REPAIR {ship.StateProgress:P0}";
                    }
                    return null;
                case ShipState.Departing when ship.Angry:
                    tone = "ws-bad";
                    return "ANGRY";
                default:
                    return null;
            }
        }

        Label Get(Dictionary<int, Label> pool, int id, string cls)
        {
            if (pool.TryGetValue(id, out var label)) return label;
            label = Ui.Text("", cls).AddTo(Root);
            label.pickingMode = PickingMode.Ignore;
            pool[id] = label;
            return label;
        }

        void Prune(Dictionary<int, Label> pool)
        {
            foreach (var id in pool.Keys.Where(id => !_seen.Contains(id)).ToList())
            {
                pool[id].RemoveFromHierarchy();
                pool.Remove(id);
            }
        }

        Vector3 Center(Building b) => BuildingRenderer.Center(b, _game.Station.Width, _game.Station.Height);

        void Place(VisualElement e, Vector3 world)
        {
            var cam = _game.Camera;
            bool visible = cam.WorldToViewportPoint(world).z > 0f;
            Ui.Show(e, visible);
            if (!visible) return;
            var p = RuntimePanelUtils.CameraTransformWorldToPanel(Root.panel, world, cam);
            e.style.left = p.x;
            e.style.top = p.y - 8f;
        }
    }
}
