using System.Globalization;
using System.Linq;
using Regolith.Sim;
using UnityEngine;

namespace Regolith.View
{
    /// <summary>
    /// Prototype HUD drawn with IMGUI: top resource bar, tile/colony panel, event log.
    /// Placeholder until the real UI Toolkit interface (and final art) arrives.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        const float TopBarHeight = 30f;
        const float PanelWidth = 380f;
        const float LogHeight = 170f;
        const float Margin = 10f;
        const int LogLines = 8;
        const string BaseName = "Shackleton Base";

        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        GameController _game;
        GUIStyle _wrap, _rich, _small, _error, _richButton, _landmark, _landmarkShadow;
        Vector2 _panelScroll;
        ICommand _pending;
        System.Action _deferred;

        Rect TopBarRect => new Rect(0f, 0f, Screen.width, TopBarHeight);
        Rect PanelRect => new Rect(Margin, TopBarHeight + Margin, PanelWidth, Screen.height - TopBarHeight - LogHeight - 3 * Margin);
        Rect LogRect => new Rect(Margin, Screen.height - LogHeight - Margin, 600f, LogHeight);

        public void Init(GameController game) => _game = game;

        /// <summary>Screen position (bottom-left origin, as input reports it) over any HUD element?</summary>
        public bool IsPointerOver(Vector2 screenPosition)
        {
            var p = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return TopBarRect.Contains(p) || PanelRect.Contains(p) || LogRect.Contains(p);
        }

        void Update()
        {
            // Button clicks run here, not inside OnGUI, so the layout never
            // changes between IMGUI's layout and event passes.
            if (_deferred != null)
            {
                var action = _deferred;
                _deferred = null;
                action();
            }

            if (_pending == null) return;
            _game.TryExecute(_pending);
            _pending = null;
        }

        void OnGUI()
        {
            if (_game == null || _game.Sim == null) return;
            EnsureStyles();

            DrawLandmarkLabels();
            DrawTopBar();
            DrawPanel();
            DrawLog();
        }

