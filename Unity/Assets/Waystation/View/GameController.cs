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
    }

    /// <summary>
    /// Entry point: owns the simulation, runs the real-time clock (pause, 1×, 2×, 4×), and turns
    /// mouse and keyboard input into build/demolish commands. Auto-spawns when you press Play in
    /// any scene, so the project needs no hand-built scene yet.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        public static readonly float[] SpeedMultipliers = { 0f, 1f, 2f, 4f };
        const int MaxStepsPerFrame = 400;

        [Tooltip("0 = random seed each run")]
        public int Seed;

        Camera _camera;
        IsoCamera _cameraRig;
        WorldRenderer _world;
        BuildingRenderer _buildings;
        ShipRenderer _ships;
        Hud _hud;
        LineRenderer _ghostOutline;
        MeshFilter _ghostFill;
        Material _ghostMaterial;
        float _accumulator;
        int _resumeSpeed = 1;
        int _renderedRevision = -1;
        Cell? _lastPaintedCell;

        public Simulation Sim { get; private set; }
        public Station Station => Sim.Station;
        public int Speed { get; private set; } = 1;
        public Tool Tool { get; private set; } = Tool.Select;
        public BuildingType BuildType { get; private set; } = BuildingType.LandingPad;
        public Cell? HoverCell { get; private set; }
        public Building Selected { get; private set; }
        /// <summary>Why the current placement isn't allowed, for the HUD; null when it is.</summary>
        public string PlacementError { get; private set; }
        public string LastError { get; private set; }
        public float LastErrorTime { get; private set; } = -100f;
        public Camera Camera => _camera;

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
            _buildings = new GameObject("Buildings").AddComponent<BuildingRenderer>();
            _ships = new GameObject("Ships").AddComponent<ShipRenderer>();
            CreateGhost();
            _hud = gameObject.AddComponent<Hud>();
            _hud.Init(this);

            NewGame(Seed != 0 ? Seed : System.Environment.TickCount);
        }

        public void NewGame(int seed)
        {
            Sim = GameSetup.NewStation(seed);
            Selected = null;
            Tool = Tool.Select;
            Speed = 1;
            _accumulator = 0f;
            _renderedRevision = -1;

            _world.Build(Station.Width, Station.Height, seed, _camera);
            _buildings.Bind(Station.Width, Station.Height);
            _ships.Bind(Station);
            float half = Station.Width / 2f + 6f;
            _cameraRig.Init(_camera, Vector3.zero, Rect.MinMaxRect(-half, -half, half, half));
        }

        void Update()
        {
            HandleKeys();
            RunClock();
            if (Station.Revision != _renderedRevision)
            {
                _buildings.Rebuild(Station);
                _renderedRevision = Station.Revision;
                if (Selected != null && !Station.Buildings.Contains(Selected)) Selected = null;
            }
            HandlePointer();
        }

        // ------------------------------------------------------------------ clock & keys

        void HandleKeys()
        {
            if (InputShim.PausePressed()) TogglePause();
            int speed = InputShim.SpeedPressed();
            if (speed > 0) SetSpeed(speed);
            if (InputShim.CancelPressed()) { SetTool(Tool.Select); Selected = null; }

            switch (InputShim.ToolPressed())
            {
                case 'P': StartBuilding(BuildingType.LandingPad); break;
                case 'H': StartBuilding(BuildingType.Hangar); break;
                case 'C': StartBuilding(BuildingType.Corridor); break;
                case 'X': SetTool(Tool.Demolish); break;
            }
        }

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
            _lastPaintedCell = null;
        }

        public void SelectBuilding(Building b) => Selected = b;

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
            var mouse = InputShim.MousePosition();
            HoverCell = _hud.IsPointerOver(mouse) ? null : CellUnder(mouse);

            if (Tool == Tool.Build && HoverCell.HasValue)
            {
                var place = new PlaceBuilding(BuildType, OriginFor(HoverCell.Value));
                PlacementError = place.Validate(Station);
                ShowGhost(place.Origin, BuildingCatalog.Get(BuildType), PlacementError == null);

                bool corridor = BuildingCatalog.Get(BuildType).IsCorridor;
                bool click = InputShim.SelectPressed();
                // Corridors paint while the button is held, one cell at a time.
                bool paint = corridor && InputShim.SelectHeld() && _lastPaintedCell != HoverCell;
                if ((click || paint) && PlacementError == null)
                {
                    TryExecute(place);
                    _lastPaintedCell = HoverCell;
                }
                if (!InputShim.SelectHeld()) _lastPaintedCell = null;
                return;
            }

            HideGhost();
            if (!InputShim.SelectPressed() || !HoverCell.HasValue) return;

            var building = Station.BuildingAt(HoverCell.Value);
            if (Tool == Tool.Demolish)
            {
                if (building != null) TryExecute(new Demolish(building.Id));
            }
            else
            {
                Selected = building;
            }
        }

        /// <summary>Footprints are centred on the cursor.</summary>
        Cell OriginFor(Cell hover)
        {
            var def = BuildingCatalog.Get(BuildType);
            return new Cell(hover.X - (def.Width - 1) / 2, hover.Y - (def.Height - 1) / 2);
        }

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

        void ShowGhost(Cell origin, BuildingDef def, bool valid)
        {
            var color = valid ? MeshBuilder.Hex("#5dff9a") : MeshBuilder.Hex("#ff4d6a");
            float x0 = origin.X - Station.Width / 2f, z0 = origin.Y - Station.Height / 2f;
            float x1 = x0 + def.Width, z1 = z0 + def.Height;
            const float y = 0.04f;

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

        void HideGhost()
        {
            if (_ghostFill != null) _ghostFill.gameObject.SetActive(false);
            if (_ghostOutline != null) _ghostOutline.enabled = false;
        }

        public bool TryGetShipScreenPosition(int shipId, out Vector3 screen)
        {
            screen = default;
            if (!_ships.TryGetPosition(shipId, out var world)) return false;
            screen = _camera.WorldToScreenPoint(world + Vector3.up * 0.8f);
            return true;
        }
    }
}
