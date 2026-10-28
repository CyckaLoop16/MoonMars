using Regolith.Sim;
using UnityEngine;

namespace Regolith.View
{
    /// <summary>
    /// Prototype entry point: generates a map, renders it, wires the camera and draws a debug inspector.
    /// Auto-spawns when you press Play in any scene, so the project needs no hand-built scene yet.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public int MapWidth = 64;
        public int MapHeight = 40;
        [Tooltip("0 = random seed each run")]
        public int Seed;

        Camera _camera;
        StrategyCamera _cameraRig;
        HexMapRenderer _mapRenderer;
        WorldMap _map;
        Tile _hovered;
        GUIStyle _wrapStyle;
        GUIStyle _richStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (FindAnyObjectByType<GameBootstrap>() != null) return;
            new GameObject("Regolith").AddComponent<GameBootstrap>();
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
            _camera.backgroundColor = new Color(0.16f, 0.09f, 0.07f);
            _camera.farClipPlane = 500f;

            _cameraRig = _camera.GetComponent<StrategyCamera>();
            if (_cameraRig == null) _cameraRig = _camera.gameObject.AddComponent<StrategyCamera>();

            _mapRenderer = new GameObject("HexMap").AddComponent<HexMapRenderer>();

            Regenerate(Seed != 0 ? Seed : System.Environment.TickCount);
        }

        void Regenerate(int seed)
        {
            _map = MarsMapGenerator.Generate(MapWidth, MapHeight, seed);
            _mapRenderer.Build(_map);
            _hovered = null;

            var bounds = _mapRenderer.WorldBounds();
            _cameraRig.Init(_camera, new Vector3(bounds.center.x, 0f, bounds.center.y), bounds);
        }

        void Update()
        {
            if (InputShim.RegeneratePressed())
                Regenerate(System.Environment.TickCount);

            Tile hovered = null;
            var ray = _camera.ScreenPointToRay(InputShim.MousePosition());
            if (_mapRenderer.Collider.Raycast(ray, out var hit, 1000f))
            {
                // Nudge into the prism so hits on walls resolve to the taller tile.
                var coord = HexMapRenderer.FromWorld(hit.point - hit.normal * 0.02f);
                hovered = _map.GetTile(coord);
            }

            if (hovered != _hovered)
            {
                _hovered = hovered;
                _mapRenderer.SetHovered(hovered);
            }
        }

        void OnGUI()
        {
            if (_map == null) return;
            if (_wrapStyle == null)
            {
                _wrapStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
                _richStyle = new GUIStyle(_wrapStyle) { richText = true };
            }

            GUILayout.BeginArea(new Rect(12, 12, 340, 300), GUI.skin.box);
            GUILayout.Label($"<b>REGOLITH</b>  ·  prototype M0  ·  seed {_map.Seed}", _richStyle);

            if (_hovered != null)
            {
                var def = _hovered.Def;
                GUILayout.Space(6);
                GUILayout.Label($"<b>{def.Name}</b>  {_hovered.Coord}", _richStyle);
                GUILayout.Label(def.Description, _wrapStyle);
                GUILayout.Label($"Elevation level {HexMapRenderer.HeightLevel(_hovered)}  ·  Move cost {def.MoveCost}");
                GUILayout.Label($"Ice {_hovered.Ice:P0}  ·  Minerals {_hovered.Minerals:P0}");
                if (_hovered.Feature == TileFeature.LavaTube) GUILayout.Label("Lava tube: shielded habitat site");
                if (_hovered.Feature == TileFeature.CraterRim) GUILayout.Label("Crater rim: exposed strata (+1 Science)");
                GUILayout.Label(_hovered.Yields.ToString(), _wrapStyle);
            }
            else
            {
                GUILayout.Label("Hover a tile to inspect it.");
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("WASD pan · Q/E rotate · wheel zoom · RMB drag · R new map", _wrapStyle);
            GUILayout.EndArea();
        }
    }
}
