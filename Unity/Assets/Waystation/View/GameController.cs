using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Waystation.Sim;

namespace Waystation.View
{
    public enum Tool
    {
        Select,
        Build,
        Demolish,
        /// <summary>Pick a pad or hangar for the hailing ship in <see cref="GameController.ClearingShip"/>.</summary>
        ClearToLand,
    }

    /// <summary>
    /// Entry point: owns the simulation, runs the real-time clock (pause, 1×, 2×, 4×), and turns
    /// mouse and keyboard input into commands: building, demolishing, and clearing hailing ships to
    /// land. Auto-spawns when you press Play in any scene, so the project needs no hand-built scene yet.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        public static readonly float[] SpeedMultipliers = { 0f, 1f, 2f, 4f };
        const int MaxStepsPerFrame = 400;
        /// <summary>How close (in pixels) a click must be to a ship to pick it.</summary>
        const float ShipPickRadius = 34f;

        [Tooltip("0 = random seed each run")]
        public int Seed;

        Camera _camera;
        IsoCamera _cameraRig;
        WorldRenderer _world;
        BuildingRenderer _buildings;
        ShipRenderer _ships;
        PeopleRenderer _people;
        SkyCycle _sky;
        UiRoot _ui;
        LineRenderer _ghostOutline;
        MeshFilter _ghostFill;
        Material _ghostMaterial;
        float _accumulator;
        int _resumeSpeed = 1;
        int _renderedRevision = -1;

