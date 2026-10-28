using System.Collections.Generic;
using Regolith.Sim;
using UnityEngine;

namespace Regolith.View
{
    /// <summary>
    /// Draws colony modules. Art drop-in slot: if a prefab exists at
    /// <c>Resources/Modules/&lt;ModuleType&gt;</c> (e.g. <c>Resources/Modules/SolarArray.prefab</c>)
    /// it is used for built modules; otherwise a coloured placeholder column is drawn.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ModuleRenderer : MonoBehaviour
    {
        readonly struct Placeholder
        {
            public readonly Color Color;
            public readonly float Height;
            public readonly float Size;

            public Placeholder(Color color, float height, float size)
            {
                Color = color;
                Height = height;
                Size = size;
            }
        }

        static readonly Dictionary<ModuleType, Placeholder> Placeholders = new Dictionary<ModuleType, Placeholder>
        {
            { ModuleType.HabCore, new Placeholder(new Color(0.93f, 0.93f, 0.9f), 0.6f, 0.55f) },
            { ModuleType.Habitat, new Placeholder(new Color(0.85f, 0.82f, 0.74f), 0.45f, 0.5f) },
            { ModuleType.SolarArray, new Placeholder(new Color(0.16f, 0.27f, 0.6f), 0.06f, 0.85f) },
            { ModuleType.FissionReactor, new Placeholder(new Color(0.58f, 0.62f, 0.64f), 0.55f, 0.35f) },
            { ModuleType.IceMine, new Placeholder(new Color(0.6f, 0.85f, 0.98f), 0.25f, 0.5f) },
            { ModuleType.OxygenPlant, new Placeholder(new Color(0.3f, 0.55f, 0.9f), 0.35f, 0.45f) },
            { ModuleType.Greenhouse, new Placeholder(new Color(0.35f, 0.78f, 0.35f), 0.3f, 0.6f) },
            { ModuleType.Foundry, new Placeholder(new Color(0.88f, 0.5f, 0.2f), 0.4f, 0.45f) },
        };

        static readonly Color UnderConstruction = new Color(0.5f, 0.5f, 0.5f);

        readonly HexMeshBuilder _builder = new HexMeshBuilder();
        readonly List<GameObject> _artInstances = new List<GameObject>();
        readonly Dictionary<ModuleType, GameObject> _prefabs = new Dictionary<ModuleType, GameObject>();
        Mesh _mesh;

        void Awake()
        {
            _mesh = new Mesh { name = "Modules" };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            GetComponent<MeshRenderer>().sharedMaterial = HexMeshBuilder.CreateMaterial();
        }

        public void Build(GameState state)
        {
            _builder.Clear();
            foreach (var go in _artInstances) Destroy(go);
            _artInstances.Clear();

            foreach (var colony in state.Colonies)
            {
                foreach (var module in colony.Modules)
                {
                    var tile = state.Map.GetTile(module.Coord);
                    var center = HexMapRenderer.ToWorld(module.Coord);
                    float ground = HexMapRenderer.TopHeight(tile);

                    var prefab = LoadPrefab(module.Type);
                    if (prefab != null && module.IsBuilt)
                    {
                        _artInstances.Add(Instantiate(prefab, new Vector3(center.x, ground, center.z), Quaternion.identity, transform));
                        continue;
                    }

                    var p = Placeholders[module.Type];
                    float height = module.IsBuilt ? p.Height : p.Height * 0.35f;
                    var color = module.IsBuilt ? p.Color : Color.Lerp(p.Color, UnderConstruction, 0.65f);
                    if (!colony.IsAlive) color = HexMeshBuilder.Shade(color, 0.4f);
                    _builder.AddPrism(center, ground, ground + height, p.Size, color);
                }
            }

            _builder.ApplyTo(_mesh);
        }

        GameObject LoadPrefab(ModuleType type)
        {
            if (!_prefabs.TryGetValue(type, out var prefab))
            {
                prefab = Resources.Load<GameObject>($"Modules/{type}");
                _prefabs[type] = prefab;
            }
            return prefab;
        }
    }
}
