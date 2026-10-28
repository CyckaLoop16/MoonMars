using System.Collections.Generic;
using System.Linq;
using Regolith.Sim;
using UnityEngine;

namespace Regolith.View
{
    /// <summary>
    /// Prototype entry point: owns the simulation, ends turns (1 turn = 1 month of daily ticks),
    /// handles selection and forwards player actions to the sim as commands.
    /// Auto-spawns when you press Play in any scene, so the project needs no hand-built scene yet.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        [Tooltip("0 = random seed each run")]
        public int Seed;
        public MapSize MapSize = MapSize.Large;

        Camera _camera;
        StrategyCamera _cameraRig;
        HexMapRenderer _mapRenderer;
        ModuleRenderer _moduleRenderer;
        Hud _hud;
        int _renderedRevision = -1;

        public Simulation Sim { get; private set; }
        public Tile Hovered { get; private set; }
        public Tile Selected { get; private set; }
        public string LastError { get; private set; }
        public float LastErrorTime { get; private set; } = -100f;
        /// <summary>How the player colony's stockpiles changed over the last turn.</summary>
        public double[] LastTurnChange { get; private set; }
        /// <summary>First day of the turn that just ended, so the HUD can show what happened in it.</summary>
        public int LastTurnStartDay { get; private set; }

        public GameState State => Sim.State;
        public Faction Player => State.GetFaction(GameSetup.PlayerFactionId);
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
            Selected = null;
            Hovered = null;
            LastTurnChange = null;
            LastTurnStartDay = 0;
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
            if (InputShim.RegeneratePressed()) NewGame(System.Environment.TickCount);
            if (InputShim.EndTurnPressed()) EndTurn();

            if (State.Revision != _renderedRevision) RefreshVisuals();
            HandlePointer();
        }

        public void EndTurn()
        {
            var colony = PlayerColony;
            var before = new double[Stockpile.Count];
            for (int i = 0; i < before.Length && colony != null; i++) before[i] = colony.Stock[(Resource)i];

            LastTurnStartDay = State.Day;
            Sim.AdvanceToNextMonth();

            if (colony == null) return;
            LastTurnChange = new double[Stockpile.Count];
            for (int i = 0; i < before.Length; i++) LastTurnChange[i] = colony.Stock[(Resource)i] - before[i];
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
