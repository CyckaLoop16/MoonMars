using System.Collections.Generic;
using System.Linq;
using Regolith.Sim;
using UnityEngine;

namespace Regolith.View
{
    /// <summary>
    /// Prototype entry point: owns the simulation, runs the clock (real-time or turn-based),
    /// handles selection and forwards player actions to the sim as commands.
    /// Auto-spawns when you press Play in any scene, so the project needs no hand-built scene yet.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        /// <summary>Game days per real second for each speed. Index 0 = paused.</summary>
        public static readonly float[] DaysPerSecond = { 0f, 1f, 2f, 5f, 10f, 25f };
        const int MaxTicksPerFrame = 30;

        [Tooltip("0 = random seed each run")]
        public int Seed;
        public MapSize MapSize = MapSize.Large;
        [Tooltip("Start in turn-based mode (1 turn = 1 month) instead of real-time with pause")]
        public bool StartTurnBased;

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
        /// <summary>Turn-based: each End Turn runs a month of the daily sim. Otherwise real-time with pause.</summary>
        public bool TurnBased { get; private set; }
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
            _camera.farClipPlane = 2000f;

            _cameraRig = _camera.GetComponent<StrategyCamera>();
            if (_cameraRig == null) _cameraRig = _camera.gameObject.AddComponent<StrategyCamera>();

            _mapRenderer = new GameObject("HexMap").AddComponent<HexMapRenderer>();
            _moduleRenderer = new GameObject("Modules").AddComponent<ModuleRenderer>();
            _hud = gameObject.AddComponent<Hud>();
            _hud.Init(this);

            TurnBased = StartTurnBased;
            NewGame(Seed != 0 ? Seed : System.Environment.TickCount);
        }

        public void NewGame(MapSize size)
        {
            MapSize = size;
            NewGame(System.Environment.TickCount);
        }

        void NewGame(int seed)
        {
            Sim = GameSetup.NewLunarGame(seed, MapSize);
            Speed = 0;
            LastTurnChange = null;
            Selected = null;
            Hovered = null;
            _mapRenderer.Build(State.Map);
            _renderedRevision = -1;
            RefreshVisuals();

            // Start zoomed in over the pole, where the action is.
            var bounds = _mapRenderer.WorldBounds();
            _cameraRig.MaxDistance = Mathf.Max(90f, bounds.width * 0.75f);
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
            if (InputShim.ModeTogglePressed()) ToggleMode();

            if (TurnBased)
            {
                if (InputShim.EndTurnPressed()) EndTurn();
                return;
            }

            if (InputShim.PausePressed()) TogglePause();
            int speed = InputShim.SpeedPressed();
            if (speed > 0) SetSpeed(speed);
        }

        public void ToggleMode()
        {
            TurnBased = !TurnBased;
            Speed = 0;
            _accumulator = 0f;
        }

        /// <summary>How the player colony's stockpiles changed over the last turn (turn mode only).</summary>
        public double[] LastTurnChange { get; private set; }

        public void EndTurn()
        {
            if (!TurnBased) return;

            var colony = PlayerColony;
            var before = new double[Stockpile.Count];
            for (int i = 0; i < before.Length && colony != null; i++) before[i] = colony.Stock[(Resource)i];

            Sim.AdvanceToNextMonth();

            if (colony == null) return;
            LastTurnChange = new double[Stockpile.Count];
            for (int i = 0; i < before.Length; i++) LastTurnChange[i] = colony.Stock[(Resource)i] - before[i];
        }

        public void TogglePause() => SetSpeed(Speed == 0 ? _resumeSpeed : 0);

        public void SetSpeed(int speed)
        {
            Speed = Mathf.Clamp(speed, 0, DaysPerSecond.Length - 1);
            if (Speed > 0) _resumeSpeed = Speed;
        }

        void RunClock()
        {
            if (TurnBased || Speed == 0) return;

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

            _mapRenderer.SetTerritory(territory);
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
            if (_mapRenderer.Raycast(_camera.ScreenPointToRay(mouse), out var point))
                tile = State.Map.GetTile(HexMapRenderer.FromWorld(point));

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