        public Simulation Sim { get; private set; }
        public Station Station => Sim.Station;
        public int Speed { get; private set; } = 1;
        public Tool Tool { get; private set; } = Tool.Select;
        public BuildingType BuildType { get; private set; } = BuildingType.LandingPad;
        public Cell? HoverCell { get; private set; }
        public Building Selected { get; private set; }
        /// <summary>The ship shown in the inspector (picked on the map or in the comms panel).</summary>
        public Ship SelectedShip { get; private set; }
        /// <summary>Pause menu open: the sim is paused and the map ignores input.</summary>
        public bool MenuOpen { get; private set; }
        int _speedBeforeMenu = 1;
        /// <summary>The hailing ship being cleared to land (Tool.ClearToLand).</summary>
        public Ship ClearingShip { get; private set; }
        /// <summary>Why the current placement or clearance isn't allowed, for the HUD; null when it is.</summary>
        public string PlacementError { get; private set; }
        /// <summary>What a drag-paint would place and cost, for the HUD.</summary>
        public string PlacementInfo { get; private set; }
        Cell? _dragStart;
        public string LastError { get; private set; }
        public float LastErrorTime { get; private set; } = -100f;
        public Camera Camera => _camera;
        /// <summary>Build grid overlay, toggled with G or the HUD button.</summary>
        public bool GridVisible { get; private set; }
        public BuildingRenderer Buildings => _buildings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (FindAnyObjectByType<GameController>() != null) return;
            new GameObject("Waystation").AddComponent<GameController>();
        }

        void Start()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _camera = go.AddComponent<Camera>();
            }
            // Not `??`: Unity's "fake null" for missing components defeats the null-coalescing operator.
            _cameraRig = _camera.GetComponent<IsoCamera>();
            if (_cameraRig == null) _cameraRig = _camera.gameObject.AddComponent<IsoCamera>();

            _world = new GameObject("Space").AddComponent<WorldRenderer>();
            SetUpLighting();

            _buildings = new GameObject("Buildings").AddComponent<BuildingRenderer>();
            _ships = new GameObject("Ships").AddComponent<ShipRenderer>();
            _people = new GameObject("People").AddComponent<PeopleRenderer>();
            CreateGhost();
            _ui = gameObject.AddComponent<UiRoot>();
            _ui.Init(this);

            NewGame(Seed != 0 ? Seed : System.Environment.TickCount);
        }

        /// <summary>
        /// The lamp rig that feeds the placeholder shader, the sun and the day/night cycle (SkyCycle),
        /// and URP post-processing (bloom, vignette, grade).
        /// </summary>
        void SetUpLighting()
        {
            gameObject.AddComponent<LightRig>();
            _sky = gameObject.AddComponent<SkyCycle>();
            _sky.Init(_camera, _world);
            GrimPostFx.Apply(_camera);
        }

        public void NewGame(int seed)
        {
            Sim = GameSetup.NewStation(seed);
            Selected = null;
            SelectedShip = null;
            MenuOpen = false;
            ClearingShip = null;
            Tool = Tool.Select;
            Speed = 1;
            _accumulator = 0f;
            _renderedRevision = -1;

            _world.Build(Station.Width, Station.Height, seed, _camera);
            _buildings.Bind(Station);
            _ships.Bind(Station);
            _people.Bind(Station);
            _sky.Bind(Station);
            float half = Station.Width / 2f + 6f;
            _cameraRig.Init(_camera, Vector3.zero, Rect.MinMaxRect(-half, -half, half, half));
            if (_ui != null) _ui.OnNewGame();
        }

        void Update()
        {
            HandleKeys();
            RunClock();
            if (Station.Revision != _renderedRevision)
            {
                _buildings.Rebuild();
                _renderedRevision = Station.Revision;
                if (Selected != null && !Station.Buildings.Contains(Selected)) Selected = null;
            }
            if (SelectedShip != null && !Station.Ships.Contains(SelectedShip)) SelectedShip = null;

            // A ship that stopped hailing (gave up, or was cleared elsewhere) ends clearance mode.
            if (Tool == Tool.ClearToLand && (ClearingShip == null || ClearingShip.State != ShipState.Hailing))
                SetTool(Tool.Select);

            HandlePointer();
            _world.ShowGrid(GridVisible);
        }

        // ------------------------------------------------------------------ clock & keys

        void HandleKeys()
        {
            if (InputShim.CancelPressed()) Cancel();
            if (MenuOpen) return;

            if (InputShim.PausePressed()) TogglePause();
            double cheat = InputShim.CheatPressed();
            if (cheat > 0) Cheat(cheat);
            int speed = InputShim.SpeedPressed();
            if (speed > 0) SetSpeed(speed);

            switch (InputShim.ToolPressed())
            {
                case 'P': StartBuilding(BuildingType.LandingPad); break;
                case 'H': StartBuilding(BuildingType.Hangar); break;
                case 'C': StartBuilding(BuildingType.Walkway); break;
                case 'F': StartBuilding(BuildingType.Foundation); break;
                case 'X': SetTool(Tool.Demolish); break;
                case 'T': ClearNextHail(); break;
                case 'L': _ui.ToggleWindow(UiWindow.Log); break;
                case 'M': _ui.ToggleWindow(UiWindow.Ledger); break;
                case 'G': ToggleGrid(); break;
            }
        }

        /// <summary>Esc: close a window, else cancel the tool, else clear the selection, else open/close the menu.</summary>
        public void Cancel()
        {
            if (_ui.CloseWindow()) return;
            if (MenuOpen) { SetMenuOpen(false); return; }
            if (Tool != Tool.Select) { SetTool(Tool.Select); return; }
            if (Selected != null || SelectedShip != null) { ClearSelection(); return; }
            SetMenuOpen(true);
        }

        public void SetMenuOpen(bool open)
        {
            if (open == MenuOpen) return;
            MenuOpen = open;
            if (open)
            {
                _speedBeforeMenu = Speed;
                Speed = 0;
                SetTool(Tool.Select);
            }
            else
            {
                Speed = _speedBeforeMenu;
            }
        }

        public void ToggleGrid() => GridVisible = !GridVisible;

        /// <summary>F9 / pause menu: free credits for testing.</summary>
        public void Cheat(double amount) => TryExecute(new CheatMoney(amount));

        public void ClearSelection()
        {
            Selected = null;
            SelectedShip = null;
        }

        public void SelectBuilding(Building b)
        {
            Selected = b;
            SelectedShip = null;
        }

        public void SelectShip(Ship ship)
        {
            SelectedShip = ship;
            Selected = null;
        }

        public void FocusOn(Vector3 world) => _cameraRig.FocusOn(world);

        public void FocusOnShip(Ship ship)
        {
            if (ship != null && _ships.TryGetPosition(ship.Id, out var p)) FocusOn(p);
        }

        public void FocusOnBuilding(Building b)
        {
            if (b != null) FocusOn(BuildingRenderer.Center(b, Station.Width, Station.Height));
        }

        public bool TryGetShipWorldPosition(int shipId, out Vector3 world) => _ships.TryGetPosition(shipId, out world);

        public void TogglePause() => SetSpeed(Speed == 0 ? _resumeSpeed : 0);

        public void SetSpeed(int speed)
        {
            Speed = Mathf.Clamp(speed, 0, SpeedMultipliers.Length - 1);
            if (Speed > 0) _resumeSpeed = Speed;
        }

        void RunClock()
        {
            if (Speed == 0) return;
            _accumulator += Time.unscaledDeltaTime * SpeedMultipliers[Speed] * Station.TicksPerSecond;
            int steps = 0;
            while (_accumulator >= 1f && steps < MaxStepsPerFrame)
            {
                Sim.Step();
                _accumulator -= 1f;
                steps++;
            }
            if (steps == MaxStepsPerFrame) _accumulator = 0f; // don't spiral when the frame rate tanks
        }

        // ------------------------------------------------------------------ tools

        public void StartBuilding(BuildingType type)
        {
            BuildType = type;
            SetTool(Tool.Build);
        }

        public void SetTool(Tool tool)
        {
            Tool = tool;
            if (tool != Tool.ClearToLand) ClearingShip = null;
            _dragStart = null;
        }

        /// <summary>Start clearing a hailing ship: the next click on a pad or hangar assigns it.</summary>
        public void BeginClearance(Ship ship)
        {
            if (ship == null || ship.State != ShipState.Hailing) return;
            SetTool(Tool.ClearToLand);
            ClearingShip = ship;
        }

        /// <summary>T: pick up the longest-waiting hail.</summary>
        public void ClearNextHail() => BeginClearance(Station.Hailing.FirstOrDefault());

        /// <summary>Runs a command; on failure the reason is shown in the HUD.</summary>
        public bool TryExecute(ICommand command)
        {
            if (Sim.Execute(command, out var error)) return true;
            LastError = error;
            LastErrorTime = Time.unscaledTime;
            return false;
        }

        void HandlePointer()
        {
            PlacementError = null;
            if (Tool != Tool.Build) PlacementInfo = null;
            var mouse = InputShim.MousePosition();
            if (MenuOpen)
            {
                HoverCell = null;
                HideGhost();
                return;
            }
            bool overHud = _ui.IsPointerOver(mouse);
            HoverCell = overHud ? null : CellUnder(mouse);

            switch (Tool)
            {
                case Tool.Build when HoverCell.HasValue:
                    HandleBuild();
                    return;
                case Tool.ClearToLand:
                    HandleClearance(overHud);
                    return;
            }

            HideGhost();
            if (overHud || !InputShim.SelectPressed()) return;

            if (Tool == Tool.Select)
            {
                // Clicking near a ship selects it; a hailing ship also starts clearance.
                var ship = ShipNear(mouse);
                if (ship != null)
                {
                    SelectShip(ship);
                    if (ship.State == ShipState.Hailing) BeginClearance(ship);
                    return;
                }
            }

            if (!HoverCell.HasValue) return;
            var building = Station.BuildingAt(HoverCell.Value);
            if (Tool == Tool.Demolish)
            {
                if (building != null) TryExecute(new Demolish(building.Id));
            }
            else
            {
                SelectBuilding(building);
            }
        }

        void HandleBuild()
        {
            var def = BuildingCatalog.Get(BuildType);
            if (def.IsPaintable)
            {
                HandlePaint(def);
                return;
            }

            PlacementInfo = null;
            var place = new PlaceBuilding(BuildType, OriginFor(HoverCell.Value, def));
            PlacementError = place.Validate(Station);
            ShowGhost(place.Origin, def.Width, def.Height, PlacementError == null);
            if (InputShim.SelectPressed() && PlacementError == null) TryExecute(place);
        }

        /// <summary>
        /// Walkways and foundations: press, drag and release. Foundations fill the rectangle; walkways
        /// run in an L (along X, then along Y). Cells that are already taken are skipped.
        /// </summary>
        void HandlePaint(BuildingDef def)
        {
            if (InputShim.SelectPressed()) _dragStart = HoverCell;
            var from = _dragStart ?? HoverCell.Value;
            var to = HoverCell.Value;
            var cells = def.IsFloor ? Rectangle(from, to) : LPath(from, to);

            var valid = new List<Cell>();
            string firstError = null;
            foreach (var c in cells)
            {
                var error = new PlaceBuilding(BuildType, c).Validate(Station);
                if (error == null || error.Contains("credits")) valid.Add(c);
                else if (firstError == null) firstError = error;
            }

            double cost = valid.Count * def.Cost;
            bool affordable = cost <= Station.Money;
            PlacementError = valid.Count == 0 ? firstError : !affordable ? $"Needs {cost:N0} credits" : null;
            PlacementInfo = valid.Count > 0 ? $"{valid.Count} × {def.Name} · {cost:N0} cr" : null;
            ShowGhostCells(valid.Count > 0 ? valid : cells, PlacementError == null);

            if (_dragStart.HasValue && !InputShim.SelectHeld())
            {
                if (PlacementError == null)
                    foreach (var c in valid)
                        if (!TryExecute(new PlaceBuilding(BuildType, c))) break;
                _dragStart = null;
            }
        }

        static List<Cell> Rectangle(Cell a, Cell b)
        {
            var cells = new List<Cell>();
            for (int x = Mathf.Min(a.X, b.X); x <= Mathf.Max(a.X, b.X); x++)
            for (int y = Mathf.Min(a.Y, b.Y); y <= Mathf.Max(a.Y, b.Y); y++)
                cells.Add(new Cell(x, y));
            return cells;
        }

        static List<Cell> LPath(Cell a, Cell b)
        {
            var cells = new List<Cell>();
            int dx = b.X > a.X ? 1 : -1, dy = b.Y > a.Y ? 1 : -1;
            for (int x = a.X; ; x += dx)
            {
                cells.Add(new Cell(x, a.Y));
                if (x == b.X) break;
            }
            for (int y = a.Y + dy; a.Y != b.Y; y += dy)
            {
                cells.Add(new Cell(b.X, y));
                if (y == b.Y) break;
            }
            return cells;
        }

        void HandleClearance(bool overHud)
        {
            var target = HoverCell.HasValue ? Station.BuildingAt(HoverCell.Value) : null;
            if (target == null || target.Def.Serves.Length == 0)
            {
                HideGhost();
                return;
            }

            PlacementError = Station.LandingError(ClearingShip, target);
            ShowGhost(target.Origin, target.Def.Width, target.Def.Height, PlacementError == null);

            if (!overHud && InputShim.SelectPressed() && TryExecute(new AssignLanding(ClearingShip.Id, target.Id)))
                SetTool(Tool.Select);
        }

        Ship ShipNear(Vector2 mouse)
        {
            Ship best = null;
            float bestDistance = ShipPickRadius;
            foreach (var ship in Station.Ships)
            {
                if (!TryGetShipScreenPosition(ship.Id, out var sp) || sp.z <= 0f) continue;
                float d = Vector2.Distance(mouse, new Vector2(sp.x, sp.y));
                if (d < bestDistance)
                {
                    best = ship;
                    bestDistance = d;
                }
            }
            return best;
        }

        /// <summary>Footprints are centred on the cursor.</summary>
        static Cell OriginFor(Cell hover, BuildingDef def) =>
            new Cell(hover.X - (def.Width - 1) / 2, hover.Y - (def.Height - 1) / 2);

        Cell? CellUnder(Vector2 screen)
        {
            var ray = _camera.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-5f) return null;
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) return null;
            var p = ray.origin + ray.direction * t;
            var cell = new Cell(Mathf.FloorToInt(p.x + Station.Width / 2f), Mathf.FloorToInt(p.z + Station.Height / 2f));
            return Station.InBounds(cell) ? cell : (Cell?)null;
        }

        public bool TryGetShipScreenPosition(int shipId, out Vector3 screen)
        {
            screen = default;
            if (!_ships.TryGetPosition(shipId, out var world)) return false;
            screen = _camera.WorldToScreenPoint(world + Vector3.up * 0.4f);
            return true;
        }

        public Vector3 WorldToScreen(Vector3 world) => _camera.WorldToScreenPoint(world);

        // ------------------------------------------------------------------ placement ghost

        void CreateGhost()
        {
            _ghostMaterial = MeshBuilder.CreateMaterial();

            var fill = new GameObject("GhostFill");
            _ghostFill = fill.AddComponent<MeshFilter>();
            _ghostFill.sharedMesh = new Mesh { name = "Ghost" };
            fill.AddComponent<MeshRenderer>().sharedMaterial = _ghostMaterial;

            _ghostOutline = new GameObject("GhostOutline").AddComponent<LineRenderer>();
            _ghostOutline.sharedMaterial = _ghostMaterial;
            _ghostOutline.loop = true;
            _ghostOutline.positionCount = 4;
            _ghostOutline.widthMultiplier = 0.08f;
            _ghostOutline.shadowCastingMode = ShadowCastingMode.Off;
            HideGhost();
        }

        void ShowGhost(Cell origin, int width, int height, bool valid)
        {
            var color = valid ? MeshBuilder.Hex("#5dff9a") : MeshBuilder.Hex("#ff4d6a");
            float x0 = origin.X - Station.Width / 2f, z0 = origin.Y - Station.Height / 2f;
            float x1 = x0 + width, z1 = z0 + height;
            const float y = 0.05f;

            var b = new MeshBuilder();
            b.AddQuad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), Vector3.up, color * 0.45f, false);
            b.ApplyTo(_ghostFill.sharedMesh);
            _ghostFill.gameObject.SetActive(true);

            _ghostOutline.SetPosition(0, new Vector3(x0, y + 0.02f, z0));
            _ghostOutline.SetPosition(1, new Vector3(x0, y + 0.02f, z1));
            _ghostOutline.SetPosition(2, new Vector3(x1, y + 0.02f, z1));
            _ghostOutline.SetPosition(3, new Vector3(x1, y + 0.02f, z0));
            _ghostOutline.startColor = _ghostOutline.endColor = MeshBuilder.ToRenderSpace(color);
            _ghostOutline.enabled = true;
        }

        /// <summary>A ghost square on each cell (painting walkways and foundations).</summary>
        void ShowGhostCells(List<Cell> cells, bool valid)
        {
            var color = valid ? MeshBuilder.Hex("#5dff9a") : MeshBuilder.Hex("#ff4d6a");
            const float y = 0.05f, inset = 0.06f;
            var b = new MeshBuilder();
            foreach (var c in cells)
            {
                float x0 = c.X - Station.Width / 2f + inset, z0 = c.Y - Station.Height / 2f + inset;
                float x1 = x0 + 1f - 2f * inset, z1 = z0 + 1f - 2f * inset;
                b.AddQuad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), Vector3.up, color * 0.5f, false);
            }
            b.ApplyTo(_ghostFill.sharedMesh);
            _ghostFill.gameObject.SetActive(true);
            _ghostOutline.enabled = false;
        }

        void HideGhost()
        {
            if (_ghostFill != null) _ghostFill.gameObject.SetActive(false);
            if (_ghostOutline != null) _ghostOutline.enabled = false;
        }
    }
}
