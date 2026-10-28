using System.Collections.Generic;
using UnityEngine;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>
    /// Station modules floating in space. Corridors auto-connect: each corridor piece grows an arm
    /// toward every neighbouring corridor or building, so lines and junctions form on their own.
    ///
    /// Art drop-in slots (pivot at the footprint centre, deck level at Y=0, 1 unit = 1 grid cell):
    ///   Resources/Buildings/StarterPad.prefab, LandingPad.prefab, Hangar.prefab
    ///   Resources/Buildings/Corridor_Hub.prefab   (centre piece of a corridor cell)
    ///   Resources/Buildings/Corridor_Arm.prefab   (runs from the cell centre to its +Z edge; rotated per link)
    /// Anything missing falls back to the coloured placeholder.
    /// </summary>
    public sealed class BuildingRenderer : MonoBehaviour
    {
        static readonly Color Deck = MeshBuilder.Hex("#3b4252");
        static readonly Color Underside = MeshBuilder.Hex("#262b38");
        static readonly Color Stripe = MeshBuilder.Hex("#ffc93c");
        static readonly Color Light = MeshBuilder.Hex("#4fe3ff");
        static readonly Color Hull = MeshBuilder.Hex("#e9eef3");
        static readonly Color Accent = MeshBuilder.Hex("#ff8a3d");
        static readonly Color Teal = MeshBuilder.Hex("#3fd0c9");
        static readonly Color Warning = MeshBuilder.Hex("#ff3b5c");

        readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
        readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
        readonly List<GameObject> _objects = new List<GameObject>();
        Material _material;
        int _gridWidth, _gridHeight;

        void Awake()
        {
            _material = MeshBuilder.CreateMaterial();
        }

        public void Bind(int gridWidth, int gridHeight)
        {
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
        }

        public static Vector3 Center(Building b, int gridWidth, int gridHeight) =>
            new Vector3(b.Origin.X + b.Def.Width / 2f - gridWidth / 2f, 0f, b.Origin.Y + b.Def.Height / 2f - gridHeight / 2f);

        public void Rebuild(Station s)
        {
            foreach (var go in _objects) Destroy(go);
            _objects.Clear();

            foreach (var b in s.Buildings)
            {
                var root = new GameObject($"{b.Def.Name} #{b.Id}");
                root.transform.SetParent(transform, false);
                root.transform.position = Center(b, _gridWidth, _gridHeight);
                _objects.Add(root);

                if (b.Def.IsCorridor) BuildCorridor(root, s.LinkMask(b.Origin));
                else Spawn(root, b.Type.ToString(), () => Placeholder(b.Type), Quaternion.identity);

                if (!b.Connected) Spawn(root, "NoConnection", WarningBeacon, Quaternion.identity, prefabAllowed: false);
            }
        }

        void BuildCorridor(GameObject root, int mask)
        {
            Spawn(root, "Corridor_Hub", CorridorHub, Quaternion.identity);
            for (int i = 0; i < 4; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                var d = Cell.Directions[i];
                Spawn(root, "Corridor_Arm", CorridorArm, Quaternion.LookRotation(new Vector3(d.X, 0f, d.Y)));
            }
        }

        void Spawn(GameObject parent, string key, System.Func<Mesh> placeholder, Quaternion rotation, bool prefabAllowed = true)
        {
            var prefab = prefabAllowed ? LoadPrefab(key) : null;
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, parent.transform);
            }
            else
            {
                go = new GameObject(key);
                go.transform.SetParent(parent.transform, false);
                if (!_meshes.TryGetValue(key, out var mesh)) _meshes[key] = mesh = placeholder();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = _material;
            }
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = rotation;
        }

        GameObject LoadPrefab(string key)
        {
            if (!_prefabs.TryGetValue(key, out var prefab))
                _prefabs[key] = prefab = Resources.Load<GameObject>($"Buildings/{key}");
            return prefab;
        }

        // ------------------------------------------------------------------ placeholders

        static Mesh Placeholder(BuildingType type)
        {
            var b = new MeshBuilder();
            var def = BuildingCatalog.Get(type);
            float w = def.Width - 0.1f, h = def.Height - 0.1f;

            // Every module is a floating deck with a tapered underside.
            b.AddBox(new Vector3(0f, -0.12f, 0f), new Vector3(w, 0.24f, h), Deck);
            b.AddSpike(new Vector3(0f, -0.24f, 0f), new Vector3(0f, -0.4f - def.Width * 0.3f, 0f), Mathf.Min(w, h) * 0.45f, 4, Underside, 45f);
            AddEdgeStripes(b, w, h);
            foreach (var (sx, sz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                b.AddBox(new Vector3(sx * (w / 2f - 0.12f), 0.06f, sz * (h / 2f - 0.12f)), Vector3.one * 0.12f, Light);

            switch (type)
            {
                case BuildingType.StarterPad:
                    AddLandingMark(b);
                    // The station's fuel tank, in one corner.
                    b.AddPrism(new Vector3(1.0f, 0f, 1.0f), 0.3f, 0.85f, 8, Accent);
                    b.AddPrism(new Vector3(1.0f, 0.85f, 1.0f), 0.22f, 0.12f, 8, Hull);
                    break;
                case BuildingType.LandingPad:
                    AddLandingMark(b);
                    break;
                case BuildingType.Hangar:
                    // An open bay: three walls and a gantry crane, so docked ships stay visible.
                    float wallH = 1.3f;
                    b.AddBox(new Vector3(0f, wallH / 2f, h / 2f - 0.12f), new Vector3(w, wallH, 0.24f), Hull);
                    b.AddBox(new Vector3(w / 2f - 0.12f, wallH / 2f, 0f), new Vector3(0.24f, wallH, h), Hull);
                    b.AddBox(new Vector3(-w / 2f + 0.12f, wallH / 2f, 0f), new Vector3(0.24f, wallH, h), Hull);
                    b.AddBox(new Vector3(0f, wallH + 0.04f, h / 2f - 0.12f), new Vector3(w, 0.08f, 0.3f), Accent);
                    b.AddBox(new Vector3(0f, wallH + 0.1f, 0f), new Vector3(w, 0.14f, 0.28f), Teal);
                    b.AddBox(new Vector3(0f, wallH - 0.15f, 0f), new Vector3(0.3f, 0.3f, 0.3f), Accent);
                    break;
            }
            return b.ToMesh(type.ToString());
        }

        static void AddEdgeStripes(MeshBuilder b, float w, float h)
        {
            const float y = 0.01f, t = 0.08f, inset = 0.25f;
            b.AddBox(new Vector3(0f, y, h / 2f - inset), new Vector3(w - 2 * inset, 0.02f, t), Stripe);
            b.AddBox(new Vector3(0f, y, -h / 2f + inset), new Vector3(w - 2 * inset, 0.02f, t), Stripe);
            b.AddBox(new Vector3(w / 2f - inset, y, 0f), new Vector3(t, 0.02f, h - 2 * inset), Stripe);
            b.AddBox(new Vector3(-w / 2f + inset, y, 0f), new Vector3(t, 0.02f, h - 2 * inset), Stripe);
        }

        /// <summary>The big "L" from the concept sketch.</summary>
        static void AddLandingMark(MeshBuilder b)
        {
            b.AddBox(new Vector3(-0.22f, 0.012f, 0.08f), new Vector3(0.24f, 0.02f, 1.2f), Stripe);
            b.AddBox(new Vector3(0.08f, 0.012f, -0.4f), new Vector3(0.84f, 0.02f, 0.24f), Stripe);
        }

        static Mesh CorridorHub()
        {
            var b = new MeshBuilder();
            b.AddBox(new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.4f, 0.5f), Hull);
            b.AddBox(new Vector3(0f, 0.41f, 0f), new Vector3(0.2f, 0.03f, 0.2f), Teal);
            b.AddSpike(new Vector3(0f, -0.01f, 0f), new Vector3(0f, -0.35f, 0f), 0.2f, 4, Underside, 45f);
            return b.ToMesh("Corridor_Hub");
        }

        /// <summary>One arm, from the cell centre to its +Z edge.</summary>
        static Mesh CorridorArm()
        {
            var b = new MeshBuilder();
            b.AddBox(new Vector3(0f, 0.18f, 0.25f), new Vector3(0.38f, 0.34f, 0.5f), Hull);
            b.AddBox(new Vector3(0f, 0.355f, 0.25f), new Vector3(0.12f, 0.02f, 0.5f), Teal);
            b.AddBox(new Vector3(0.195f, 0.2f, 0.25f), new Vector3(0.02f, 0.08f, 0.4f), Teal);
            b.AddBox(new Vector3(-0.195f, 0.2f, 0.25f), new Vector3(0.02f, 0.08f, 0.4f), Teal);
            return b.ToMesh("Corridor_Arm");
        }

        /// <summary>Red beacon hovering over a module that isn't connected to the station.</summary>
        static Mesh WarningBeacon()
        {
            var b = new MeshBuilder();
            b.AddSpike(new Vector3(0f, 2.2f, 0f), new Vector3(0f, 1.6f, 0f), 0.25f, 4, Warning, 45f);
            b.AddBox(new Vector3(0f, 2.35f, 0f), new Vector3(0.3f, 0.3f, 0.3f), Warning, 45f);
            return b.ToMesh("NoConnection");
        }
    }
}
