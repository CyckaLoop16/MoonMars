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
    ///   Modules (base at the foundation deck, Y=0): SolarArray, BatteryBank, FuelCellGenerator, FuelTank,
    ///   IceRefinery, PilotLounge, CommsArray, AtcTower; moving parts SolarArray_Wing (panels face +Y),
    ///   CommsArray_Dish (faces +Z), AtcTower_Radar (spins about Y).
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
        static readonly Color PanelBlue = MeshBuilder.Hex("#1c2b4d");
        static readonly Color PanelCell = MeshBuilder.Hex("#3a5c96");
        static readonly Color Foil = MeshBuilder.Hex("#b8902a");
        static readonly Color Radiator = MeshBuilder.Hex("#b9bcc0");
        static readonly Color HabWhite = MeshBuilder.Hex("#8e9297");
        static readonly Color Window = MeshBuilder.Hex("#ffcf8a");
        static readonly Color Neon = MeshBuilder.Hex("#ff3ab0");
        static readonly Color Ice = MeshBuilder.Hex("#a9cfe0");
        static readonly Color Glass = MeshBuilder.Hex("#9feaff");
        static readonly Color TankPaint = MeshBuilder.Hex("#a8562a");
        static readonly Color Casing = MeshBuilder.Hex("#3a4048");
        static readonly Color Charge = MeshBuilder.Hex("#3cff7a");

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

        /// <summary>Moving and glowing parts of a module, updated every frame from the sim.</summary>
        sealed class ModuleFx
        {
            public Transform Wing;
            public Transform Dish;
            public Transform Radar;
            public GameObject[] ChargeLights;
            public GameObject Glow;
            public FxLight GlowLight;
            public ParticleSystem Plume;
        }

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
        readonly Dictionary<int, ModuleFx> _modules = new Dictionary<int, ModuleFx>();
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
            _modules.Clear();
            var s = _station;

            foreach (var b in s.Buildings)
            {
                var root = new GameObject($"{s.Label(b)} #{b.Id}");
                root.transform.SetParent(transform, false);
                root.transform.position = Center(b, s.Width, s.Height);
                _objects.Add(root);
                bool powered = b.Operating;

                switch (b.Type)
                {
                    case BuildingType.Walkway:
                        int mask = s.LinkMask(b.Origin);
                        BuildTile(root, mask, "Walkway_Deck", WalkwayDeck, "Walkway_Railing", WalkwayRailing);
                        AddWalkwayLamp(root, b, mask, powered);
                        break;
                    case BuildingType.Foundation:
                        int foundationMask = s.LinkMask(b.Origin);
                        BuildTile(root, foundationMask, "Foundation", FoundationPlate, "Foundation_Edge", FoundationEdge);
                        AddFoundationLights(root, b, foundationMask, powered);
                        break;
                    default:
                        string key = powered ? b.Type.ToString() : b.Type + "_Dark";
                        if (b.Def.RequiresFoundation)
                        {
                            Spawn(root, b.Type.ToString(), key, () => ModulePlaceholder(b.Type, powered), Vector3.zero, Quaternion.identity);
                            AddModuleFx(root, b, powered);
                        }
                        else
                        {
                            Spawn(root, b.Type.ToString(), key, () => Placeholder(b.Type, powered), Vector3.zero, Quaternion.identity);
                        }
                        var padArt = root.GetComponentInChildren<PadArtLighting>();
                        if (padArt != null) padArt.SetPowered(powered);

                        if (powered) AddFixtures(root, b);
                        break;
                }

                if (b.Def.Serves.Length > 0) AddPadFx(root, b, powered);
                if (!b.Connected) AddEmergencyLamp(root, b);
            }
            UpdatePads();
        }

        void Update()
        {
            if (_station == null) return;
            UpdatePads();
            UpdateModules();
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
                    var southwest = FxLight.Flood(t, new Vector3(-(w / 2f - 0.25f), 1.0f, -(h / 2f - 0.25f)), Sodium, 1.5f, 3.4f, failing ? FxLightMode.Flicker : FxLightMode.Steady);
                    var northeast = FxLight.Flood(t, new Vector3(w / 2f - 0.25f, 1.0f, h / 2f - 0.25f), Sodium, 1.5f, 3.4f);
                    FxLight beacon = null;

                    if (b.Type == BuildingType.StarterPad)
                        beacon = FxLight.Create(t, new Vector3(1.05f, 1.75f, -1.05f), Alarm, 1.6f, 4f, FxLightMode.Strobe);
                    var art = root.GetComponentInChildren<PadArtLighting>();
                    if (art != null)
                    {
                        if (art.SouthwestSocket != null)
                        {
                            southwest.transform.position = art.SouthwestSocket.position;
                            southwest.ConfigureSpot(t.InverseTransformDirection(art.SouthwestSocket.forward));
                        }
                        if (art.NortheastSocket != null)
                        {
                            northeast.transform.position = art.NortheastSocket.position;
                            northeast.ConfigureSpot(t.InverseTransformDirection(art.NortheastSocket.forward));
                        }
                        if (beacon != null && art.BeaconSocket != null) beacon.transform.position = art.BeaconSocket.position;
                        art.BindLights(southwest, northeast, beacon);
                    }
                    break;

                case BuildingType.Hangar:
                    FxLight.Flood(t, new Vector3(-(w / 2f - 0.4f), 1.2f, h / 2f - 0.4f), WorkLight, 1.3f, 3.8f);
                    FxLight.Flood(t, new Vector3(w / 2f - 0.4f, 1.2f, h / 2f - 0.4f), WorkLight, 1.3f, 3.8f, failing ? FxLightMode.Flicker : FxLightMode.Steady);
                    FxLight.Create(t, new Vector3(0f, 1.1f, -h / 2f + 0.2f), Alarm, 0.7f, 2.2f, FxLightMode.Pulse);
                    break;

                default:
                    if (!b.Def.RequiresFoundation) break;
                    // A floodlight on one corner of every module, and a beacon on the tall ones.
                    FxLight.Flood(t, new Vector3(-(w / 2f - 0.15f), 0.9f, -(h / 2f - 0.15f)), Sodium, 1.1f, 2.6f, failing ? FxLightMode.Flicker : FxLightMode.Steady);
                    if (b.Type == BuildingType.CommsArray) FxLight.Create(t, new Vector3(-0.5f, 0.5f, 0.5f), Alarm, 1.2f, 2.8f, FxLightMode.Strobe);
                    if (b.Type == BuildingType.AtcTower)
                    {
                        FxLight.Create(t, new Vector3(0f, 1.9f, 0f), Glass, 1.4f, 3.5f);
                        FxLight.Create(t, new Vector3(0.6f, 0.45f, -0.6f), Charge, 0.8f, 1.6f, FxLightMode.Pulse);
                    }
                    if (b.Type == BuildingType.PilotLounge) FxLight.Create(t, new Vector3(0.5f, 1.0f, 0.4f), Neon, 0.9f, 2.4f, FxLightMode.Flicker);
                    if (b.Type == BuildingType.FuelTank) FxLight.Create(t, new Vector3(0f, 1.9f, 0f), Alarm, 0.8f, 1.8f, FxLightMode.Pulse);
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

                var status = !b.Operating ? PadStatus.Offline
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
            bool enclosed = mask == 0xF;
            int side = 0;
            if (!enclosed) while ((mask & (1 << side)) != 0) side++;
            var d = Cell.Directions[side];
            var rotation = enclosed ? Quaternion.identity : Quaternion.LookRotation(new Vector3(d.X, 0f, d.Y));
            string prefab = enclosed ? "Walkway_InsetLight" : "Walkway_Lamp";
            string key = prefab + (powered ? "" : "_Dark");
            var go = Spawn(root, prefab, key, () => enclosed ? InsetGuideLights(powered) : WalkwayLamp(powered), Vector3.zero, rotation);
            var fallback = enclosed ? new Vector3(0f, .14f, 0f) : rotation * new Vector3(.38f, .70f, .38f);
            var mode = (b.Origin.X + b.Origin.Y) % 11 == 0 ? FxLightMode.Flicker : FxLightMode.Steady;
            BindTileFixture(root, go, powered, true, fallback, Sodium, enclosed ? .35f : .65f, 1.8f, mode);
        }

        void AddFoundationLights(GameObject root, Building b, int mask, bool powered)
        {
            if (mask == 0xF)
            {
                var go = Spawn(root, "Foundation_InsetLight", "Foundation_InsetLight" + (powered ? "" : "_Dark"),
                    () => InsetGuideLights(powered), Vector3.zero, Quaternion.identity);
                // Recessed markers on every interior tile; alternating tiles supply the area light.
                BindTileFixture(root, go, powered, (b.Origin.X + b.Origin.Y) % 2 == 0,
                    new Vector3(0f, .14f, 0f), WorkLight, .3f, 1.6f);
                return;

            }
            bool areaLight = true;
            for (int side = 0; side < 4; side++)
            {
                if ((mask & (1 << side)) != 0) continue;
                var d = Cell.Directions[side];
                var rotation = Quaternion.LookRotation(new Vector3(d.X, 0f, d.Y));
                var go = Spawn(root, "Foundation_EdgeLight", "Foundation_EdgeLight" + (powered ? "" : "_Dark"),
                    () => FoundationGuideLight(powered), Vector3.zero, rotation);
                BindTileFixture(root, go, powered, areaLight, rotation * new Vector3(0f, .2f, .36f), WorkLight, .5f, 1.8f);
                areaLight = false;
            }
        }

        void BindTileFixture(GameObject root, GameObject go, bool powered, bool areaLight, Vector3 fallback,
            Color color, float intensity, float range, FxLightMode mode = FxLightMode.Steady)
        {
            var fixture = go.GetComponent<TileLightFixture>();
            FxLight lamp = null;
            if (powered && areaLight)
            {
                Vector3 position = fixture != null && fixture.Socket != null
                    ? root.transform.InverseTransformPoint(fixture.Socket.position) : fallback;
                lamp = FxLight.Flood(root.transform, position, color, intensity, range, mode);
            }
            if (fixture != null) fixture.Bind(powered, lamp);
        }

        void AddEmergencyLamp(GameObject root, Building b)
        {
            Spawn(root, "EmergencyLamp", "EmergencyLamp", EmergencyLamp, Vector3.zero, Quaternion.identity, prefabAllowed: false);
            FxLight.Create(root.transform, new Vector3(0f, 1.15f, 0f), Alarm, 1.4f, Mathf.Max(2.4f, b.Def.Width * 0.9f), FxLightMode.Flicker);
        }

        // ------------------------------------------------------------------ modules: moving parts and status

        void AddModuleFx(GameObject root, Building b, bool powered)
        {
            var t = root.transform;
            var fx = new ModuleFx();
            switch (b.Type)
            {
                case BuildingType.SolarArray:
                    fx.Wing = Spawn(root, "SolarArray_Wing", "SolarArray_Wing", SolarWing, new Vector3(0f, 0.68f, 0f), Quaternion.identity).transform;
                    break;

                case BuildingType.CommsArray:
                    fx.Dish = Spawn(root, "CommsArray_Dish", "CommsArray_Dish", Dish, new Vector3(0f, 1.25f, 0f), Quaternion.Euler(-30f, b.Id * 47f, 0f)).transform;
                    break;

                case BuildingType.AtcTower:
                    fx.Radar = Spawn(root, "AtcTower_Radar", "AtcTower_Radar", Radar, new Vector3(0f, 2.47f, 0f), Quaternion.identity).transform;
                    break;

                case BuildingType.BatteryBank:
                    fx.ChargeLights = new GameObject[4];
                    for (int i = 0; i < 4; i++)
                        fx.ChargeLights[i] = Spawn(root, "BatteryBank_Charge", "BatteryBank_Charge", ChargeLight, new Vector3(0.36f, 0.3f, -0.45f + i * 0.3f), Quaternion.identity, prefabAllowed: false);
                    break;

                case BuildingType.FuelCellGenerator:
                    if (!powered) break;
                    fx.GlowLight = FxLight.Create(t, new Vector3(0.45f, 0.35f, 0f), Sodium, 0f, 2.2f, FxLightMode.Pulse);
                    fx.Plume = Fx.Create(t, FxPreset.Vapor, new Vector3(0.25f, 0.95f, 0.75f), Vector3.up);
                    break;

                case BuildingType.IceRefinery:
                    if (!powered) break;
                    fx.Glow = Spawn(root, "IceRefinery_Glow", "IceRefinery_Glow", RefineryGlow, Vector3.zero, Quaternion.identity, prefabAllowed: false);
                    fx.GlowLight = FxLight.Create(t, new Vector3(0.45f, 0.9f, -0.75f), Sodium, 0f, 2.8f, FxLightMode.Flicker);
                    fx.Plume = Fx.Create(t, FxPreset.Vapor, new Vector3(0.45f, 1.65f, -0.2f), Vector3.up);
                    break;
            }
            _modules[b.Id] = fx;
        }

        void UpdateModules()
        {
            float dt = Time.deltaTime;
            var sun = SkyCycle.SunDirection;
            bool sunUp = SkyCycle.Daylight > 0.05f;
            bool systems = _station.SystemsOnline;
            int charge = Mathf.CeilToInt((float)_station.BatteryFraction * 4f - 0.01f);

            foreach (var kv in _modules)
            {
                var b = _station.GetBuilding(kv.Key);
                var fx = kv.Value;
                if (b == null) continue;

                if (fx.Wing != null)
                {
                    // Track the sun (tilting at most 60°); park flat in the shadow or when switched off.
                    var target = Quaternion.identity;
                    var flat = new Vector3(sun.x, 0f, sun.z);
                    if (sunUp && b.Operating && flat.sqrMagnitude > 1e-4f)
                    {
                        float tilt = Mathf.Min(60f, Vector3.Angle(Vector3.up, sun));
                        target = Quaternion.LookRotation(flat) * Quaternion.Euler(tilt, 0f, 0f);
                    }
                    fx.Wing.rotation = Quaternion.RotateTowards(fx.Wing.rotation, target, 25f * dt);
                }

                if (fx.Dish != null && b.Operating && systems)
                    fx.Dish.Rotate(0f, 9f * dt, 0f, Space.World);

                if (fx.Radar != null && b.Operating && systems)
                    fx.Radar.Rotate(0f, 120f * dt, 0f, Space.World);

                if (fx.ChargeLights != null)
                    for (int i = 0; i < fx.ChargeLights.Length; i++)
                    {
                        bool on = b.Connected && i < charge;
                        if (fx.ChargeLights[i].activeSelf != on) fx.ChargeLights[i].SetActive(on);
                    }

                if (fx.Glow != null && fx.Glow.activeSelf != b.Running) fx.Glow.SetActive(b.Running);
                if (fx.GlowLight != null) fx.GlowLight.Intensity = b.Running ? 1.6f : 0f;
                Fx.SetActive(fx.Plume, b.Running);
            }
        }

        // ------------------------------------------------------------------ module placeholders

        /// <summary>Modules stand on foundations: a thin skid plate at deck level, then the module itself.</summary>
        static Mesh ModulePlaceholder(BuildingType type, bool powered)
        {
            var b = new MeshBuilder();
            var def = BuildingCatalog.Get(type);
            float w = def.Width - 0.1f, h = def.Height - 0.1f;
            float glow = powered ? 1f : 0f;
            Color Lamp(Color c) => powered ? c : Dead;

            b.AddBox(new Vector3(0f, 0.03f, 0f), new Vector3(w - 0.1f, 0.04f, h - 0.1f), Deck);
            b.AddBox(new Vector3(-(w / 2f - 0.15f), 0.45f, -(h / 2f - 0.15f)), new Vector3(0.05f, 0.9f, 0.05f), Post);
            b.AddBox(new Vector3(-(w / 2f - 0.15f), 0.9f, -(h / 2f - 0.15f)), new Vector3(0.14f, 0.07f, 0.14f), Lamp(Sodium), 45f, glow);

            switch (type)
            {
                case BuildingType.SolarArray:
                    b.AddBox(new Vector3(0f, 0.3f, 0f), new Vector3(0.14f, 0.6f, 0.14f), Post);
                    b.AddBox(new Vector3(0f, 0.6f, 0f), new Vector3(0.22f, 0.12f, 0.22f), Hull);
                    b.AddBox(new Vector3(0.6f, 0.15f, 0.6f), new Vector3(0.3f, 0.22f, 0.24f), Foil);
                    b.AddBox(new Vector3(0.6f, 0.2f, 0.47f), new Vector3(0.1f, 0.05f, 0.02f), Lamp(Charge), 0f, glow);
                    break;

                case BuildingType.BatteryBank:
                    b.AddBox(new Vector3(0f, 0.3f, 0f), new Vector3(0.66f, 0.5f, 1.66f), Casing);
                    for (int i = 0; i < 6; i++)
                        b.AddBox(new Vector3(0f, 0.57f, -0.7f + i * 0.28f), new Vector3(0.6f, 0.05f, 0.06f), Post);
                    b.AddBox(new Vector3(-0.335f, 0.3f, 0f), new Vector3(0.02f, 0.08f, 1.5f), Stripe);
                    b.AddBox(new Vector3(0.2f, 0.62f, 0.7f), new Vector3(0.08f, 0.1f, 0.08f), Rust);
                    b.AddBox(new Vector3(-0.2f, 0.62f, 0.7f), new Vector3(0.08f, 0.1f, 0.08f), Alarm);
                    break;

                case BuildingType.FuelCellGenerator:
                    b.AddBox(new Vector3(0f, 0.08f, 0f), new Vector3(0.7f, 0.1f, 1.7f), Post);
                    foreach (float z in new[] { -0.55f, 0f, 0.55f })
                    {
                        b.AddPrism(new Vector3(-0.08f, 0.13f, z), 0.22f, 0.55f, 8, Hull);
                        b.AddPrism(new Vector3(-0.08f, 0.68f, z), 0.23f, 0.05f, 8, Foil);
                    }
                    b.AddPrism(new Vector3(0.25f, 0.13f, 0.75f), 0.06f, 0.8f, 6, Rust);
                    b.AddBox(new Vector3(0.33f, 0.35f, 0f), new Vector3(0.02f, 0.12f, 0.5f), Lamp(Sodium), 0f, glow * 0.6f);
                    break;

                case BuildingType.FuelTank:
                    foreach (var (sx, sz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                        b.AddBox(new Vector3(sx * 0.48f, 0.35f, sz * 0.48f), new Vector3(0.08f, 0.7f, 0.08f), Post);
                    b.AddSphere(new Vector3(0f, 1.05f, 0f), 0.78f, 6, 12, TankPaint);
                    b.AddPrism(new Vector3(0f, 0.98f, 0f), 0.8f, 0.12f, 12, Stripe);
                    b.AddPrism(new Vector3(0f, 1.78f, 0f), 0.08f, 0.12f, 6, Hull);
                    b.AddBox(new Vector3(0.55f, 0.12f, -0.55f), new Vector3(0.5f, 0.08f, 0.08f), Rust, 45f);
                    b.AddBox(new Vector3(0.75f, 0.2f, -0.75f), new Vector3(0.14f, 0.14f, 0.14f), Hull);
                    break;

                case BuildingType.IceRefinery:
                    // Ice hopper with chunks of comet ice, the cracking column, electrolysis tanks, radiator.
                    b.AddBox(new Vector3(-0.45f, 0.35f, -0.8f), new Vector3(0.8f, 0.66f, 0.9f), Hull);
                    for (int i = 0; i < 5; i++)
                        b.AddBox(new Vector3(-0.65f + (i % 3) * 0.2f, 0.72f, -1.0f + (i / 3) * 0.35f), Vector3.one * 0.17f, Ice, i * 23f);
                    b.AddPrism(new Vector3(0.45f, 0.05f, -0.2f), 0.32f, 1.6f, 8, Rust);
                    for (int i = 0; i < 3; i++)
                        b.AddPrism(new Vector3(0.45f, 0.35f + i * 0.5f, -0.2f), 0.34f, 0.06f, 8, Post);
                    b.AddPrism(new Vector3(-0.45f, 0.05f, 0.55f), 0.24f, 0.9f, 8, Hull);
                    b.AddPrism(new Vector3(0.45f, 0.05f, 0.95f), 0.22f, 0.75f, 8, Hull);
                    b.AddBox(new Vector3(0f, 0.75f, 0.2f), new Vector3(0.9f, 0.06f, 0.06f), Post);
                    b.AddBox(new Vector3(0.45f, 0.55f, 0.4f), new Vector3(0.06f, 0.06f, 0.7f), Post);
                    b.AddBox(new Vector3(-0.85f, 0.55f, 0.6f), new Vector3(0.04f, 0.9f, 1.1f), Radiator);
                    break;

                case BuildingType.PilotLounge:
                    b.AddBox(new Vector3(0f, 0.43f, 0f), new Vector3(1.6f, 0.8f, 1.4f), HabWhite);
                    b.AddBox(new Vector3(0f, 0.87f, 0f), new Vector3(1.5f, 0.08f, 1.3f), Post);
                    b.AddBox(new Vector3(0f, 0.52f, 0.705f), new Vector3(1.3f, 0.14f, 0.02f), Lamp(Window), 0f, glow * 0.8f);
                    b.AddBox(new Vector3(0f, 0.52f, -0.705f), new Vector3(0.9f, 0.14f, 0.02f), Lamp(Window), 0f, glow * 0.8f);
                    b.AddBox(new Vector3(0.805f, 0.52f, 0f), new Vector3(0.02f, 0.14f, 1.1f), Lamp(Window), 0f, glow * 0.8f);
                    b.AddBox(new Vector3(-0.805f, 0.52f, 0f), new Vector3(0.02f, 0.14f, 1.1f), Lamp(Window), 0f, glow * 0.8f);
                    b.AddBox(new Vector3(0.5f, 0.98f, 0.4f), new Vector3(0.42f, 0.14f, 0.04f), Lamp(Neon), 0f, glow);
                    b.AddBox(new Vector3(0.6f, 0.3f, -0.72f), new Vector3(0.35f, 0.5f, 0.06f), Stripe);
                    b.AddBox(new Vector3(-0.45f, 0.98f, -0.3f), new Vector3(0.4f, 0.16f, 0.3f), Rust);
                    b.AddBox(new Vector3(-0.6f, 1.2f, 0.4f), new Vector3(0.03f, 0.5f, 0.03f), Post);
                    break;

                case BuildingType.CommsArray:
                    foreach (var (sx, sz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                        b.AddBox(new Vector3(sx * 0.15f, 0.6f, sz * 0.15f), new Vector3(0.04f, 1.2f, 0.04f), Post);
                    for (int i = 0; i < 4; i++)
                    {
                        b.AddBox(new Vector3(0f, 0.2f + i * 0.3f, 0.15f), new Vector3(0.34f, 0.03f, 0.03f), Post);
                        b.AddBox(new Vector3(0.15f, 0.35f + i * 0.3f, 0f), new Vector3(0.03f, 0.03f, 0.34f), Post);
                    }
                    b.AddBox(new Vector3(0f, 1.2f, 0f), new Vector3(0.22f, 0.1f, 0.22f), Hull);
                    b.AddBox(new Vector3(-0.5f, 0.22f, 0.5f), new Vector3(0.6f, 0.4f, 0.6f), Hull);
                    b.AddBox(new Vector3(-0.5f, 0.22f, 0.5f), new Vector3(0.62f, 0.12f, 0.62f), Foil);
                    b.AddBox(new Vector3(-0.5f, 0.45f, 0.5f), new Vector3(0.1f, 0.08f, 0.1f), Lamp(Alarm), 45f, glow);
                    break;

                case BuildingType.AtcTower:
                    b.AddBox(new Vector3(0f, 0.22f, 0f), new Vector3(1.3f, 0.4f, 1.3f), Hull);
                    b.AddBox(new Vector3(0f, 0.3f, -0.655f), new Vector3(0.8f, 0.08f, 0.02f), Stripe);
                    b.AddPrism(new Vector3(0f, 0.42f, 0f), 0.28f, 1.3f, 8, HabWhite);
                    b.AddPrism(new Vector3(0f, 1.66f, 0f), 0.5f, 0.06f, 8, Post);
                    b.AddPrism(new Vector3(0f, 1.72f, 0f), 0.62f, 0.4f, 8, Lamp(Glass), 22.5f, glow * 0.7f);
                    b.AddPrism(new Vector3(0f, 2.12f, 0f), 0.68f, 0.08f, 8, Post, 22.5f);
                    b.AddPrism(new Vector3(0f, 2.2f, 0f), 0.5f, 0.06f, 8, Hull, 22.5f);
                    b.AddBox(new Vector3(0f, 2.33f, 0f), new Vector3(0.06f, 0.22f, 0.06f), Post);
                    b.AddBox(new Vector3(0.6f, 0.45f, -0.6f), new Vector3(0.1f, 0.1f, 0.1f), Lamp(Charge), 45f, glow);
                    break;
            }
            return b.ToMesh(type + (powered ? "" : "_Dark"));
        }

        /// <summary>Boom and two photovoltaic wings; the panels face +Y and the whole wing turns to the sun.</summary>
        static Mesh SolarWing()
        {
            var b = new MeshBuilder();
            b.AddBox(Vector3.zero, new Vector3(1.9f, 0.05f, 0.06f), Hull);
            foreach (float side in new[] { -0.5f, 0.5f })
            {
                b.AddBox(new Vector3(side, 0f, 0f), new Vector3(0.82f, 0.03f, 0.9f), PanelBlue);
                for (int i = -1; i <= 1; i++)
                    b.AddBox(new Vector3(side, 0.017f, i * 0.3f), new Vector3(0.8f, 0.004f, 0.015f), PanelCell);
                for (int i = -1; i <= 1; i++)
                    b.AddBox(new Vector3(side + i * 0.27f, 0.017f, 0f), new Vector3(0.015f, 0.004f, 0.88f), PanelCell);
                b.AddBox(new Vector3(side, 0f, 0.46f), new Vector3(0.84f, 0.035f, 0.03f), Foil);
                b.AddBox(new Vector3(side, 0f, -0.46f), new Vector3(0.84f, 0.035f, 0.03f), Foil);
            }
            return b.ToMesh("SolarArray_Wing");
        }

        /// <summary>A dish facing +Z with its feed horn.</summary>
        static Mesh Dish()
        {
            var b = new MeshBuilder();
            const int sides = 14;
            const float radius = 0.7f;
            b.AddSpike(Vector3.zero, new Vector3(0f, 0f, -0.28f), radius, sides, Hull);
            for (int i = 0; i < sides; i++)
            {
                float a0 = 2f * Mathf.PI * i / sides, a1 = 2f * Mathf.PI * (i + 1) / sides;
                b.AddTriangle(new Vector3(0f, 0f, -0.12f), new Vector3(Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius, 0f),
                    new Vector3(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, 0f), Vector3.forward, Radiator);
            }
            b.AddBox(new Vector3(0f, 0f, 0.3f), new Vector3(0.03f, 0.03f, 0.6f), Post);
            b.AddBox(new Vector3(0f, 0f, 0.62f), new Vector3(0.1f, 0.1f, 0.1f), Hull);
            b.AddBox(new Vector3(0f, -0.1f, -0.3f), new Vector3(0.12f, 0.2f, 0.12f), Post);
            return b.ToMesh("CommsArray_Dish");
        }

        static Mesh Radar()
        {
            var b = new MeshBuilder();
            b.AddBox(Vector3.zero, new Vector3(1.0f, 0.12f, 0.04f), Radiator);
            b.AddBox(new Vector3(0f, -0.02f, 0.05f), new Vector3(0.9f, 0.05f, 0.05f), Post);
            b.AddBox(new Vector3(0f, -0.08f, 0f), new Vector3(0.1f, 0.06f, 0.1f), Hull);
            return b.ToMesh("AtcTower_Radar");
        }

        static Mesh ChargeLight()
        {
            var b = new MeshBuilder();
            b.AddBox(Vector3.zero, new Vector3(0.02f, 0.12f, 0.22f), Charge, 0f, 1f);
            return b.ToMesh("BatteryBank_Charge");
        }

        /// <summary>Furnace windows on the cracking column, lit while the refinery runs.</summary>
        static Mesh RefineryGlow()
        {
            var b = new MeshBuilder();
            foreach (float y in new[] { 0.6f, 1.1f })
            {
                b.AddBox(new Vector3(0.45f, y, -0.53f), new Vector3(0.16f, 0.22f, 0.02f), Sodium, 0f, 1f);
                b.AddBox(new Vector3(0.12f, y, -0.2f), new Vector3(0.02f, 0.22f, 0.16f), Sodium, 0f, 1f);
            }
            return b.ToMesh("IceRefinery_Glow");
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

        GameObject Spawn(GameObject parent, string prefabKey, string meshKey, System.Func<Mesh> placeholder, Vector3 position, Quaternion rotation, bool prefabAllowed = true)
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
            return go;
        }

        GameObject LoadPrefab(string key)
        {
            if (!_prefabs.TryGetValue(key, out var prefab))
                _prefabs[key] = prefab = Resources.Load<GameObject>($"Buildings/{key}");
            return prefab;
        }

        // ------------------------------------------------------------------ pad and hangar placeholders

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

        static Mesh InsetGuideLights(bool powered)
        {
            var b = new MeshBuilder();
            foreach (float x in new[] { -.34f, .34f })
            {
                b.AddBox(new Vector3(x, .015f, 0f), new Vector3(.09f, .03f, .36f), Post);
                b.AddBox(new Vector3(x, .033f, 0f), new Vector3(.045f, .006f, .28f), powered ? Sodium : Dead, 0f, powered ? 1f : 0f);
            }
            return b.ToMesh("InsetGuideLights");
        }

        static Mesh FoundationGuideLight(bool powered)
        {
            var b = new MeshBuilder();
            b.AddBox(new Vector3(0f, .052f, .45f), new Vector3(.7f, .075f, .09f), Post);
            b.AddBox(new Vector3(0f, .093f, .45f), new Vector3(.60f, .01f, .044f), powered ? WorkLight : Dead, 0f, powered ? 1f : 0f);
            return b.ToMesh("FoundationGuideLight");
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
