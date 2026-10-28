using System.Collections.Generic;
using UnityEngine;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>
    /// Station modules floating in the dark. Walkways and foundations auto-connect: railings and edge
    /// trim only appear on sides that face open space, so paths and platforms join up on their own.
    ///
    /// Lighting and FX: sodium floodlights on pads (some failing), cold work lights in hangars, a red
    /// rotating beacon on the comms mast, lamp posts along walkways, fuel lines venting vapour while a
    /// ship refuels, welding sparks and arc flashes during repairs. Unconnected modules go dark except
    /// for a flickering red emergency lamp. Pads carry a status light (green free, amber cleared,
    /// red occupied, grey offline).
    ///
    /// Art drop-in slots (pivot at the footprint centre, deck top at Y=0, 1 unit = 1 grid cell):
    ///   Resources/Buildings/StarterPad.prefab, LandingPad.prefab, Hangar.prefab
    ///   Resources/Buildings/Walkway_Deck.prefab, Walkway_Railing.prefab (+Z edge), Walkway_Lamp.prefab
    ///   Resources/Buildings/Foundation.prefab, Foundation_Edge.prefab (+Z edge)
    /// Lamps and effects are added in code on top of any art, at the positions noted in docs/TECH.md.
    /// </summary>
    public sealed class BuildingRenderer : MonoBehaviour
    {
        static readonly Color Deck = MeshBuilder.Hex("#2c2f34");
        static readonly Color Underside = MeshBuilder.Hex("#15171a");
        static readonly Color Stripe = MeshBuilder.Hex("#b8942a");
        static readonly Color Hull = MeshBuilder.Hex("#7d8187");
        static readonly Color Rust = MeshBuilder.Hex("#7a3418");
        static readonly Color Oxide = MeshBuilder.Hex("#3d5f55");
        static readonly Color Grate = MeshBuilder.Hex("#33373d");
        static readonly Color GrateSlat = MeshBuilder.Hex("#1f2125");
        static readonly Color Post = MeshBuilder.Hex("#55595f");
        static readonly Color Plate = MeshBuilder.Hex("#3a3e44");
        static readonly Color PlateTop = MeshBuilder.Hex("#44494f");
        static readonly Color Dead = MeshBuilder.Hex("#2a2c2f");

        public static readonly Color Sodium = MeshBuilder.Hex("#ff9a3c");
        static readonly Color WorkLight = MeshBuilder.Hex("#cfe0ff");
        static readonly Color Alarm = MeshBuilder.Hex("#ff2a3a");
        static readonly Color ArcLight = MeshBuilder.Hex("#bcd8ff");

        enum PadStatus
        {
            Offline,
            Free,
            Cleared,
            Occupied,
        }

        static readonly Dictionary<PadStatus, Color> StatusColors = new Dictionary<PadStatus, Color>
        {
            { PadStatus.Offline, MeshBuilder.Hex("#3a3d44") },
            { PadStatus.Free, MeshBuilder.Hex("#3cff7a") },
            { PadStatus.Cleared, MeshBuilder.Hex("#ffb020") },
            { PadStatus.Occupied, MeshBuilder.Hex("#ff3048") },
        };

        /// <summary>Live bits of a pad or hangar, updated every frame from the sim.</summary>
        sealed class PadFx
        {
            public MeshFilter StatusMesh;
            public FxLight StatusLight;
            public ParticleSystem Vapor;
            public ParticleSystem Weld;
            public FxLight WeldLight;
        }

        readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
        readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
        readonly List<GameObject> _objects = new List<GameObject>();
        readonly Dictionary<int, PadFx> _pads = new Dictionary<int, PadFx>();
        readonly Dictionary<PadStatus, Mesh> _statusMeshes = new Dictionary<PadStatus, Mesh>();
        Material _material;
        Station _station;

        void Awake()
        {
            _material = MeshBuilder.CreateMaterial();
            foreach (var kv in StatusColors)
            {
                var b = new MeshBuilder();
                b.AddBox(Vector3.zero, Vector3.one * 0.14f, kv.Value, 45f, kv.Key == PadStatus.Offline ? 0f : 1f);
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
            _pads.Clear();
            var s = _station;

            foreach (var b in s.Buildings)
            {
                var root = new GameObject($"{s.Label(b)} #{b.Id}");
                root.transform.SetParent(transform, false);
                root.transform.position = Center(b, s.Width, s.Height);
                _objects.Add(root);
                bool powered = b.Connected;

                switch (b.Type)
                {
                    case BuildingType.Walkway:
                        int mask = s.LinkMask(b.Origin);
                        BuildTile(root, mask, "Walkway_Deck", WalkwayDeck, "Walkway_Railing", WalkwayRailing);
                        AddWalkwayLamp(root, b, mask, powered);
                        break;
                    case BuildingType.Foundation:
                        BuildTile(root, s.LinkMask(b.Origin), "Foundation", FoundationPlate, "Foundation_Edge", FoundationEdge);
                        break;
                    default:
                        string key = powered ? b.Type.ToString() : b.Type + "_Dark";
                        Spawn(root, b.Type.ToString(), key, () => Placeholder(b.Type, powered), Vector3.zero, Quaternion.identity);
                        if (powered) AddFixtures(root, b);
                        break;
                }

                if (b.Def.Serves.Length > 0) AddPadFx(root, b, powered);
                if (!powered) AddEmergencyLamp(root, b);
            }
            UpdatePads();
        }

        void Update()
        {
            if (_station != null) UpdatePads();
        }

        // ------------------------------------------------------------------ lamps and effects

        /// <summary>Floodlights, beacons and work lights for a connected module.</summary>
        void AddFixtures(GameObject root, Building b)
        {
            var t = root.transform;
            float w = b.Def.Width - 0.1f, h = b.Def.Height - 0.1f;
            bool failing = b.Id % 5 == 3; // the odd lamp is always on its way out

            switch (b.Type)
            {
                case BuildingType.StarterPad:
                case BuildingType.LandingPad:
                    FxLight.Create(t, new Vector3(-(w / 2f - 0.25f), 1.0f, -(h / 2f - 0.25f)), Sodium, 1.5f, 3.4f, failing ? FxLightMode.Flicker : FxLightMode.Steady);
                    FxLight.Create(t, new Vector3(w / 2f - 0.25f, 1.0f, h / 2f - 0.25f), Sodium, 1.5f, 3.4f);
                    if (b.Type == BuildingType.StarterPad)
                        FxLight.Create(t, new Vector3(1.05f, 1.75f, -1.05f), Alarm, 1.6f, 4f, FxLightMode.Strobe);
                    break;

                case BuildingType.Hangar:
                    FxLight.Create(t, new Vector3(-(w / 2f - 0.4f), 1.2f, h / 2f - 0.4f), WorkLight, 1.3f, 3.8f);
                    FxLight.Create(t, new Vector3(w / 2f - 0.4f, 1.2f, h / 2f - 0.4f), WorkLight, 1.3f, 3.8f, failing ? FxLightMode.Flicker : FxLightMode.Steady);
                    FxLight.Create(t, new Vector3(0f, 1.1f, -h / 2f + 0.2f), Alarm, 0.7f, 2.2f, FxLightMode.Pulse);
                    break;
            }
        }

        void AddPadFx(GameObject root, Building b, bool powered)
        {
            var t = root.transform;
            float w = b.Def.Width - 0.1f, h = b.Def.Height - 0.1f;
            var fx = new PadFx();

            var status = new GameObject("StatusLight");
            status.transform.SetParent(t, false);
            status.transform.localPosition = new Vector3(w / 2f - 0.25f, 0.78f, h / 2f - 0.25f);
            fx.StatusMesh = status.AddComponent<MeshFilter>();
            status.AddComponent<MeshRenderer>().sharedMaterial = _material;
            if (powered) fx.StatusLight = FxLight.Create(t, status.transform.localPosition, Color.white, 0.6f, 1.3f);

            if (powered && b.Type != BuildingType.Hangar)
                fx.Vapor = Fx.Create(t, FxPreset.Vapor, new Vector3(0.6f, 0.45f, -h / 2f + 0.18f), Vector3.up);

            if (powered && b.Def.Repairs)
            {
                fx.Weld = Fx.Create(t, FxPreset.Weld, new Vector3(0.15f, 0.55f, 0.1f), Vector3.up);
                fx.WeldLight = FxLight.Create(t, new Vector3(0.15f, 0.7f, 0.1f), ArcLight, 0f, 3.2f, FxLightMode.Flicker);
            }
            _pads[b.Id] = fx;
        }

        void UpdatePads()
        {
            foreach (var kv in _pads)
            {
                var b = _station.GetBuilding(kv.Key);
                var fx = kv.Value;
                if (b == null || fx.StatusMesh == null) continue;
                var ship = b.ShipId.HasValue ? _station.GetShip(b.ShipId.Value) : null;

                var status = !b.Connected ? PadStatus.Offline
                    : ship == null ? PadStatus.Free
                    : ship.State == ShipState.Landing ? PadStatus.Cleared
                    : PadStatus.Occupied;
                var mesh = _statusMeshes[status];
                if (fx.StatusMesh.sharedMesh != mesh) fx.StatusMesh.sharedMesh = mesh;
                if (fx.StatusLight != null) fx.StatusLight.Color = StatusColors[status];

                bool servicing = ship != null && ship.State == ShipState.Servicing;
                bool refuelling = servicing && ship.FuelLoaded < ship.FuelNeeded;
                bool welding = servicing && !refuelling && ship.NeedsRepair && !ship.Repaired && b.Def.Repairs;

                Fx.SetActive(fx.Vapor, refuelling);
                Fx.SetActive(fx.Weld, welding);
                if (fx.WeldLight != null) fx.WeldLight.Intensity = welding ? 2.4f : 0f;
            }
        }

        void AddWalkwayLamp(GameObject root, Building b, int mask, bool powered)
        {
            // A lamp post on roughly every third walkway tile, on an open side.
            if (mask == 0xF || (b.Origin.X * 7 + b.Origin.Y * 13) % 3 != 0) return;
            int side = 0;
            while ((mask & (1 << side)) != 0) side++;
            var d = Cell.Directions[side];
            var rotation = Quaternion.LookRotation(new Vector3(d.X, 0f, d.Y));

            string key = powered ? "Walkway_Lamp" : "Walkway_Lamp_Dark";
            Spawn(root, "Walkway_Lamp", key, () => WalkwayLamp(powered), Vector3.zero, rotation);
            if (powered)
            {
                var mode = (b.Origin.X + b.Origin.Y) % 4 == 0 ? FxLightMode.Flicker : FxLightMode.Steady;
                FxLight.Create(root.transform, rotation * new Vector3(0.38f, 0.72f, 0.38f), Sodium, 0.9f, 2f, mode);
            }
        }

        void AddEmergencyLamp(GameObject root, Building b)
        {
            Spawn(root, "EmergencyLamp", "EmergencyLamp", EmergencyLamp, Vector3.zero, Quaternion.identity, prefabAllowed: false);
            FxLight.Create(root.transform, new Vector3(0f, 1.15f, 0f), Alarm, 1.4f, Mathf.Max(2.4f, b.Def.Width * 0.9f), FxLightMode.Flicker);
        }

        // ------------------------------------------------------------------ auto-connecting tiles

        /// <summary>A deck piece plus an edge piece on every side that faces open space.</summary>
        void BuildTile(GameObject root, int mask, string deckKey, System.Func<Mesh> deck, string edgeKey, System.Func<Mesh> edge)
        {
            Spawn(root, deckKey, deckKey, deck, Vector3.zero, Quaternion.identity);
            for (int i = 0; i < 4; i++)
            {
                if ((mask & (1 << i)) != 0) continue; // a neighbour is there: leave the side open
                var d = Cell.Directions[i];
                Spawn(root, edgeKey, edgeKey, edge, Vector3.zero, Quaternion.LookRotation(new Vector3(d.X, 0f, d.Y)));
            }
        }

        // ------------------------------------------------------------------ spawning

        void Spawn(GameObject parent, string prefabKey, string meshKey, System.Func<Mesh> placeholder, Vector3 position, Quaternion rotation, bool prefabAllowed = true)
        {
            var prefab = prefabAllowed ? LoadPrefab(prefabKey) : null;
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, parent.transform);
            }
            else
            {
                go = new GameObject(meshKey);
                go.transform.SetParent(parent.transform, false);
                if (!_meshes.TryGetValue(meshKey, out var mesh)) _meshes[meshKey] = mesh = placeholder();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = _material;
            }
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
        }

        GameObject LoadPrefab(string key)
        {
            if (!_prefabs.TryGetValue(key, out var prefab))
                _prefabs[key] = prefab = Resources.Load<GameObject>($"Buildings/{key}");
            return prefab;
        }

        // ------------------------------------------------------------------ module placeholders

        /// <summary>Grimy industrial placeholder; unpowered modules get dead (non-glowing) lamps.</summary>
        static Mesh Placeholder(BuildingType type, bool powered)
        {
            var b = new MeshBuilder();
            var def = BuildingCatalog.Get(type);
            float w = def.Width - 0.1f, h = def.Height - 0.1f;
            float glow = powered ? 1f : 0f;
            var lamp = powered ? Sodium : Dead;

            b.AddBox(new Vector3(0f, -0.12f, 0f), new Vector3(w, 0.24f, h), Deck);
            b.AddSpike(new Vector3(0f, -0.24f, 0f), new Vector3(0f, -0.4f - def.Width * 0.3f, 0f), Mathf.Min(w, h) * 0.45f, 4, Underside, 45f);
            AddEdgeStripes(b, w, h);
            foreach (var (sx, sz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                b.AddBox(new Vector3(sx * (w / 2f - 0.12f), 0.04f, sz * (h / 2f - 0.12f)), Vector3.one * 0.08f, lamp, 0f, glow * 0.6f);

            switch (type)
            {
                case BuildingType.StarterPad:
                    AddPadDetails(b, w, h, lamp, glow);
                    // The station's fuel tank and the comms mast ships hail, with its rotating beacon.
                    b.AddPrism(new Vector3(-1.0f, 0f, 1.0f), 0.3f, 0.85f, 8, Rust);
                    b.AddPrism(new Vector3(-1.0f, 0.85f, 1.0f), 0.22f, 0.12f, 8, Hull);
                    b.AddBox(new Vector3(-1.0f, 0.45f, 0.62f), new Vector3(0.08f, 0.08f, 0.5f), Post);
                    b.AddPrism(new Vector3(1.05f, 0f, -1.05f), 0.04f, 1.7f, 4, Post);
                    b.AddSpike(new Vector3(1.05f, 1.55f, -1.05f), new Vector3(0.85f, 1.75f, -0.85f), 0.22f, 6, Hull);
                    b.AddBox(new Vector3(1.05f, 1.75f, -1.05f), Vector3.one * 0.09f, powered ? Alarm : Dead, 0f, glow);
                    break;

                case BuildingType.LandingPad:
                    AddPadDetails(b, w, h, lamp, glow);
                    break;

                case BuildingType.Hangar:
                    const float wallH = 1.3f;
                    b.AddBox(new Vector3(0f, wallH / 2f, h / 2f - 0.12f), new Vector3(w, wallH, 0.24f), Hull);
                    b.AddBox(new Vector3(w / 2f - 0.12f, wallH / 2f, 0f), new Vector3(0.24f, wallH, h), Hull);
                    b.AddBox(new Vector3(-w / 2f + 0.12f, wallH / 2f, 0f), new Vector3(0.24f, wallH, h), Hull);
                    for (int i = -1; i <= 1; i++)
                        b.AddBox(new Vector3(i * 1.1f, 0.85f, h / 2f - 0.25f), new Vector3(0.6f, 0.22f, 0.02f), powered ? Oxide : Dead, 0f, glow * 0.5f);
                    b.AddBox(new Vector3(0f, wallH + 0.04f, h / 2f - 0.12f), new Vector3(w, 0.08f, 0.3f), Rust);
                    b.AddBox(new Vector3(0f, wallH + 0.1f, 0f), new Vector3(w, 0.14f, 0.28f), Post);
                    b.AddBox(new Vector3(0.6f, wallH - 0.15f, 0f), new Vector3(0.3f, 0.3f, 0.3f), Rust);
                    b.AddBox(new Vector3(0.6f, wallH - 0.55f, 0f), new Vector3(0.03f, 0.5f, 0.03f), Post);
                    b.AddBox(new Vector3(0.6f, wallH - 0.82f, 0f), new Vector3(0.14f, 0.06f, 0.14f), Stripe);
                    // Work-light housings in the back corners, tool racks, floor guide line.
                    foreach (float side in new[] { -1f, 1f })
                    {
                        b.AddBox(new Vector3(side * (w / 2f - 0.4f), 1.2f, h / 2f - 0.4f), new Vector3(0.24f, 0.12f, 0.18f), powered ? WorkLight : Dead, 0f, glow);
                        for (int i = 0; i < 3; i++)
                            b.AddBox(new Vector3(side * (w / 2f - 0.36f), 0.2f, -0.9f + i * 0.7f), new Vector3(0.2f, 0.4f, 0.4f), i % 2 == 0 ? Rust : Oxide);
                    }
                    b.AddBox(new Vector3(0f, 0.012f, -0.4f), new Vector3(0.1f, 0.02f, 2.2f), Stripe);
                    b.AddBox(new Vector3(0f, 1.1f, -h / 2f + 0.2f), new Vector3(0.14f, 0.14f, 0.14f), powered ? Alarm : Dead, 45f, glow);
                    break;
            }
            return b.ToMesh(type + (powered ? "" : "_Dark"));
        }

        static void AddEdgeStripes(MeshBuilder b, float w, float h)
        {
            const float y = 0.01f, t = 0.08f, inset = 0.25f;
            b.AddBox(new Vector3(0f, y, h / 2f - inset), new Vector3(w - 2 * inset, 0.02f, t), Stripe);
            b.AddBox(new Vector3(0f, y, -h / 2f + inset), new Vector3(w - 2 * inset, 0.02f, t), Stripe);
            b.AddBox(new Vector3(w / 2f - inset, y, 0f), new Vector3(t, 0.02f, h - 2 * inset), Stripe);
            b.AddBox(new Vector3(-w / 2f + inset, y, 0f), new Vector3(t, 0.02f, h - 2 * inset), Stripe);
        }

        /// <summary>The big "L" from the concept sketch, a touchdown ring, the fuel pump and two floodlight masts.</summary>
        static void AddPadDetails(MeshBuilder b, float w, float h, Color lamp, float glow)
        {
            b.AddBox(new Vector3(-0.22f, 0.012f, 0.08f), new Vector3(0.24f, 0.02f, 1.2f), Stripe);
            b.AddBox(new Vector3(0.08f, 0.012f, -0.4f), new Vector3(0.84f, 0.02f, 0.24f), Stripe);

            const int segments = 16;
            const float radius = 1.05f;
            for (int i = 0; i < segments; i += 2) // dashed ring
            {
                float a = 2f * Mathf.PI * (i + 0.5f) / segments;
                var p = new Vector3(Mathf.Cos(a) * radius, 0.011f, Mathf.Sin(a) * radius);
                b.AddBox(p, new Vector3(0.36f, 0.02f, 0.06f), Hull, -a * Mathf.Rad2Deg + 90f);
            }

            // Fuel pump on the south edge, with its hose reel and a dim readout.
            b.AddBox(new Vector3(0.6f, 0.22f, -h / 2f + 0.18f), new Vector3(0.3f, 0.44f, 0.22f), Rust);
            b.AddPrism(new Vector3(0.6f, 0.44f, -h / 2f + 0.18f), 0.1f, 0.06f, 8, Hull);
            b.AddBox(new Vector3(0.6f, 0.3f, -h / 2f + 0.06f), new Vector3(0.16f, 0.1f, 0.02f), lamp, 0f, glow * 0.7f);

            // Floodlight masts in two opposite corners (the north-east one also carries the status light).
            foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, 1f) })
            {
                var basePos = new Vector3(sx * (w / 2f - 0.25f), 0f, sz * (h / 2f - 0.25f));
                b.AddBox(basePos + new Vector3(0f, 0.5f, 0f), new Vector3(0.06f, 1f, 0.06f), Post);
                b.AddBox(basePos + new Vector3(0f, 1.02f, 0f), new Vector3(0.24f, 0.1f, 0.18f), lamp, 45f, glow);
            }
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

        /// <summary>A railing along the cell's +Z edge: posts, a hazard-yellow handrail and a mid rail.</summary>
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

        /// <summary>A lamp post at the +X/+Z corner (rotated onto an open side).</summary>
        static Mesh WalkwayLamp(bool powered)
        {
            var b = new MeshBuilder();
            b.AddBox(new Vector3(0.38f, 0.35f, 0.38f), new Vector3(0.05f, 0.7f, 0.05f), Post);
            b.AddBox(new Vector3(0.38f, 0.72f, 0.38f), new Vector3(0.14f, 0.07f, 0.14f), powered ? Sodium : Dead, 45f, powered ? 1f : 0f);
            return b.ToMesh(powered ? "Walkway_Lamp" : "Walkway_Lamp_Dark");
        }

        static Mesh FoundationPlate()
        {
            var b = new MeshBuilder();
            b.AddBox(new Vector3(0f, -0.15f, 0f), new Vector3(1f, 0.3f, 1f), Plate);
            b.AddBox(new Vector3(0f, 0.005f, 0f), new Vector3(0.86f, 0.01f, 0.86f), PlateTop);
            foreach (var (sx, sz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                b.AddBox(new Vector3(sx * 0.36f, 0.015f, sz * 0.36f), Vector3.one * 0.05f, Post);
            b.AddBox(new Vector3(0.2f, 0.012f, -0.25f), new Vector3(0.3f, 0.005f, 0.08f), Rust);
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

        /// <summary>Red emergency lamp on a short post: the only light left on an unconnected module.</summary>
        static Mesh EmergencyLamp()
        {
            var b = new MeshBuilder();
            b.AddBox(new Vector3(0f, 0.5f, 0f), new Vector3(0.07f, 1f, 0.07f), Post);
            b.AddBox(new Vector3(0f, 1.08f, 0f), new Vector3(0.2f, 0.16f, 0.2f), Alarm, 45f, 1f);
            return b.ToMesh("EmergencyLamp");
        }
    }
}
