using System.Linq;
using UnityEngine;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>
    /// Prototype HUD drawn with IMGUI: status bar, build toolbar, traffic board, selected-module
    /// panel and event log. Placeholder until the real UI (and final art) arrives.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        const float TopBarHeight = 32f;
        const float ToolbarHeight = 64f;
        const float SideWidth = 300f;
        const float Margin = 10f;

        GameController _game;
        GUIStyle _wrap, _rich, _small, _button, _activeButton, _error, _shipLabel;
        System.Action _deferred;

        Rect TopBarRect => new Rect(0f, 0f, Screen.width, TopBarHeight);
        Rect ToolbarRect => new Rect(Screen.width / 2f - 330f, Screen.height - ToolbarHeight - Margin, 660f, ToolbarHeight);
        Rect TrafficRect => new Rect(Screen.width - SideWidth - Margin, TopBarHeight + Margin, SideWidth, 260f);
        Rect InfoRect => new Rect(Margin, TopBarHeight + Margin, SideWidth, 210f);
        Rect LogRect => new Rect(Margin, Screen.height - 170f - Margin, 360f, 170f);

        public void Init(GameController game) => _game = game;

        public bool IsPointerOver(Vector2 screenPosition)
        {
            var p = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return TopBarRect.Contains(p) || ToolbarRect.Contains(p) || TrafficRect.Contains(p) || LogRect.Contains(p)
                   || (_game.Selected != null && InfoRect.Contains(p));
        }

        void Update()
        {
            // Clicks run here, not inside OnGUI, so the layout never changes mid-frame.
            if (_deferred == null) return;
            var action = _deferred;
            _deferred = null;
            action();
        }

        void OnGUI()
        {
            if (_game == null || _game.Sim == null) return;
            EnsureStyles();

            DrawShipLabels();
            DrawTopBar();
            DrawToolbar();
            DrawTraffic();
            if (_game.Selected != null) DrawInfo(_game.Selected);
            DrawLog();
        }

        void EnsureStyles()
        {
            if (_wrap != null) return;
            _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            _rich = new GUIStyle(_wrap) { richText = true };
            _small = new GUIStyle(_rich) { fontSize = 11 };
            _button = new GUIStyle(GUI.skin.button) { richText = true, fontSize = 12 };
            _activeButton = new GUIStyle(_button) { normal = { textColor = new Color(1f, 0.85f, 0.3f) }, fontStyle = FontStyle.Bold };
            _error = new GUIStyle(_rich) { normal = { textColor = new Color(1f, 0.45f, 0.45f) } };
            _shipLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, richText = true };
        }

        static string Time(double seconds)
        {
            int s = (int)seconds;
            return $"{s / 60:00}:{s % 60:00}";
        }

        // ------------------------------------------------------------------ top bar

        void DrawTopBar()
        {
            var s = _game.Station;
            GUILayout.BeginArea(TopBarRect, GUI.skin.box);
            GUILayout.BeginHorizontal();

            GUILayout.Label("<b>WAYSTATION</b>", _rich, GUILayout.Width(110));
            var moneyColor = s.Money < 0 ? "#ff6b6b" : "#ffd36b";
            GUILayout.Label($"<color={moneyColor}><b>{s.Money:N0} cr</b></color>", _rich, GUILayout.Width(110));
            GUILayout.Label($"Reputation <b>{s.Reputation:0}</b>/100", _rich, GUILayout.Width(130));

            string tanker = s.FuelDeliveryTick >= 0 ? $"  (tanker in {(s.FuelDeliveryTick - s.Tick) / Station.TicksPerSecond}s)" : "";
            var fuelColor = s.FuelStock < s.FuelCapacity * Rules.RestockThreshold ? "#ff9f43" : "white";
            GUILayout.Label($"Fuel <color={fuelColor}>{s.FuelStock:0}/{s.FuelCapacity:0}</color>{tanker}", _rich, GUILayout.Width(220));
            GUILayout.Label($"Served {s.ShipsServed} · Lost {s.ShipsLostAngry + s.ShipsDiverted}", GUILayout.Width(150));

            GUILayout.FlexibleSpace();
            GUILayout.Label(Time(s.Seconds), GUILayout.Width(50));
            if (GUILayout.Button(_game.Speed == 0 ? "> Play" : "|| Pause", _button, GUILayout.Width(70))) _deferred = _game.TogglePause;
            for (int i = 1; i < GameController.SpeedMultipliers.Length; i++)
            {
                int speed = i;
                if (GUILayout.Button($"{GameController.SpeedMultipliers[i]:0}x", _game.Speed == i ? _activeButton : _button, GUILayout.Width(38)))
                    _deferred = () => _game.SetSpeed(speed);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ------------------------------------------------------------------ toolbar

        void DrawToolbar()
        {
            GUILayout.BeginArea(ToolbarRect, GUI.skin.box);
            GUILayout.BeginHorizontal();

            ToolButton("Select\n<size=10>Esc</size>", _game.Tool == Tool.Select, () => _game.SetTool(Tool.Select));
            BuildButton(BuildingType.LandingPad, "P");
            BuildButton(BuildingType.Hangar, "H");
            BuildButton(BuildingType.Corridor, "C");
            ToolButton("Demolish\n<size=10>X · 50% refund</size>", _game.Tool == Tool.Demolish, () => _game.SetTool(Tool.Demolish));

            GUILayout.EndHorizontal();

            string hint = _game.Tool switch
            {
                Tool.Build when _game.PlacementError != null && _game.HoverCell.HasValue => $"<color=#ff8a8a>{_game.PlacementError}</color>",
                Tool.Build => BuildingCatalog.Get(_game.BuildType).IsCorridor
                    ? "Click or drag to lay corridors. They link buildings to the station."
                    : "Click to place. It must touch the station or a corridor to get fuel.",
                Tool.Demolish => "Click a module to demolish it.",
                _ => "Click a module to inspect it. Space pause · 1/2/3 speed · Q/E rotate · wheel zoom",
            };
            GUILayout.Label(hint, _small);
            GUILayout.EndArea();
        }

        void BuildButton(BuildingType type, string key)
        {
            var def = BuildingCatalog.Get(type);
            bool active = _game.Tool == Tool.Build && _game.BuildType == type;
            string cost = _game.Station.Money >= def.Cost ? $"{def.Cost:0} cr" : $"<color=#ff8a8a>{def.Cost:0} cr</color>";
            ToolButton($"{def.Name}\n<size=10>{cost} · {key}</size>", active, () => _game.StartBuilding(type));
        }

        void ToolButton(string label, bool active, System.Action onClick)
        {
            if (GUILayout.Button(label, active ? _activeButton : _button, GUILayout.Height(36))) _deferred = onClick;
        }

        // ------------------------------------------------------------------ traffic board

        void DrawTraffic()
        {
            var s = _game.Station;
            GUILayout.BeginArea(TrafficRect, GUI.skin.box);
            int holding = s.Ships.Count(x => x.State == ShipState.Holding || x.State == ShipState.Approaching);
            GUILayout.Label($"<b>Traffic</b> · holding {holding}/{Rules.MaxHolding}", _rich);

            foreach (var ship in s.Ships.OrderBy(x => x.State).ThenBy(x => x.ArrivedTick).Take(10))
            {
                string status = ship.State switch
                {
                    ShipState.Approaching => "<color=#9ecbff>inbound</color>",
                    ShipState.Holding => $"<color={(ship.PatienceTicks < ship.MaxPatienceTicks / 3 ? "#ff8a8a" : "#ffd36b")}>waiting {Bar(ship.PatienceTicks / (double)ship.MaxPatienceTicks)}</color>",
                    ShipState.Landing => "<color=#9ecbff>landing</color>",
                    ShipState.Servicing => ServiceStatus(ship),
                    ShipState.Departing => ship.Angry ? "<color=#ff8a8a>leaving angry</color>" : "<color=#88dd88>departing</color>",
                    _ => "",
                };
                GUILayout.Label($"{ship.Name} <size=10>({ship.Class})</size>  {status}", _small);
            }
            GUILayout.EndArea();
        }

        static string ServiceStatus(Ship ship)
        {
            if (ship.FuelLoaded < ship.FuelNeeded) return $"<color=#88dd88>refuelling {Bar(ship.FuelLoaded / ship.FuelNeeded)}</color>";
            if (ship.NeedsRepair && !ship.Repaired) return $"<color=#c9a0ff>repairs {Bar(ship.StateProgress)}</color>";
            return "<color=#88dd88>paying</color>";
        }

        static string Bar(double fraction)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt((float)fraction * 8), 0, 8);
            return new string('|', filled) + new string('.', 8 - filled);
        }

        void DrawShipLabels()
        {
            if (Event.current.type != EventType.Repaint) return;
            foreach (var ship in _game.Station.Ships)
            {
                if (ship.State != ShipState.Holding && ship.State != ShipState.Servicing) continue;
                if (!_game.TryGetShipScreenPosition(ship.Id, out var sp) || sp.z <= 0f) continue;

                string text = ship.State == ShipState.Holding
                    ? $"<color=#ffd36b>{Bar(ship.PatienceTicks / (double)ship.MaxPatienceTicks)}</color>"
                    : $"<color=#88dd88>{ServiceStatus(ship)}</color>";
                GUI.Label(new Rect(sp.x - 70f, Screen.height - sp.y - 10f, 140f, 20f), text, _shipLabel);
            }
        }

        // ------------------------------------------------------------------ selected module

        void DrawInfo(Building b)
        {
            var s = _game.Station;
            var def = b.Def;
            GUILayout.BeginArea(InfoRect, GUI.skin.box);
            GUILayout.Label($"<b>{def.Name}</b>", _rich);
            GUILayout.Label(def.Description, _small);
            GUILayout.Label(b.Connected ? "<color=#88dd88>Connected to the station</color>" : "<color=#ff6b6b>NOT CONNECTED: no fuel or power. Link it with corridors.</color>", _rich);

            if (def.Serves.Length > 0)
            {
                var ship = b.ShipId.HasValue ? s.GetShip(b.ShipId.Value) : null;
                GUILayout.Label($"Serves: {string.Join(", ", def.Serves)}{(def.Repairs ? " · repairs" : "")}", _small);
                GUILayout.Label(ship != null ? $"Docked: {ship.Name}" : "Free", _small);
                GUILayout.Label($"Ships served {b.ShipsServed} · earned {b.Earned:N0} cr", _small);
            }
            if (def.FuelCapacity > 0) GUILayout.Label($"Fuel tank: {def.FuelCapacity:0} units", _small);
            GUILayout.Label($"Upkeep {def.UpkeepPerMinute:0.#} cr/min", _small);

            if (!def.IsHub && GUILayout.Button($"Demolish (+{def.Cost * Rules.DemolishRefund:0} cr)", _button))
                _deferred = () => _game.TryExecute(new Demolish(b.Id));
            GUILayout.EndArea();
        }

        // ------------------------------------------------------------------ log

        void DrawLog()
        {
            var s = _game.Station;
            GUILayout.BeginArea(LogRect, GUI.skin.box);
            if (UnityEngine.Time.unscaledTime - _game.LastErrorTime < 3f)
                GUILayout.Label($"<b>{_game.LastError}</b>", _error);
            for (int i = s.Log.Count - 1; i >= 0 && i >= s.Log.Count - 7; i--)
            {
                var e = s.Log[i];
                GUILayout.Label($"<color=#8a93a6>{Time(e.Tick / (double)Station.TicksPerSecond)}</color>  {e.Text}", _small);
            }
            GUILayout.EndArea();
        }
    }
}
