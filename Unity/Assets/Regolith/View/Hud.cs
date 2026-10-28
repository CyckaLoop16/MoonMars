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
            Earth,
            Pioneers,
        }

        /// <summary>Pioneer name labels show when the camera is closer than this.</summary>
        const float PioneerLabelDistance = 25f;

        const float TopBarHeight = 30f;
        const float PanelWidth = 400f;
        const float ReportWidth = 460f;
        const float ReportHeight = 230f;
        const float Margin = 10f;
        const string BaseName = "Shackleton Base";

        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        GameController _game;
        GUIStyle _wrap, _rich, _small, _error, _richButton, _landmark, _landmarkShadow, _pioneerLabel;
        int _selectedPioneerId;
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
            DrawPioneerLabels();
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
            _pioneerLabel = new GUIStyle(_landmark) { fontSize = 11, fontStyle = FontStyle.Normal, richText = true };
        }

        void DrawPioneerLabels()
        {
            var cam = _game.Camera;
            if (cam == null || Event.current.type != EventType.Repaint) return;
            bool close = _game.CameraDistance < PioneerLabelDistance;

            foreach (var p in _game.State.Pioneers.Where(p => p.IsAlive && p.FactionId == GameSetup.PlayerFactionId))
            {
                bool selected = p.Id == _selectedPioneerId;
                if (!close && !selected) continue;
                if (!_game.TryGetPioneerPosition(p.Id, out var pos)) continue;

                var sp = cam.WorldToScreenPoint(pos + Vector3.up * 0.35f);
                if (sp.z <= 0f) continue;

                var color = selected ? "#ffcc55" : "#" + ColorUtility.ToHtmlStringRGB(PioneerRenderer.ColorOf(p.Expertise));
                var rect = new Rect(sp.x - 90f, Screen.height - sp.y - 10f, 180f, 20f);
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), p.Name, _landmarkShadow);
                GUI.Label(rect, $"<color={color}>{p.Name}</color>", _pioneerLabel);
            }
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

            var player = _game.Player;
            GUILayout.Label($"Credits <b>{player.Credits:0}</b> (+{EarthSystem.Funding(state, player):0})", _rich, GUILayout.Width(150));
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
            TabButton(Tab.Tile, "Tile");
            TabButton(Tab.Research, _game.Player.CurrentResearch == null ? "Research <color=#ffcc55>(!)</color>" : "Research");
            TabButton(Tab.Earth, "Earth");
            TabButton(Tab.Pioneers, "Pioneers");
            GUILayout.EndHorizontal();

            _panelScroll = GUILayout.BeginScrollView(_panelScroll);

            if (Time.unscaledTime - _game.LastErrorTime < 3f)
                GUILayout.Label($"<b>{_game.LastError}</b>", _error);

            switch (_tab)
            {
                case Tab.Tile: DrawTileTab(); break;
                case Tab.Research: DrawResearchTab(); break;
                case Tab.Earth: DrawEarthTab(); break;
                default: DrawPioneersTab(); break;
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Enter end turn · LMB select · WASD/RMB pan · Q/E rotate · wheel zoom · R new map", _small);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void TabButton(Tab tab, string label)
        {
            if (GUILayout.Button(_tab == tab ? $"<b>{label}</b>" : label, _richButton)) _deferred = () => _tab = tab;
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
                if (ActionButton($"{def.Name}  ·  {def.MaterialCost} mat  ·  {Modifiers.BuildDays(state, colony, def)} d{bonus}", place.Validate(state)))
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
            if (def.Inputs.Length + def.Outputs.Length > 0 || def.IsSolar || def.Science > 0 || def.ExtractionRate > 0)
                GUILayout.Label($"Running at {module.Efficiency:P0}");

            DrawModuleSpecialist(colony, module);
        }

        void DrawModuleSpecialist(Colony colony, Module module)
        {
            var state = _game.State;
            var best = PioneerCatalog.BestFor(module.Type);
            if (best == null) return;

            GUILayout.Space(6);
            var current = PioneerSystem.AtModule(state, colony, module.Coord);
            if (current != null)
            {
                double bonus = PioneerSystem.ModuleBonus(state, colony, module) - 1.0;
                GUILayout.Label($"Specialist: <b>{current.Name}</b> ({current.Expertise}, skill {current.Skill}) <color=#88dd88>+{bonus:P0}</color>", _rich);
                if (GUILayout.Button("Recall specialist", _richButton))
                    _pending = new AssignPioneer(current.FactionId, current.Id, Assignment.None);
                return;
            }

            GUILayout.Label($"<b>Assign a specialist</b> (best: {best})", _rich);
            foreach (var p in PioneerSystem.Living(state, _game.Player))
            {
                var assign = new AssignPioneer(p.FactionId, p.Id, Assignment.ToModule(colony.Id, module.Coord));
                string match = p.Expertise == best ? " <color=#88dd88>(match)</color>" : "";
                if (ActionButton($"{p.Name} · {p.Expertise} {p.Skill}{match} · now: {AssignmentText(p)}", assign.Validate(state)))
                    _pending = assign;
            }
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

        // ---------------------------------------------------------------- Earth

        void DrawEarthTab()
        {
            var state = _game.State;
            var f = _game.Player;
            var colony = _game.PlayerColony;

            GUILayout.Label($"<b>Earth</b> · {f.Credits:0} credits · funding +{EarthSystem.Funding(state, f):0} per turn", _rich);
            GUILayout.Label("Your sponsor funds the mission and sets objectives. Each one completed pays out and permanently raises your funding.", _small);

            GUILayout.Space(6);
            GUILayout.Label("<b>Objectives</b>", _rich);
            foreach (var o in EarthCatalog.Objectives)
            {
                bool done = f.CompletedObjectives.Contains(o.Id);
                string status = done ? "<color=#88dd88>done</color>" : $"+{o.Reward:0} cr, +{o.FundingIncrease:0}/turn";
                GUILayout.Label($"{(done ? "<color=#88dd88>[x]</color>" : "[ ]")} <b>{o.Name}</b>: {o.Description}  {status}", _small);
            }

            GUILayout.Space(8);
            GUILayout.Label($"<b>Order cargo</b> (arrives in {EarthCatalog.TransitDays(state.Body)} days)", _rich);
            if (colony == null)
            {
                GUILayout.Label("Land a base first.", _small);
                return;
            }

            foreach (var cargo in EarthCatalog.AllCargo.Where(c => c.Type != CargoType.Specialist))
            {
                var order = new OrderCargo(f.Id, colony.Id, cargo.Type);
                if (ActionButton($"{cargo.Name} · {cargo.Cost:0} cr · {cargo.Description}", order.Validate(state))) _pending = order;
            }

            var specialist = EarthCatalog.Get(CargoType.Specialist);
            GUILayout.Label($"{specialist.Name} · {specialist.Cost:0} cr · {specialist.Description}", _small);
            GUILayout.BeginHorizontal();
            foreach (Expertise e in System.Enum.GetValues(typeof(Expertise)))
            {
                var order = new OrderCargo(f.Id, colony.Id, CargoType.Specialist, e);
                GUI.enabled = order.Validate(state) == null;
                if (GUILayout.Button(e.ToString(), _richButton)) _pending = order;
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();

            var inbound = state.Shipments.Where(x => x.FactionId == f.Id).ToList();
            if (inbound.Count > 0)
                GUILayout.Label("In transit: " + string.Join(", ", inbound.Select(x => $"{EarthCatalog.Get(x.Cargo).Name} ({x.ArrivalDay - state.Day} d)")), _small);

            GUILayout.Space(8);
            GUILayout.Label("<b>Exports</b>", _rich);
            foreach (var r in new[] { Resource.RareEarths, Resource.PlatinumMetals })
            {
                double units = System.Math.Floor(colony.Stock[r]);
                var export = new ExportToEarth(f.Id, colony.Id, r);
                if (ActionButton($"Export {units:0} {r} · +{units * EarthCatalog.ExportPrice(r):0} cr", export.Validate(state))) _pending = export;
            }
            GUILayout.Label("Strategic deposits are mined by a Mineral Extractor (research Mineral Beneficiation).", _small);
        }

        // ---------------------------------------------------------------- Pioneers

        void DrawPioneersTab()
        {
            var state = _game.State;
            var f = _game.Player;

            GUILayout.Label("<b>Council</b>", _rich);
            foreach (CouncilSeat seat in System.Enum.GetValues(typeof(CouncilSeat)))
            {
                var holder = PioneerSystem.OnCouncil(state, f, seat);
                GUILayout.Label($"{PioneerCatalog.Describe(seat)}: <b>{(holder != null ? holder.Name : "vacant")}</b>", _small);
            }

            GUILayout.Space(8);
            var living = PioneerSystem.Living(state, f).ToList();
            GUILayout.Label($"<b>Pioneers</b> ({living.Count})", _rich);
            if (living.Count == 0) GUILayout.Label("Your founding Pioneers arrive with the first landing.", _small);

            foreach (var p in living)
            {
                bool selected = p.Id == _selectedPioneerId;
                var color = "#" + ColorUtility.ToHtmlStringRGB(PioneerRenderer.ColorOf(p.Expertise));
                GUILayout.Label($"<color={color}><b>{p.Name}</b></color> · {p.Expertise} {p.Skill}/{PioneerCatalog.MaxSkill} · age {p.Age(state)}", _rich);
                GUILayout.Label(string.Join("\n", p.Traits.Select(PioneerCatalog.Describe)), _small);
                GUILayout.Label($"Now: {AssignmentText(p)} · accident risk {PioneerSystem.MonthlyRisk(state, p):P2}/turn", _small);

                GUILayout.BeginHorizontal();
                int id = p.Id;
                if (GUILayout.Button(selected ? "<b>Focus</b>" : "Focus", _richButton))
                    _deferred = () => { _selectedPioneerId = id; _game.FocusOnPioneer(id); };

                var colony = _game.PlayerColony;
                if (colony != null) AssignButton(p, "Admin", Assignment.AsAdministrator(colony.Id));
                AssignButton(p, "Sci", Assignment.OnCouncil(CouncilSeat.ChiefScientist));
                AssignButton(p, "Eng", Assignment.OnCouncil(CouncilSeat.ChiefEngineer));
                AssignButton(p, "Liaison", Assignment.OnCouncil(CouncilSeat.EarthLiaison));
                AssignButton(p, "Off duty", Assignment.None);
                GUILayout.EndHorizontal();
                GUILayout.Label("To put them on a module, select the module's tile.", _small);
                GUILayout.Space(6);
            }

            var fallen = state.Pioneers.Where(p => !p.IsAlive && p.FactionId == f.Id).ToList();
            if (fallen.Count > 0)
            {
                GUILayout.Space(6);
                GUILayout.Label("<b>In memoriam</b>", _rich);
                foreach (var p in fallen)
                    GUILayout.Label($"{p.Name} ({p.Expertise}), {GameState.StartDate.AddDays(p.DiedDay ?? 0).ToString("MMM yyyy", Invariant)}: {p.CauseOfDeath}", _small);
            }
        }

        void AssignButton(Pioneer p, string label, Assignment assignment)
        {
            var command = new AssignPioneer(p.FactionId, p.Id, assignment);
            GUI.enabled = command.Validate(_game.State) == null;
            if (GUILayout.Button(label, _richButton)) _pending = command;
            GUI.enabled = true;
        }

        string AssignmentText(Pioneer p)
        {
            var state = _game.State;
            switch (p.Assignment.Kind)
            {
                case AssignmentKind.Module:
                    var colony = state.GetColony(p.Assignment.ColonyId);
                    return $"{colony?.ModuleAt(p.Assignment.Module)?.Def.Name ?? "module"} at {colony?.Name}";
                case AssignmentKind.Administrator:
                    return $"Administrator of {state.GetColony(p.Assignment.ColonyId)?.Name}";
                case AssignmentKind.Council:
                    return p.Assignment.Seat switch
                    {
                        CouncilSeat.ChiefScientist => "Chief Scientist",
                        CouncilSeat.ChiefEngineer => "Chief Engineer",
                        _ => "Earth Liaison",
                    };
                default:
                    return "off duty";
            }
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
