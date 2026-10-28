using System.Collections.Generic;
using UnityEngine;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>
    /// Station modules floating in space. Walkways and foundations auto-connect: railings and edge
    /// trim only appear on sides that face open space, so paths and platforms join up on their own.
    /// Landing pads carry a live status light (green free, amber cleared, red occupied, grey offline).
    ///
    /// Art drop-in slots (pivot at the footprint centre, deck top at Y=0, 1 unit = 1 grid cell):
    ///   Resources/Buildings/StarterPad.prefab, LandingPad.prefab, Hangar.prefab
    ///   Resources/Buildings/Walkway_Deck.prefab       (full 1×1 grated deck)
    ///   Resources/Buildings/Walkway_Railing.prefab    (railing along the cell's +Z edge; rotated per open side)
    ///   Resources/Buildings/Foundation.prefab         (full 1×1 deck plate)
    ///   Resources/Buildings/Foundation_Edge.prefab    (trim along the +Z edge; rotated per open side)
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
        static readonly Color Grate = MeshBuilder.Hex("#4a5163");
        static readonly Color GrateSlat = MeshBuilder.Hex("#353a48");
        static readonly Color Post = MeshBuilder.Hex("#8a93a6");
        static readonly Color Plate = MeshBuilder.Hex("#59627a");
        static readonly Color PlateTop = MeshBuilder.Hex("#6b7590");

        enum PadStatus
        {
            Offline,
            Free,
            Cleared,
            Occupied,
        }

        static readonly Dictionary<PadStatus, Color> StatusColors = new Dictionary<PadStatus, Color>
        {
            { PadStatus.Offline, MeshBuilder.Hex("#5b6070") },
            { PadStatus.Free, MeshBuilder.Hex("#45ff8a") },
            { PadStatus.Cleared, MeshBuilder.Hex("#ffb020") },
            { PadStatus.Occupied, MeshBuilder.Hex("#ff4060") },
        };

        readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
        readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
        readonly List<GameObject> _objects = new List<GameObject>();
        readonly Dictionary<int, MeshFilter> _statusLights = new Dictionary<int, MeshFilter>();
        readonly Dictionary<PadStatus, Mesh> _statusMeshes = new Dictionary<PadStatus, Mesh>();
        Material _material;
        Station _station;

        void Awake()
        {
            _material = MeshBuilder.CreateMaterial();
            foreach (var kv in StatusColors)
            {
                var b = new MeshBuilder();
                b.AddBox(Vector3.zero, Vector3.one * 0.16f, kv.Value, 45f);
                _statusMeshes[kv.Key] = b.ToMesh($"Status {kv.Key}");
            }
        }

        public void Bind(Station station) => _station = station;

        public static Vector3 Center(Building b, int gridWidth, int gridHeight) =>
            new Vector3(b.Origin.X + b.Def.Width / 2f - gridWidth / 2f, 0f, b.Origin.Y + b.Def.Height / 2f - gridHeight / 2f);

        public void Rebuild()
        {
            foreach (var go in _objects) Destroy(go);
            _objects.Clear();
            _statusLights.Clear();
            var s = _station;

            foreach (var b in s.Buildings)
            {
                var root = new GameObject($"{s.Label(b)} #{b.Id}");
                root.transform.SetParent(transform, false);
                root.transform.position = Center(b, s.Width, s.Height);
                _objects.Add(root);

                switch (b.Type)
                {
                    case BuildingType.Walkway:
                        BuildTile(root, s.LinkMask(b.Origin), "Walkway_Deck", WalkwayDeck, "Walkway_Railing", WalkwayRailing);
                        break;
                    case BuildingType.Foundation:
                        BuildTile(root, s.LinkMask(b.Origin), "Foundation", FoundationPlate, "Foundation_Edge", FoundationEdge);
                        break;
                    default:
                        Spawn(root, b.Type.ToString(), () => Placeholder(b.Type), Vector3.zero, Quaternion.identity);
                        break;
                }

                if (b.Def.Serves.Length > 0) AddStatusLight(root, b);
                if (!b.Connected) Spawn(root, "NoConnection", WarningBeacon, Vector3.zero, Quaternion.identity, prefabAllowed: false);
            }
            UpdateStatusLights();
        }

        void Update()
        {
            if (_station != null) UpdateStatusLights();
        }

        // ------------------------------------------------------------------ auto-connecting tiles

        /// <summary>A deck piece plus an edge piece on every side that faces open space.</summary>
        void BuildTile(GameObject root, int mask, string deckKey, System.Func<Mesh> deck, string edgeKey, System.Func<Mesh> edge)
        {
            Spawn(root, deckKey, deck, Vector3.zero, Quaternion.identity);
            for (int i = 0; i < 4; i++)
            {
                if ((mask & (1 << i)) != 0) continue; // a neighbour is there: leave the side open
                var d = Cell.Directions[i];
                Spawn(root, edgeKey, edge, Vector3.zero, Quaternion.LookRotation(new Vector3(d.X, 0f, d.Y)));
            }
        }

        // ------------------------------------------------------------------ pad status lights

        void AddStatusLight(GameObject root, Building b)
        {
            var light = new GameObject("StatusLight");
            light.transform.SetParent(root.transform, false);
            float corner = b.Def.Width / 2f - 0.3f;
            light.transform.localPosition = new Vector3(corner, 0.75f, b.Def.Height / 2f - 0.3f);
            var filter = light.AddComponent<MeshFilter>();
            light.AddComponent<MeshRenderer>().sharedMaterial = _material;
            _statusLights[b.Id] = filter;

            // The pole the light sits on.
            var pole = new MeshBuilder();
            pole.AddBox(new Vector3(0f, -0.38f, 0f), new Vector3(0.06f, 0.7f, 0.06f), Post);
            var poleGo = new GameObject("StatusPole");
            poleGo.transform.SetParent(light.transform, false);
            poleGo.AddComponent<MeshFilter>().sharedMesh = Cached("StatusPole", () => pole.ToMesh("StatusPole"));
            poleGo.AddComponent<MeshRenderer>().sharedMaterial = _material;
        }

        void UpdateStatusLights()
        {
            foreach (var kv in _statusLights)
            {
                var b = _station.GetBuilding(kv.Key);
                if (b == null || kv.Value == null) continue;

                var status = !b.Connected ? PadStatus.Offline
                    : b.ShipId == null ? PadStatus.Free
                    : _station.GetShip(b.ShipId.Value)?.State == ShipState.Landing ? PadStatus.Cleared
                    : PadStatus.Occupied;
                var mesh = _statusMeshes[status];
                if (kv.Value.sharedMesh != mesh) kv.Value.sharedMesh = mesh;
            }
        }

        // ------------------------------------------------------------------ spawning

        void Spawn(GameObject parent, string key, System.Func<Mesh> placeholder, Vector3 position, Quaternion rotation, bool prefabAllowed = true)
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
                go.AddComponent<MeshFilter>().sharedMesh = Cached(key, placeholder);
                go.AddComponent<MeshRenderer>().sharedMaterial = _material;
            }
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
        }

        Mesh Cached(string key, System.Func<Mesh> build)
        {
            if (!_meshes.TryGetValue(key, out var mesh)) _meshes[key] = mesh = build();
            return mesh;
        }

        GameObject LoadPrefab(string key)
        {
            if (!_prefabs.TryGetValue(key, out var prefab))
                _prefabs[key] = prefab = Resources.Load<GameObject>($"Buildings/{key}");
            return prefab;
        }

        // ------------------------------------------------------------------ module placeholders

        static Mesh Placeholder(BuildingType type)
        {
            var b = new MeshBuilder();
            var def = BuildingCatalog.Get(type);
            float w = def.Width - 0.1f, h = def.Height - 0.1f;

            // Every module is a floating deck with a tapered underside, hazard stripes and corner lights.
            b.AddBox(new Vector3(0f, -0.12f, 0f), new Vector3(w, 0.24f, h), Deck);
            b.AddSpike(new Vector3(0f, -0.24f, 0f), new Vector3(0f, -0.4f - def.Width * 0.3f, 0f), Mathf.Min(w, h) * 0.45f, 4, Underside, 45f);
            AddEdgeStripes(b, w, h);
            foreach (var (sx, sz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                b.AddBox(new Vector3(sx * (w / 2f - 0.12f), 0.06f, sz * (h / 2f - 0.12f)), Vector3.one * 0.12f, Light);

            switch (type)
            {
                case BuildingType.StarterPad:
                    AddPadDetails(b, w, h);
                    // The station's fuel tank and the comms mast ships hail.
                    b.AddPrism(new Vector3(-1.0f, 0f, 1.0f), 0.3f, 0.85f, 8, Accent);
                    b.AddPrism(new Vector3(-1.0f, 0.85f, 1.0f), 0.22f, 0.12f, 8, Hull);
                    b.AddBox(new Vector3(-1.0f, 0.45f, 0.62f), new Vector3(0.08f, 0.08f, 0.5f), Post);
                    b.AddPrism(new Vector3(1.05f, 0f, -1.05f), 0.04f, 1.7f, 4, Post);
                    b.AddSpike(new Vector3(1.05f, 1.55f, -1.05f), new Vector3(0.85f, 1.75f, -0.85f), 0.22f, 6, Hull);
                    b.AddBox(new Vector3(1.05f, 1.75f, -1.05f), Vector3.one * 0.08f, Warning);
                    break;

                case BuildingType.LandingPad:
                    AddPadDetails(b, w, h);
                    break;

                case BuildingType.Hangar:
                    // An open bay: three walls, a gantry crane and floor markings, so docked ships stay visible.
                    const float wallH = 1.3f;
                    b.AddBox(new Vector3(0f, wallH / 2f, h / 2f - 0.12f), new Vector3(w, wallH, 0.24f), Hull);
                    b.AddBox(new Vector3(w / 2f - 0.12f, wallH / 2f, 0f), new Vector3(0.24f, wallH, h), Hull);
                    b.AddBox(new Vector3(-w / 2f + 0.12f, wallH / 2f, 0f), new Vector3(0.24f, wallH, h), Hull);
                    for (int i = -1; i <= 1; i++)
                        b.AddBox(new Vector3(i * 1.1f, 0.85f, h / 2f - 0.25f), new Vector3(0.6f, 0.22f, 0.02f), Light);
                    b.AddBox(new Vector3(0f, wallH + 0.04f, h / 2f - 0.12f), new Vector3(w, 0.08f, 0.3f), Accent);
                    b.AddBox(new Vector3(0f, wallH + 0.1f, 0f), new Vector3(w, 0.14f, 0.28f), Teal);
                    b.AddBox(new Vector3(0.6f, wallH - 0.15f, 0f), new Vector3(0.3f, 0.3f, 0.3f), Accent);
                    b.AddBox(new Vector3(0.6f, wallH - 0.55f, 0f), new Vector3(0.03f, 0.5f, 0.03f), Post);
                    b.AddBox(new Vector3(0.6f, wallH - 0.82f, 0f), new Vector3(0.14f, 0.06f, 0.14f), Stripe);
                    // Tool racks along the side walls.
                    foreach (float side in new[] { -1f, 1f })
                    for (int i = 0; i < 3; i++)
                        b.AddBox(new Vector3(side * (w / 2f - 0.36f), 0.2f, -0.9f + i * 0.7f), new Vector3(0.2f, 0.4f, 0.4f), i % 2 == 0 ? Accent : Teal);
                    // Floor guide lines.
                    b.AddBox(new Vector3(0f, 0.012f, -0.4f), new Vector3(0.1f, 0.02f, 2.2f), Stripe);
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

        /// <summary>The big "L" from the concept sketch, a touchdown ring and the fuel pump.</summary>
        static void AddPadDetails(MeshBuilder b, float w, float h)
        {
            b.AddBox(new Vector3(-0.22f, 0.012f, 0.08f), new Vector3(0.24f, 0.02f, 1.2f), Stripe);
            b.AddBox(new Vector3(0.08f, 0.012f, -0.4f), new Vector3(0.84f, 0.02f, 0.24f), Stripe);

            const int segments = 16;
            const float radius = 1.05f;
            for (int i = 0; i < segments; i += 1)
            {
                if (i % 2 == 1) continue; // dashed ring
                float a = 2f * Mathf.PI * (i + 0.5f) / segments;
                var p = new Vector3(Mathf.Cos(a) * radius, 0.011f, Mathf.Sin(a) * radius);
                b.AddBox(p, new Vector3(0.36f, 0.02f, 0.06f), Hull, -a * Mathf.Rad2Deg + 90f);
            }

            // Fuel pump on the south edge, with its hose reel.
            b.AddBox(new Vector3(0.6f, 0.22f, -h / 2f + 0.18f), new Vector3(0.3f, 0.44f, 0.22f), Accent);
            b.AddPrism(new Vector3(0.6f, 0.44f, -h / 2f + 0.18f), 0.1f, 0.06f, 8, Hull);
            b.AddBox(new Vector3(0.6f, 0.3f, -h / 2f + 0.06f), new Vector3(0.16f, 0.1f, 0.02f), Light);
        }

        // ------------------------------------------------------------------ walkway and foundation pieces

        static Mesh WalkwayDeck()
        {
            var b = new MeshBuilder();
            b.AddBox(new Vector3(0f, -0.05f, 0f), new Vector3(1f, 0.1f, 1f), Grate);
            for (int i = -2; i <= 2; i++)
                b.AddBox(new Vector3(i * 0.18f, 0.003f, 0f), new Vector3(0.05f, 0.01f, 0.96f), GrateSlat);
            b.AddSpike(new Vector3(0f, -0.1f, 0f), new Vector3(0f, -0.45f, 0f), 0.22f, 4, Underside, 45f);
            return b.ToMesh("Walkway_Deck");
        }

        /// <summary>A railing along the cell's +Z edge: two posts, a yellow handrail and a mid rail.</summary>
        static Mesh WalkwayRailing()
        {
            var b = new MeshBuilder();
            const float z = 0.46f;
            foreach (float x in new[] { -0.46f, 0f, 0.46f })
                b.AddBox(new Vector3(x, 0.18f, z), new Vector3(0.05f, 0.36f, 0.05f), Post);
            b.AddBox(new Vector3(0f, 0.37f, z), new Vector3(0.98f, 0.04f, 0.05f), Stripe);
            b.AddBox(new Vector3(0f, 0.19f, z), new Vector3(0.98f, 0.025f, 0.03f), Post);
            return b.ToMesh("Walkway_Railing");
        }

        static Mesh FoundationPlate()
        {
            var b = new MeshBuilder();
            b.AddBox(new Vector3(0f, -0.15f, 0f), new Vector3(1f, 0.3f, 1f), Plate);
            b.AddBox(new Vector3(0f, 0.005f, 0f), new Vector3(0.86f, 0.01f, 0.86f), PlateTop);
            foreach (var (sx, sz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                b.AddBox(new Vector3(sx * 0.36f, 0.015f, sz * 0.36f), Vector3.one * 0.05f, Post);
            b.AddSpike(new Vector3(0f, -0.3f, 0f), new Vector3(0f, -0.8f, 0f), 0.35f, 4, Underside, 45f);
            return b.ToMesh("Foundation");
        }

        /// <summary>Hazard trim along the +Z edge of a foundation that faces open space.</summary>
        static Mesh FoundationEdge()
        {
            var b = new MeshBuilder();
            for (int i = 0; i < 4; i++)
                b.AddBox(new Vector3(-0.375f + i * 0.25f, 0.02f, 0.46f), new Vector3(0.25f, 0.04f, 0.08f), i % 2 == 0 ? Stripe : GrateSlat);
            return b.ToMesh("Foundation_Edge");
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
