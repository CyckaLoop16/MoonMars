using System.Collections.Generic;
using System.Linq;
using Regolith.Sim;
using UnityEngine;

namespace Regolith.View
{
    /// <summary>
    /// Prototype entry point: owns the simulation, runs the clock, handles selection and
    /// forwards player actions to the sim as commands.
    /// Auto-spawns when you press Play in any scene, so the project needs no hand-built scene yet.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        /// <summary>Game days per real second for each speed. Index 0 = paused.</summary>
        public static readonly float[] DaysPerSecond = { 0f, 1f, 2f, 5f, 10f, 25f };
        const int MaxTicksPerFrame = 30;

        [Tooltip("0 = random seed each run")]
        public int Seed;

        Camera _camera;
        StrategyCamera _cameraRig;
        HexMapRenderer _mapRenderer;
        ModuleRenderer _moduleRenderer;
        Hud _hud;
        int _renderedRevision = -1;
        float _accumulator;
        int _resumeSpeed = 1;

        public Simulation Sim { get; private set; }
        public int Speed { get; private set; }
        public Tile Hovered { get; private set; }
        public Tile Selected { get; private set; }
        public string LastError { get; private set; }
        public float LastErrorTime { get; private set; } = -100f;

        public GameState State => Sim.State;
        public Colony PlayerColony => State.Colonies.FirstOrDefault(c => c.FactionId == GameSetup.PlayerFactionId);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (FindAnyObjectByType<GameController>() != null) return;
            new GameObject("Regolith").AddComponent<GameController>();
        }

        void Start()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                _camera = go.AddComponent<Camera>();
            }
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.02f, 0.02f, 0.04f); // space
            _camera.farClipPlane = 500f;

            _cameraRig = _camera.GetComponent<StrategyCamera>();
            if (_cameraRig == null) _cameraRig = _camera.gameObject.AddComponent<StrategyCamera>();

            _mapRenderer = new GameObject("HexMap").AddComponent<HexMapRenderer>();
            _moduleRenderer = new GameObject("Modules").AddComponent<ModuleRenderer>();
            _hud = gameObject.AddComponent<Hud>();
            _hud.Init(this);

            NewGame(Seed != 0 ? Seed : System.Environment.TickCount);
        }

        void NewGame(int seed)
        {
            Sim = GameSetup.NewLunarGame(seed);
            Speed = 0;
            Selected = null;
            Hovered = null;
            _renderedRevision = -1;
            RefreshVisuals();

            var bounds = _mapRenderer.WorldBounds();
            _cameraRig.Init(_camera, new Vector3(bounds.center.x, 0f, bounds.center.y), bounds);
        }

        void Update()
        {
            HandleKeys();
            RunClock();
            if (State.Revision != _renderedRevision) RefreshVisuals();
            HandlePointer();
        }

        void HandleKeys()
        {
            if (InputShim.RegeneratePressed()) NewGame(System.Environment.TickCount);
            if (InputShim.PausePressed()) TogglePause();

            int speed = InputShim.SpeedPressed();
            if (speed > 0) SetSpeed(speed);
        }

        public void TogglePause() => SetSpeed(Speed == 0 ? _resumeSpeed : 0);

        public void SetSpeed(int speed)
        {
            Speed = Mathf.Clamp(speed, 0, DaysPerSecond.Length - 1);
            if (Speed > 0) _resumeSpeed = Speed;
        }

        void RunClock()
        {
            if (Speed == 0) return;

            _accumulator += Time.unscaledDeltaTime * DaysPerSecond[Speed];
            int ticks = 0;
            while (_accumulator >= 1f && ticks < MaxTicksPerFrame)
            {
                Sim.Tick();
                _accumulator -= 1f;
                ticks++;
            }
            if (ticks == MaxTicksPerFrame) _accumulator = 0f; // don't spiral if the frame rate tanks
        }

        void RefreshVisuals()
        {
            var territory = new Dictionary<HexCoord, Color>();
            foreach (var colony in State.Colonies)
            {
                var color = HexMapRenderer.ParseColor(State.GetFaction(colony.FactionId).Color);
                foreach (var c in colony.Territory) territory[c] = color;
            }

            _mapRenderer.Build(State.Map, territory);
            _moduleRenderer.Build(State);
            _mapRenderer.SetHovered(Hovered);
            _mapRenderer.SetSelected(Selected);
            _renderedRevision = State.Revision;
        }

        void HandlePointer()
        {
            var mouse = InputShim.MousePosition();
            if (_hud.IsPointerOver(mouse))
            {
                SetHovered(null);
                return;
            }

            Tile tile = null;
            var ray = _camera.ScreenPointToRay(mouse);
            if (_mapRenderer.Collider.Raycast(ray, out var hit, 1000f))
            {
                // Nudge into the prism so hits on walls resolve to the taller tile.
                tile = State.Map.GetTile(HexMapRenderer.FromWorld(hit.point - hit.normal * 0.02f));
            }

            SetHovered(tile);
            if (InputShim.SelectPressed())
            {
                Selected = tile;
                _mapRenderer.SetSelected(tile);
            }
        }

        void SetHovered(Tile tile)
        {
            if (tile == Hovered) return;
            Hovered = tile;
            _mapRenderer.SetHovered(tile);
        }

        /// <summary>Runs a player command; on failure the reason is shown in the HUD.</summary>
        public bool TryExecute(ICommand command)
        {
            if (Sim.Execute(command, out var error)) return true;

            LastError = error;
            LastErrorTime = Time.unscaledTime;
            return false;
        }
    }
}
