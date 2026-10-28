using System.Globalization;
using System.Linq;
using Regolith.Sim;
using UnityEngine;

namespace Regolith.View
{
    /// <summary>
    /// Prototype HUD drawn with IMGUI: top bar with End Turn, tile/research panel, turn report.
    /// Placeholder until the real UI Toolkit interface (and final art) arrives.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        enum Tab
        {
            Tile,
            Research,
        }

        const float TopBarHeight = 30f;
        const float PanelWidth = 400f;
        const float ReportWidth = 460f;
        const float ReportHeight = 230f;
        const float Margin = 10f;
        const string BaseName = "Shackleton Base";

        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        GameController _game;
        GUIStyle _wrap, _rich, _small, _error, _richButton, _landmark, _landmarkShadow;
        Vector2 _panelScroll, _reportScroll;
        Tab _tab;
        ICommand _pending;
        System.Action _deferred;

        Rect TopBarRect => new Rect(0f, 0f, Screen.width, TopBarHeight);
        Rect PanelRect => new Rect(Margin, TopBarHeight + Margin, PanelWidth, Screen.height - TopBarHeight - 2 * Margin);
        Rect ReportRect => new Rect(Screen.width - ReportWidth - Margin, Screen.height - ReportHeight - Margin, ReportWidth, ReportHeight);

        public void Init(GameController game) => _game = game;

        /// <summary>Screen position (bottom-left origin, as input reports it) over any HUD element?</summary>
        public bool IsPointerOver(Vector2 screenPosition)
        {
            var p = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return TopBarRect.Contains(p) || PanelRect.Contains(p) || ReportRect.Contains(p);
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
            DrawTurnReport();
        }

        void EnsureStyles()
        {
            if (_wrap != null) return;
            _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            _rich = new GUIStyle(_wrap) { richText = true };
            _small = new GUIStyle(_wrap) { fontSize = 11, richText = true };
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

            GUILayout.Label($"<b>Turn {_game.Sim.Turn}</b> · {state.Date.ToString("MMM yyyy", Invariant)}", _rich, GUILayout.Width(150));
            if (GUILayout.Button("<b>End turn ></b>", _richButton, GUILayout.Width(100))) _deferred = _game.EndTurn;

            GUILayout.Space(12);
            GUILayout.Label(SunlightStatus(state), GUILayout.Width(200));

            var colony = _game.PlayerColony;
            if (colony != null)
            {
                var r = colony.LastReport;
                GUILayout.Label($"Crew {colony.Pops}/{colony.Housing}", GUILayout.Width(80));
                GUILayout.Label(StockLabel("O2", colony, Resource.Oxygen), _rich, GUILayout.Width(130));
                GUILayout.Label(StockLabel("Water", colony, Resource.Water), _rich, GUILayout.Width(145));
                GUILayout.Label(StockLabel("Food", colony, Resource.Food), _rich, GUILayout.Width(140));
                GUILayout.Label(StockLabel("Materials", colony, Resource.Materials), _rich, GUILayout.Width(170));

                var powerColor = r.EnergySupply + 1e-6 >= r.EnergyDemand ? "white" : "#ff7766";
                var battery = colony.EnergyCapacity > 0 ? $"  Batt {colony.StoredEnergy:0}/{colony.EnergyCapacity:0}" : "";
                GUILayout.Label($"Power <color={powerColor}>{r.EnergySupply:0.0}/{r.EnergyDemand:0.0}</color>{battery}", _rich, GUILayout.Width(200));
            }

            GUILayout.Label(ResearchStatus(), _rich, GUILayout.Width(300));
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

        /// <summary>Stock with its change over the last turn (or per day before the first turn).</summary>
        string StockLabel(string name, Colony colony, Resource r)
        {
            bool perTurn = _game.LastTurnChange != null;
            double net = perTurn ? _game.LastTurnChange[(int)r] : colony.LastReport.NetOf(r);
            string color = net < -1e-6 ? "#ff7766" : net > 1e-6 ? "#88dd88" : "white";
            string change = perTurn ? $"{net:+0;-0;0}" : $"{net:+0.0;-0.0;0}/d";
            return $"{name} {colony.Stock[r]:0} <color={color}>({change})</color>";
        }

        string ResearchStatus()
        {
            var f = _game.Player;
            double perTurn = f.SciencePerDay * Economy.DaysPerMonth;
            if (f.CurrentResearch == null)
                return $"Science {perTurn:0}/turn · <color=#ffcc55>choose research</color>";

            var id = f.CurrentResearch.Value;
            f.Progress.TryGetValue(id, out var progress);
            return $"Science {perTurn:0}/turn · {TechCatalog.Get(id).Name} {progress:0}/{ResearchSystem.EffectiveCost(f, id):0}";
        }

        // ---------------------------------------------------------------- side panel

        void DrawPanel()
        {
            GUILayout.BeginArea(PanelRect, GUI.skin.box);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_tab == Tab.Tile ? "<b>Tile & Colony</b>" : "Tile & Colony", _richButton)) _deferred = () => _tab = Tab.Tile;
            var researchLabel = _game.Player.CurrentResearch == null ? "Research <color=#ffcc55>(!)</color>" : "Research";
            if (GUILayout.Button(_tab == Tab.Research ? $"<b>{researchLabel}</b>" : researchLabel, _richButton)) _deferred = () => _tab = Tab.Research;
            GUILayout.EndHorizontal();