        void EnsureStyles()
        {
            if (_wrap != null) return;
            _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            _rich = new GUIStyle(_wrap) { richText = true };
            _small = new GUIStyle(_wrap) { fontSize = 11 };
            _error = new GUIStyle(_rich) { normal = { textColor = new Color(1f, 0.45f, 0.4f) } };
            _richButton = new GUIStyle(GUI.skin.button) { richText = true };
            _landmark = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13,
                normal = { textColor = new Color(1f, 0.95f, 0.8f) },
            };
            _landmarkShadow = new GUIStyle(_landmark) { normal = { textColor = new Color(0f, 0f, 0f, 0.8f) } };
        }

        // ---------------------------------------------------------------- map labels

        void DrawLandmarkLabels()
        {
            var cam = Camera.main;
            if (cam == null || Event.current.type != EventType.Repaint) return;

            var map = _game.State.Map;
            foreach (var landmark in map.Landmarks)
            {
                var tile = map.GetTile(landmark.Center);
                if (tile == null) continue;

                var world = HexMapRenderer.ToWorld(landmark.Center) + Vector3.up * (HexMapRenderer.TopHeight(tile) + 0.6f);
                var sp = cam.WorldToScreenPoint(world);
                if (sp.z <= 0f) continue;

                var rect = new Rect(sp.x - 100f, Screen.height - sp.y - 12f, 200f, 24f);
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), landmark.Name, _landmarkShadow);
                GUI.Label(rect, landmark.Name, _landmark);
            }
        }

        // ---------------------------------------------------------------- top bar

        void DrawTopBar()
        {
            var state = _game.State;
            GUILayout.BeginArea(TopBarRect, GUI.skin.box);
            GUILayout.BeginHorizontal();

            GUILayout.Label($"<b>{state.Date.ToString("d MMM yyyy", Invariant)}</b>", _rich, GUILayout.Width(100));

            if (GUILayout.Button(_game.TurnBased ? "Mode: Turns" : "Mode: Real-time", GUILayout.Width(120)))
                _deferred = _game.ToggleMode;

            if (_game.TurnBased)
            {
                GUILayout.Label($"<b>Turn {_game.Sim.Turn}</b>", _rich, GUILayout.Width(70));
                if (GUILayout.Button("End turn >", GUILayout.Width(100))) _deferred = _game.EndTurn;
            }
            else
            {
                if (GUILayout.Button(_game.Speed == 0 ? "> Play" : "|| Pause", GUILayout.Width(70))) _deferred = _game.TogglePause;
                for (int s = 1; s < GameController.DaysPerSecond.Length; s++)
                {
                    int speed = s;
                    var label = _game.Speed == s ? $"<b>[{s}]</b>" : s.ToString();
                    if (GUILayout.Button(label, _richButton, GUILayout.Width(30))) _deferred = () => _game.SetSpeed(speed);
                }
            }

            GUILayout.Space(12);
            GUILayout.Label(SunlightStatus(state), GUILayout.Width(210));

            var colony = _game.PlayerColony;
            if (colony != null)
            {
                var r = colony.LastReport;
                GUILayout.Label($"<b>{colony.Name}</b>  Crew {colony.Pops}/{colony.Housing}", _rich, GUILayout.Width(190));
                GUILayout.Label(StockLabel("O2", colony, Resource.Oxygen), _rich, GUILayout.Width(130));
                GUILayout.Label(StockLabel("Water", colony, Resource.Water), _rich, GUILayout.Width(145));
                GUILayout.Label(StockLabel("Food", colony, Resource.Food), _rich, GUILayout.Width(140));
                GUILayout.Label(StockLabel("Materials", colony, Resource.Materials), _rich, GUILayout.Width(170));
                var powerColor = r.EnergySupply + 1e-6 >= r.EnergyDemand ? "white" : "#ff7766";
                GUILayout.Label($"Power <color={powerColor}>{r.EnergySupply:0.0}/{r.EnergyDemand:0.0}</color>", _rich, GUILayout.Width(120));
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static string SunlightStatus(GameState state)
        {
            if (state.Body != BodyId.Moon) return Bodies.Name(state.Body);

            double phase = state.Day % Bodies.LunarSynodicDays;
            double half = Bodies.LunarSynodicDays / 2;
            return Bodies.IsLunarDaylight(state.Day)
                ? $"Lunar day: sunset in {half - phase:0} d"
                : $"Lunar NIGHT: sunrise in {Bodies.LunarSynodicDays - phase:0} d";
        }

        /// <summary>Stock with its change: per day in real-time, over the last turn in turn mode.</summary>
        string StockLabel(string name, Colony colony, Resource r)
        {
            bool perTurn = _game.TurnBased && _game.LastTurnChange != null;
            double net = perTurn ? _game.LastTurnChange[(int)r] : colony.LastReport.NetOf(r);
            string color = net < -1e-6 ? "#ff7766" : net > 1e-6 ? "#88dd88" : "white";
            string change = perTurn ? $"{net:+0;-0;0}/turn" : $"{net:+0.0;-0.0;0}/d";
            return $"{name} {colony.Stock[r]:0} <color={color}>({change})</color>";
        }

        // ---------------------------------------------------------------- side panel

        void DrawPanel()
        {
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            _panelScroll = GUILayout.BeginScrollView(_panelScroll);

            if (Time.unscaledTime - _game.LastErrorTime < 3f)
                GUILayout.Label($"<b>{_game.LastError}</b>", _error);

            var tile = _game.Selected;
            if (tile == null)
            {
                GUILayout.Label("<b>REGOLITH</b> · Act I: Luna", _rich);
                GUILayout.Label(_game.PlayerColony == null
                    ? "Click a tile to inspect it. Land your first base next to a permanently shadowed crater (water ice) and, ideally, a peak of light (near-constant sunlight). The rim of Shackleton, at the pole, is the classic choice."
                    : "Click a tile to inspect it or build on it.", _wrap);
            }
            else
            {
                DrawTile(tile);
                DrawActions(tile);
            }

            if (_game.PlayerColony != null)
            {
                GUILayout.Space(10);
                DrawColony(_game.PlayerColony);
            }

            if (_game.PlayerColony == null)
            {
                GUILayout.Space(10);
                GUILayout.Label("<b>New map</b>", _rich);
                GUILayout.BeginHorizontal();
                foreach (MapSize size in System.Enum.GetValues(typeof(MapSize)))
                {
                    var (w, h) = GameSetup.Dimensions(size);
                    var label = size == _game.MapSize ? $"<b>{size}</b>" : size.ToString();
                    var mapSize = size;
                    if (GUILayout.Button(new GUIContent(label, $"{w}x{h} hexes"), _richButton)) _deferred = () => _game.NewGame(mapSize);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(_game.TurnBased
                ? "Enter end turn · T real-time · LMB select · WASD/RMB pan · Q/E rotate · wheel zoom · R new map"
                : "Space pause · 1-5 speed · T turns · LMB select · WASD/RMB pan · Q/E rotate · wheel zoom · R new map", _small);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawTile(Tile tile)
        {
            var def = tile.Def;
            GUILayout.Label($"<b>{def.Name}</b>  {tile.Coord}", _rich);
            GUILayout.Label(def.Description, _wrap);
            GUILayout.Label($"Ice {tile.Ice:P0}  ·  Minerals {tile.Minerals:P0}  ·  Sunlight now {Bodies.SolarAvailability(_game.State.Body, tile, _game.State.Day):P0}");
            if (tile.Feature == TileFeature.PeakOfLight) GUILayout.Label("Peak of light: sunlit ~90% of the time, even at night.", _wrap);
            if (tile.Feature == TileFeature.LavaTube) GUILayout.Label("Lava tube: natural radiation shelter.", _wrap);
            if (tile.Feature == TileFeature.CraterRim) GUILayout.Label("Crater rim: exposed strata.", _wrap);

            var landmark = _game.State.Map.LandmarkAt(tile.Coord);
            if (landmark != null)
            {
                GUILayout.Label($"<b>{landmark.Name}</b> ({landmark.Kind.ToString().ToLowerInvariant()}, ~{landmark.Radius * 2 * LunarMapGenerator.KmPerHex:0} km)", _rich);
                GUILayout.Label(landmark.Description, _wrap);
            }

            var owner = _game.State.ColonyOwning(tile.Coord);
            if (owner != null) GUILayout.Label($"Territory of <b>{owner.Name}</b>", _rich);
        }

        void DrawActions(Tile tile)
        {
            var state = _game.State;
            var colony = _game.PlayerColony;
            GUILayout.Space(8);

            if (colony == null)
            {
                var land = new FoundColony(GameSetup.PlayerFactionId, tile.Coord, BaseName);
                if (ActionButton($"Land {BaseName} here", land.Validate(state))) _pending = land;
                return;
            }

            if (!colony.Territory.Contains(tile.Coord)) return;

            var module = colony.ModuleAt(tile.Coord);
            if (module != null)
            {
                DrawModule(colony, module);
                return;
            }

            GUILayout.Label("<b>Build</b>", _rich);
            foreach (var def in ModuleCatalog.All.Where(d => d.Buildable))
            {
                var place = new PlaceModule(colony.Id, tile.Coord, def.Type);
                if (ActionButton($"{def.Name}  ·  {def.MaterialCost} mat  ·  {def.BuildDays} d", place.Validate(state)))
                    _pending = place;
            }
        }

        void DrawModule(Colony colony, Module module)
        {
            var def = module.Def;
            GUILayout.Label($"<b>{def.Name}</b>", _rich);
            GUILayout.Label(def.Description, _wrap);

            if (!module.IsBuilt)
            {
                bool active = colony.UnderConstruction == module;
                GUILayout.Label(active ? $"Under construction: {module.BuildDaysRemaining} days left" : "Queued for construction");
                return;
            }

            if (def.Jobs > 0) GUILayout.Label($"Crew {module.Workers}/{def.Jobs}");
            if (def.Housing > 0) GUILayout.Label($"Houses {def.Housing}");
            if (def.Inputs.Length + def.Outputs.Length > 0 || def.IsSolar) GUILayout.Label($"Running at {module.Efficiency:P0}");
        }

        void DrawColony(Colony colony)
        {
            var r = colony.LastReport;
            GUILayout.Label($"<b>{colony.Name}</b>{(colony.IsAlive ? "" : "  (LOST)")}", _rich);
            GUILayout.Label($"Crew {colony.Pops}/{colony.Housing}  ·  Jobs filled {r.Workers}/{r.Jobs}");

            var queue = colony.Modules.Where(m => !m.IsBuilt).ToList();
            if (queue.Count > 0)
                GUILayout.Label("Building: " + string.Join(", ", queue.Select(m => $"{m.Def.Name} ({m.BuildDaysRemaining} d)")), _wrap);
        }

        /// <summary>A button that is disabled (with the reason shown) when the action isn't possible.</summary>
        bool ActionButton(string label, string error)
        {
            GUI.enabled = error == null;
            bool clicked = GUILayout.Button(label);
            GUI.enabled = true;
            if (error != null) GUILayout.Label(error, _small);
            return clicked && error == null;
        }

        // ---------------------------------------------------------------- event log

        void DrawLog()
        {
            var log = _game.State.Log;
            GUILayout.BeginArea(LogRect, GUI.skin.box);
            for (int i = log.Count - 1; i >= 0 && i >= log.Count - LogLines; i--)
            {
                var e = log[i];
                var date = GameState.StartDate.AddDays(e.Day).ToString("d MMM yyyy", Invariant);
                GUILayout.Label($"<color=#aaaaaa>{date}</color>  {e.Text}", _rich);
            }
            GUILayout.EndArea();
        }
    }
}
