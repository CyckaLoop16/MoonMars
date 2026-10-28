using System.Linq;
using UnityEngine;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>
    /// Prototype HUD drawn with IMGUI: status bar, build toolbar, comms panel (hailing ships to clear),
    /// pad labels on the map, selected-module panel and event log. Placeholder until the real UI arrives.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        const float TopBarHeight = 32f;
        const float ToolbarHeight = 64f;
        const float SideWidth = 320f;
        const float Margin = 10f;

        GameController _game;
        GUIStyle _wrap, _rich, _small, _button, _smallButton, _activeButton, _error, _mapLabel, _mapShadow;
        System.Action _deferred;
        Vector2 _commsScroll;

        Rect TopBarRect => new Rect(0f, 0f, Screen.width, TopBarHeight);
        Rect ToolbarRect => new Rect(Screen.width / 2f - 400f, Screen.height - ToolbarHeight - Margin, 800f, ToolbarHeight);
        Rect CommsRect => new Rect(Screen.width - SideWidth - Margin, TopBarHeight + Margin, SideWidth, Mathf.Min(460f, Screen.height - TopBarHeight - ToolbarHeight - 4 * Margin));
        Rect InfoRect => new Rect(Margin, TopBarHeight + Margin, SideWidth, 220f);
        Rect LogRect => new Rect(Margin, Screen.height - 170f - Margin, 380f, 170f);

        public void Init(GameController game) => _game = game;

        public bool IsPointerOver(Vector2 screenPosition)
        {
            var p = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return TopBarRect.Contains(p) || ToolbarRect.Contains(p) || CommsRect.Contains(p) || LogRect.Contains(p)
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
            // Grim terminal look: dark panels, amber text (rich-text colours still override).
            GUI.backgroundColor = new Color(0.3f, 0.28f, 0.26f, 0.95f);
            GUI.contentColor = new Color(1f, 0.84f, 0.6f);

            DrawMapLabels();
            DrawShipLabels();
            DrawTopBar();
            DrawToolbar();
            DrawComms();
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
            _smallButton = new GUIStyle(_button) { fontSize = 11, padding = new RectOffset(4, 4, 2, 2) };
            _activeButton = new GUIStyle(_button) { normal = { textColor = new Color(1f, 0.85f, 0.3f) }, fontStyle = FontStyle.Bold };
            _error = new GUIStyle(_rich) { normal = { textColor = new Color(1f, 0.45f, 0.45f) } };
            _mapLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold, richText = true };
            _mapShadow = new GUIStyle(_mapLabel) { normal = { textColor = new Color(0f, 0f, 0f, 0.85f) } };
        }

        static string Clock(double seconds)
        {
            int s = (int)seconds;
            return $"{s / 60:00}:{s % 60:00}";
        }

        static string Bar(double fraction)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt((float)fraction * 8), 0, 8);
            return new string('|', filled) + new string('.', 8 - filled);
        }

        void ShadowLabel(Rect rect, string text, string plain)
        {
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), plain, _mapShadow);
            GUI.Label(rect, text, _mapLabel);
        }

        // ------------------------------------------------------------------ top bar

        void DrawTopBar()
        {
            var s = _game.Station;
            GUILayout.BeginArea(TopBarRect, GUI.skin.box);
            GUILayout.BeginHorizontal();

            GUILayout.Label("<b>WAYSTATION</b> <size=9>// DOCK CONTROL</size>", _rich, GUILayout.Width(190));
            var moneyColor = s.Money < 0 ? "#ff6b6b" : "#ffd36b";
            GUILayout.Label($"<color={moneyColor}><b>{s.Money:N0} cr</b></color>", _rich, GUILayout.Width(110));
            GUILayout.Label($"Reputation <b>{s.Reputation:0}</b>/100", _rich, GUILayout.Width(130));

            string tanker = s.FuelDeliveryTick >= 0 ? $"  (tanker in {(s.FuelDeliveryTick - s.Tick) / Station.TicksPerSecond}s)" : "";
            var fuelColor = s.FuelStock < s.FuelCapacity * Rules.RestockThreshold ? "#ff9f43" : "white";
            GUILayout.Label($"Fuel <color={fuelColor}>{s.FuelStock:0}/{s.FuelCapacity:0}</color>{tanker}", _rich, GUILayout.Width(220));
            GUILayout.Label($"Served {s.ShipsServed} · Lost {s.ShipsLostAngry + s.ShipsDiverted}", GUILayout.Width(150));
            GUILayout.Label(s.AutoAssign ? "ATC: automatic" : "ATC: <color=#ffd36b>manual</color> (no tower yet)", _rich, GUILayout.Width(200));

            GUILayout.FlexibleSpace();
            GUILayout.Label(Clock(s.Seconds), GUILayout.Width(50));
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
            BuildButton(BuildingType.Walkway, "C");
            BuildButton(BuildingType.Foundation, "F");
            ToolButton("Demolish\n<size=10>X · 50% refund</size>", _game.Tool == Tool.Demolish, () => _game.SetTool(Tool.Demolish));

            GUILayout.EndHorizontal();

            string hint;
            switch (_game.Tool)
            {
                case Tool.Build when _game.PlacementError != null && _game.HoverCell.HasValue:
                    hint = $"<color=#ff8a8a>{_game.PlacementError}</color>";
                    break;
                case Tool.Build:
                    hint = BuildingCatalog.Get(_game.BuildType).IsPaintable
                        ? "Click or drag to paint. Walkways and foundations link modules to the station."
                        : "Click to place. It must touch the station, a walkway or a foundation to get fuel.";
                    break;
                case Tool.ClearToLand:
                    hint = _game.PlacementError != null && _game.HoverCell.HasValue
                        ? $"<color=#ff8a8a>{_game.PlacementError}</color>"
                        : $"<color=#4fe3ff>Clearing {_game.ClearingShip?.Name}: click a pad marked green.</color> Esc to cancel.";
                    break;
                case Tool.Demolish:
                    hint = "Click a module to demolish it.";
                    break;
                default:
                    hint = "Click a hailing ship (blinking beacon) or press T to clear it. Space pause · 1/2/3 speed · Q/E rotate · wheel zoom";
                    break;
            }
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

        // ------------------------------------------------------------------ comms: hails and traffic

        void DrawComms()
        {
            var s = _game.Station;
            GUILayout.BeginArea(CommsRect, GUI.skin.box);
            var hails = s.Hailing.ToList();
            GUILayout.Label($"<b>Comms</b> · {hails.Count} hailing · {s.Ships.Count(x => x.State == ShipState.Approaching)} inbound", _rich);
            _commsScroll = GUILayout.BeginScrollView(_commsScroll);

            if (hails.Count == 0) GUILayout.Label("No ships hailing.", _small);
            foreach (var ship in hails) DrawHail(ship);

            GUILayout.Space(6);
            GUILayout.Label("<b>Traffic</b>", _rich);
            foreach (var ship in s.Ships.Where(x => x.State != ShipState.Hailing).OrderBy(x => x.State).ThenBy(x => x.ArrivedTick).Take(8))
            {
                var pad = ship.BuildingId.HasValue ? s.GetBuilding(ship.BuildingId.Value) : null;
                string where = pad != null ? $" · {s.Label(pad)}" : "";
                GUILayout.Label($"{ship.Name} <size=10>({ship.Class})</size>  {Status(ship)}{where}", _small);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawHail(Ship ship)
        {
            var s = _game.Station;
            bool clearing = _game.ClearingShip == ship;
            double patience = ship.PatienceTicks / (double)ship.MaxPatienceTicks;
            string urgency = patience < 0.33 ? "#ff8a8a" : "#ffd36b";

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"<color=#4fe3ff>HAILING</color>  <b>{ship.Name}</b> <size=10>({ship.Class})</size>", _small);
            GUILayout.Label($"Fuel {ship.FuelNeeded:0}{(ship.NeedsRepair ? " · <color=#c9a0ff>needs repairs</color>" : "")} · patience <color={urgency}>{Bar(patience)}</color>", _small);

            GUILayout.BeginHorizontal();
            var options = s.Buildings.Where(b => s.LandingError(ship, b) == null).OrderBy(b => b.Id).Take(4).ToList();
            foreach (var b in options)
            {
                int shipId = ship.Id, buildingId = b.Id;
                if (GUILayout.Button(s.Label(b), _smallButton)) _deferred = () => _game.TryExecute(new AssignLanding(shipId, buildingId));
            }
            if (options.Count == 0)
                GUILayout.Label(ship.Class == ShipClass.Hauler && !s.Buildings.Any(b => b.Def.CanServe(ShipClass.Hauler))
                    ? "<color=#ff8a8a>needs a hangar</color>" : "<color=#ff8a8a>no free pad</color>", _small);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(clearing ? "<b>Picking…</b>" : "Pick on map", _smallButton)) _deferred = () => _game.BeginClearance(ship);
            if (GUILayout.Button("Wave off", _smallButton)) _deferred = () => _game.TryExecute(new WaveOff(ship.Id));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        static string Status(Ship ship)
        {
            switch (ship.State)
            {
                case ShipState.Approaching: return "<color=#9ecbff>inbound</color>";
                case ShipState.Landing: return "<color=#ffb020>cleared, landing</color>";
                case ShipState.Servicing: return ServiceStatus(ship);
                case ShipState.Departing: return ship.Angry ? "<color=#ff8a8a>leaving angry</color>" : "<color=#88dd88>departing</color>";
                default: return "";
            }
        }

        static string ServiceStatus(Ship ship)
        {
            if (ship.FuelLoaded < ship.FuelNeeded) return $"<color=#88dd88>refuelling {Bar(ship.FuelLoaded / ship.FuelNeeded)}</color>";
            if (ship.NeedsRepair && !ship.Repaired) return $"<color=#c9a0ff>repairs {Bar(ship.StateProgress)}</color>";
            return "<color=#88dd88>paying</color>";
        }

        // ------------------------------------------------------------------ labels over the map

        void DrawMapLabels()
        {
            if (Event.current.type != EventType.Repaint) return;
            var s = _game.Station;
            var clearing = _game.Tool == Tool.ClearToLand ? _game.ClearingShip : null;

            foreach (var b in s.Buildings.Where(b => b.Def.Serves.Length > 0))
            {
                var sp = _game.WorldToScreen(BuildingRenderer.Center(b, s.Width, s.Height) + Vector3.up * 0.2f);
                if (sp.z <= 0f) continue;

                string label = s.Label(b).ToUpperInvariant();
                string color;
                if (clearing != null) color = s.LandingError(clearing, b) == null ? "#45ff8a" : "#ff6b7d";
                else if (!b.Connected) color = "#9aa0b0";
                else color = b.ShipId == null ? "#ffffff" : "#ffd36b";

                ShadowLabel(new Rect(sp.x - 60f, Screen.height - sp.y - 10f, 120f, 20f), $"<color={color}>{label}</color>", label);
            }
        }

        void DrawShipLabels()
        {
            if (Event.current.type != EventType.Repaint) return;
            foreach (var ship in _game.Station.Ships)
            {
                if (ship.State != ShipState.Hailing && ship.State != ShipState.Servicing) continue;
                if (!_game.TryGetShipScreenPosition(ship.Id, out var sp) || sp.z <= 0f) continue;

                string text, plain;
                if (ship.State == ShipState.Hailing)
                {
                    plain = $"HAILING {Bar(ship.PatienceTicks / (double)ship.MaxPatienceTicks)}";
                    text = $"<color=#4fe3ff>{plain}</color>";
                }
                else
                {
                    text = ServiceStatus(ship);
                    plain = System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", "");
                }
                ShadowLabel(new Rect(sp.x - 80f, Screen.height - sp.y - 26f, 160f, 20f), text, plain);
            }
        }

        // ------------------------------------------------------------------ selected module

        void DrawInfo(Building b)
        {
            var s = _game.Station;
            var def = b.Def;
            GUILayout.BeginArea(InfoRect, GUI.skin.box);
            GUILayout.Label($"<b>{s.Label(b)}</b>{(def.Label != null ? $" <size=10>({def.Name})</size>" : "")}", _rich);
            GUILayout.Label(def.Description, _small);
            GUILayout.Label(b.Connected ? "<color=#88dd88>Connected to the station</color>" : "<color=#ff6b6b>NOT CONNECTED: no fuel or power. Link it with walkways or foundations.</color>", _rich);

            if (def.Serves.Length > 0)
            {
                var ship = b.ShipId.HasValue ? s.GetShip(b.ShipId.Value) : null;
                GUILayout.Label($"Serves: {string.Join(", ", def.Serves)}{(def.Repairs ? " · repairs" : "")}", _small);
                GUILayout.Label(ship != null ? $"In use: {ship.Name}" : "Free", _small);
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
            if (Time.unscaledTime - _game.LastErrorTime < 3f)
                GUILayout.Label($"<b>{_game.LastError}</b>", _error);
            for (int i = s.Log.Count - 1; i >= 0 && i >= s.Log.Count - 7; i--)
            {
                var e = s.Log[i];
                GUILayout.Label($"<color=#8a93a6>{Clock(e.Tick / (double)Station.TicksPerSecond)}</color>  {e.Text}", _small);
            }
            GUILayout.EndArea();
        }
    }
}