            _panelScroll = GUILayout.BeginScrollView(_panelScroll);

            if (Time.unscaledTime - _game.LastErrorTime < 3f)
                GUILayout.Label($"<b>{_game.LastError}</b>", _error);

            if (_tab == Tab.Tile) DrawTileTab();
            else DrawResearchTab();

            GUILayout.FlexibleSpace();
            GUILayout.Label("Enter end turn · LMB select · WASD/RMB pan · Q/E rotate · wheel zoom · R new map", _small);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawTileTab()
        {
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
            else
            {
                GUILayout.Space(10);
                GUILayout.Label("<b>New map</b>", _rich);
                GUILayout.BeginHorizontal();
                foreach (MapSize size in System.Enum.GetValues(typeof(MapSize)))
                {
                    var label = size == _game.MapSize ? $"<b>{size}</b>" : size.ToString();
                    var mapSize = size;
                    if (GUILayout.Button(label, _richButton)) _deferred = () => _game.NewGame(mapSize);
                }
                GUILayout.EndHorizontal();
            }
        }

        void DrawTile(Tile tile)
        {
            var state = _game.State;
            var def = tile.Def;
            GUILayout.Label($"<b>{def.Name}</b>  {tile.Coord}", _rich);
            GUILayout.Label(def.Description, _wrap);

            // Ambient conditions: these just happen, no building needed.
            GUILayout.Label($"Sunlight now {Bodies.SolarAvailability(state.Body, tile, state.Day):P0}  ·  Ice {tile.Ice:P0}  ·  Minerals {tile.Minerals:P0}");
            if (tile.Feature == TileFeature.PeakOfLight) GUILayout.Label("Peak of light: sunlit ~90% of the time, even through the lunar night.", _wrap);
            if (tile.Feature == TileFeature.LavaTube) GUILayout.Label("Lava tube: natural radiation shelter.", _wrap);
            if (tile.Feature == TileFeature.CraterRim) GUILayout.Label("Crater rim: exposed strata (+1 Science).", _wrap);

            var deposit = DepositCatalog.Get(tile.Deposit);
            if (deposit != null)
            {
                var kind = deposit.Kind == DepositKind.Bonus ? "bonus resource" : "strategic resource";
                GUILayout.Label($"<color={deposit.Color}><b>{deposit.Name}</b></color> ({kind})", _rich);
                GUILayout.Label(deposit.Description, _wrap);
                if (deposit.Kind == DepositKind.Strategic)
                    GUILayout.Label("Mining strategic resources arrives with Earth trade and outposts (next milestones).", _small);
            }

            var y = tile.Yields;
            if (y.Materials + y.Science > 0)
                GUILayout.Label($"Automatic yield when claimed: +{y.Materials} materials, +{y.Science} science per turn", _small);

            var landmark = state.Map.LandmarkAt(tile.Coord);
            if (landmark != null)
            {
                GUILayout.Label($"<b>{landmark.Name}</b> ({landmark.Kind.ToString().ToLowerInvariant()}, ~{landmark.Radius * 2 * LunarMapGenerator.KmPerHex:0} km)", _rich);
                GUILayout.Label(landmark.Description, _wrap);
            }

            var owner = state.ColonyOwning(tile.Coord);
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
                DrawModule(colony, module, tile);
                return;
            }

            GUILayout.Label("<b>Build</b>", _rich);
            foreach (var def in ModuleCatalog.All.Where(d => d.Buildable))
            {
                var place = new PlaceModule(colony.Id, tile.Coord, def.Type);
                string bonus = DepositCatalog.Multiplier(tile.Deposit, def.Type) > 1.0 ? "  <color=#88dd88>+50% here</color>" : "";
                if (ActionButton($"{def.Name}  ·  {def.MaterialCost} mat  ·  {Modifiers.BuildDays(_game.Player, def)} d{bonus}", place.Validate(state)))
                    _pending = place;
            }
        }

        void DrawModule(Colony colony, Module module, Tile tile)
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
            if (def.Science > 0) GUILayout.Label($"Science {def.Science * module.Efficiency * Economy.DaysPerMonth:0}/turn");
            if (def.EnergyStorage > 0) GUILayout.Label($"Stores up to {def.EnergyStorage:0} energy");
            if (DepositCatalog.Multiplier(tile.Deposit, def.Type) > 1.0) GUILayout.Label("<color=#88dd88>Deposit bonus: +50% output</color>", _rich);
            if (def.Inputs.Length + def.Outputs.Length > 0 || def.IsSolar || def.Science > 0) GUILayout.Label($"Running at {module.Efficiency:P0}");
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

        // ---------------------------------------------------------------- research

        void DrawResearchTab()
        {
            var f = _game.Player;
            double perTurn = f.SciencePerDay * Economy.DaysPerMonth;
            GUILayout.Label($"<b>Research</b> · {perTurn:0} science per turn", _rich);
            GUILayout.Label("Tier: <b>Proven</b>. Real engineering that exists today. Boosts (*) cut a tech's cost by 40% when you achieve them in play.", _small);
            GUILayout.Space(6);

            foreach (var tech in TechCatalog.All)
            {
                bool done = f.Has(tech.Id);
                bool current = f.CurrentResearch == tech.Id;
                double cost = ResearchSystem.EffectiveCost(f, tech.Id);
                f.Progress.TryGetValue(tech.Id, out var progress);

                string status = done ? "<color=#88dd88>researched</color>"
                    : current ? $"<color=#ffcc55>researching {progress:0}/{cost:0}{TurnsLeft(cost - progress, perTurn)}</color>"
                    : $"{cost:0} science{TurnsLeft(cost - progress, perTurn)}";
                GUILayout.Label($"<b>{tech.Name}</b>  {status}", _rich);
                GUILayout.Label(tech.Description, _small);

                if (!done)
                    GUILayout.Label(f.Boosted.Contains(tech.Id) ? "<color=#88dd88>* boosted</color>" : $"* Boost: {tech.BoostText}", _small);

                if (!done && !current)
                {
                    var set = new SetResearch(f.Id, tech.Id);
                    if (ActionButton("Research this", set.Validate(_game.State))) _pending = set;
                }
                GUILayout.Space(6);
            }
        }

        static string TurnsLeft(double remaining, double perTurn) =>
            perTurn > 0 ? $" · ~{System.Math.Max(1, System.Math.Ceiling(remaining / perTurn)):0} turns" : "";

        /// <summary>A button that is disabled (with the reason shown) when the action isn't possible.</summary>
        bool ActionButton(string label, string error)
        {
            GUI.enabled = error == null;
            bool clicked = GUILayout.Button(label, _richButton);
            GUI.enabled = true;
            if (error != null) GUILayout.Label(error, _small);
            return clicked && error == null;
        }

        // ---------------------------------------------------------------- turn report

        void DrawTurnReport()
        {
            var log = _game.State.Log;
            int from = _game.LastTurnStartDay;
            bool hasTurn = _game.Sim.Turn > 1;

            GUILayout.BeginArea(ReportRect, GUI.skin.box);
            GUILayout.Label(hasTurn
                ? $"<b>Turn report</b> · {GameState.StartDate.AddDays(from).ToString("MMMM yyyy", Invariant)}"
                : "<b>Mission log</b>", _rich);

            _reportScroll = GUILayout.BeginScrollView(_reportScroll);
            for (int i = log.Count - 1; i >= 0; i--)
            {
                var e = log[i];
                if (hasTurn && e.Day < from) break;
                var date = GameState.StartDate.AddDays(e.Day).ToString("d MMM", Invariant);
                GUILayout.Label($"<color=#aaaaaa>{date}</color>  {e.Text}", _small);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
