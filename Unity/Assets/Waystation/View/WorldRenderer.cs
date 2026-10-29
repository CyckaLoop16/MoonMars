using System.Collections.Generic;
using UnityEngine;

namespace Waystation.View
{
    /// <summary>
    /// The space around the station, in low orbit: a huge planet curving across the bottom of the view
    /// (day side, night-side lights, a thin atmosphere), the sun crossing the sky and setting behind the
    /// planet's limb with a sunrise/sunset flare, a moon whose crescent follows the sun, slowly wheeling
    /// stars, nebula haze, wreck silhouettes, distant traffic, drifting rocks and scrap, and a build grid
    /// overlay (with corner brackets) that the player toggles.
    ///
    /// The sky is driven by <see cref="UpdateSky"/> from the orbit (see SkyCycle). Everything here is
    /// placeholder art: the backdrop pieces are obvious candidates for GPT replacements.
    /// </summary>
    public sealed class WorldRenderer : MonoBehaviour
    {
        public static readonly Color SpaceColor = MeshBuilder.Hex("#030406");

        static readonly Color GridLine = MeshBuilder.Hex("#16222a");
        static readonly Color GridCorner = MeshBuilder.Hex("#5a1e18");
        static readonly Color[] StarColors = { MeshBuilder.Hex("#a8adb5"), MeshBuilder.Hex("#8fa4c0"), MeshBuilder.Hex("#c8b8a0"), MeshBuilder.Hex("#e4e8f0") };
        static readonly Color[] NebulaColors = { MeshBuilder.Hex("#0d0f24"), MeshBuilder.Hex("#081a1f"), MeshBuilder.Hex("#1a0b1c"), MeshBuilder.Hex("#170b09") };
        static readonly Color PlanetNight = MeshBuilder.Hex("#06080b");
        static readonly Color Ocean = MeshBuilder.Hex("#1a3446");
        static readonly Color[] Land = { MeshBuilder.Hex("#6b4a32"), MeshBuilder.Hex("#55503a"), MeshBuilder.Hex("#7a5a3c"), MeshBuilder.Hex("#3f4a36") };
        static readonly Color Cloud = MeshBuilder.Hex("#c8cdd2");
        static readonly Color CityLight = MeshBuilder.Hex("#ff9a3c");
        static readonly Color Atmosphere = MeshBuilder.Hex("#4aa0ff");
        static readonly Color Flare = MeshBuilder.Hex("#ff7a2a");
        static readonly Color SunCore = MeshBuilder.Hex("#fff6e4");
        static readonly Color[] SunHalo = { MeshBuilder.Hex("#ffd9a0"), MeshBuilder.Hex("#ff9a50"), MeshBuilder.Hex("#7a3a18") };
        static readonly Color MoonLit = MeshBuilder.Hex("#9d988f");
        static readonly Color Wreck = MeshBuilder.Hex("#090a0c");
        static readonly Color RockA = MeshBuilder.Hex("#3a3633");
        static readonly Color RockB = MeshBuilder.Hex("#2e2a28");
        static readonly Color Scrap = MeshBuilder.Hex("#4a4d52");
        static readonly Color ScrapRust = MeshBuilder.Hex("#5a2a16");
        static readonly Color[] NavLights = { MeshBuilder.Hex("#ffffff"), MeshBuilder.Hex("#ff4a4a"), MeshBuilder.Hex("#5aff8a"), MeshBuilder.Hex("#ffb84a") };

        /// <summary>The backdrop is laid out for this orthographic size, then scaled with the zoom.</summary>
        const float ReferenceSize = 11f;
        /// <summary>
        /// Render queue for the fading sky layers: after the station's opaque geometry (so the station
        /// hides them) but before the transparent queue, so engine plumes and sparks draw on top.
        /// </summary>
        const int SkyQueue = 2600;
        /// <summary>Backdrop depths in front of the camera (its far plane is 500).</summary>
        const float TrafficDepth = 355f, WreckDepth = 360f, PlanetDepth = 380f, MoonDepth = 386f, SunDepth = 392f, StarDepth = 400f, NebulaDepth = 420f;

        // The planet: a huge disc whose top edge (the limb) arcs across the lower part of the view.
        static readonly Vector2 PlanetCenter = new Vector2(0f, -66f);
        const float PlanetRadius = 60f;
        // The sun's path across the sky: an ellipse, setting behind the limb on the right.
        static readonly Vector2 SunPathCenter = new Vector2(0f, 0.5f);
        const float SunPathX = 17f, SunPathY = 9.5f;
        static readonly Vector2 MoonPosition = new Vector2(-20f, 7f);
        const float MoonRadius = 1.2f;

        sealed class Traveller
        {
            public Transform Transform;
            public Vector3 Start;
            public Vector3 Velocity;
            public float Blink;
            public float Age;
            public float Life;
        }

        readonly List<(Transform transform, Vector3 spin)> _debris = new List<(Transform, Vector3)>();
        readonly List<Traveller> _traffic = new List<Traveller>();
        Camera _camera;
        System.Random _trafficRng = new System.Random(1);
        Transform _backdrop, _stars, _sun, _flare, _moonShadow;
        Material _sunMaterial, _dayMaterial, _nightMaterial, _atmosphereMaterial, _flareMaterial;
        GameObject _grid;
        float _sunsetAngle;

        /// <summary>Where the sun is in the sky (backdrop space) and how high above the limb, for lighting.</summary>
        public Vector2 SunPosition { get; private set; }

        public void Build(int gridWidth, int gridHeight, int seed, Camera camera)
        {
            foreach (Transform child in transform) Destroy(child.gameObject);
            if (_backdrop != null) Destroy(_backdrop.gameObject);
            _debris.Clear();
            _traffic.Clear();
            _camera = camera;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = MeshBuilder.ToRenderSpace(SpaceColor);

            var unlit = MeshBuilder.CreateMaterial(lit: false);
            var lit = MeshBuilder.CreateMaterial(lit: true);
            var rng = new System.Random(seed * 31 + 7);
            _sunsetAngle = FindSunsetAngle();

            // The far backdrop rides along with the camera, like a skybox.
            var backdrop = new MeshBuilder();
            BuildNebulae(backdrop, rng);
            BuildWrecks(backdrop, rng);
            Disc(backdrop, new Vector3(PlanetCenter.x, PlanetCenter.y, PlanetDepth), PlanetRadius, PlanetNight, 160);
            _backdrop = new GameObject("Backdrop").transform;
            _backdrop.SetParent(_camera.transform, false);
            AddLayer("Sky", backdrop, unlit);

            var stars = new MeshBuilder();
            BuildStars(stars, rng);
            _stars = AddLayer("Stars", stars, unlit).transform;

            _sunMaterial = FadeMaterial(additive: true, SkyQueue);
            _sun = AddLayer("Sun", BuildSun(), _sunMaterial).transform;

            var moon = new MeshBuilder();
            Disc(moon, Vector3.zero, MoonRadius, MoonLit, 32);
            var moonGo = AddLayer("Moon", moon, unlit);
            moonGo.transform.localPosition = new Vector3(MoonPosition.x, MoonPosition.y, MoonDepth);
            var shadow = new MeshBuilder();
            Disc(shadow, Vector3.zero, MoonRadius * 1.02f, SpaceColor, 32);
            var shadowGo = new GameObject("Moon Shadow");
            shadowGo.transform.SetParent(moonGo.transform, false);
            shadowGo.AddComponent<MeshFilter>().sharedMesh = shadow.ToMesh("Moon Shadow");
            shadowGo.AddComponent<MeshRenderer>().sharedMaterial = unlit;
            _moonShadow = shadowGo.transform;

            _dayMaterial = FadeMaterial(additive: false, SkyQueue + 1);
            AddLayer("Planet Day", BuildPlanetDay(rng), _dayMaterial);
            _nightMaterial = FadeMaterial(additive: true, SkyQueue + 2);
            AddLayer("Planet Lights", BuildCityLights(rng), _nightMaterial);
            _atmosphereMaterial = FadeMaterial(additive: true, SkyQueue + 3);
            AddLayer("Atmosphere", BuildAtmosphere(), _atmosphereMaterial);
            _flareMaterial = FadeMaterial(additive: true, SkyQueue + 4);
            _flare = AddLayer("Limb Flare", BuildFlare(), _flareMaterial).transform;

            BuildTraffic(rng);

            var grid = new MeshBuilder();
            BuildGrid(grid, gridWidth, gridHeight);
            BuildCorners(grid, gridWidth, gridHeight);
            _grid = AddChild("BuildGrid", grid.ToMesh("BuildGrid"), unlit);
            _grid.SetActive(false);

            BuildDebris(rng, gridWidth, gridHeight, lit);
            Fx.Create(transform, FxPreset.Dust, new Vector3(0f, 3f, 0f), Vector3.up, startActive: true);

            UpdateSky(Waystation.Sim.Orbit.StartPhase, 1f);
        }

        /// <summary>World position of a build-grid cell's centre (the grid is centred on the origin).</summary>
        public static Vector3 CellCenter(int x, int y, int gridWidth, int gridHeight) =>
            new Vector3(x + 0.5f - gridWidth / 2f, 0f, y + 0.5f - gridHeight / 2f);

        /// <summary>The build grid is an overlay the player toggles (G).</summary>
        public void ShowGrid(bool visible)
        {
            if (_grid != null && _grid.activeSelf != visible) _grid.SetActive(visible);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var (t, spin) in _debris) t.Rotate(spin * dt, Space.Self);
            UpdateTraffic(dt);
        }

        void LateUpdate()
        {
            if (_backdrop == null || _camera == null) return;
            float k = _camera.orthographicSize / ReferenceSize;
            _backdrop.localScale = new Vector3(k, k, 1f);
        }

        // ------------------------------------------------------------------ the orbit

        /// <summary>
        /// Moves the sun along its path for this point in the orbit (0 = sunrise) and fades the planet's
        /// day side, night lights, atmosphere and the limb flare to match the sunlight reaching the station.
        /// </summary>
        public void UpdateSky(double phase, float exposure)
        {
            if (_sun == null) return;

            // Day: rise on the left, arc over the top, set behind the limb on the right.
            // Night: carry on round underneath, hidden behind the planet.
            float dayEnd = (float)(1 - Waystation.Sim.Orbit.ShadowFraction);
            float p = (float)phase;
            float angle = p < dayEnd
                ? Mathf.Lerp(180f + _sunsetAngle, -_sunsetAngle, p / dayEnd)
                : Mathf.Lerp(-_sunsetAngle, -180f + _sunsetAngle, (p - dayEnd) / (1f - dayEnd));
            var sun = SunPathPoint(angle);
            SunPosition = sun;
            _sun.localPosition = new Vector3(sun.x, sun.y, SunDepth);
            _sunMaterial.SetColor("_Tint", Color.white);

            // Stars wheel slowly as the station goes round.
            _stars.localRotation = Quaternion.Euler(0f, 0f, -p * 360f * 0.25f);

            // Moon crescent faces the sun.
            var away = (MoonPosition - sun).normalized;
            _moonShadow.localPosition = new Vector3(away.x, away.y, 0f) * MoonRadius * 0.75f + new Vector3(0f, 0f, -0.2f);

            float night = 1f - exposure;
            _dayMaterial.SetColor("_Tint", new Color(1f, 1f, 1f, 0.08f + 0.92f * exposure));
            _nightMaterial.SetColor("_Tint", Color.white * (night * night * 1.6f));
            _atmosphereMaterial.SetColor("_Tint", Color.white * (0.25f + 0.75f * exposure));

            // Sunrise/sunset flare: a burst of colour along the limb while the sun is near it.
            float height = Vector2.Distance(sun, PlanetCenter) - PlanetRadius;
            float flare = Mathf.Exp(-(height * height) / 4f);
            var limb = PlanetCenter + (sun - PlanetCenter).normalized * PlanetRadius;
            _flare.localPosition = new Vector3(limb.x, limb.y, PlanetDepth - 0.8f);
            _flare.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(sun.y - PlanetCenter.y, sun.x - PlanetCenter.x) * Mathf.Rad2Deg - 90f);
            _flareMaterial.SetColor("_Tint", Color.white * (flare * 2.2f));
        }

        static Vector2 SunPathPoint(float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return SunPathCenter + new Vector2(Mathf.Cos(a) * SunPathX, Mathf.Sin(a) * SunPathY);
        }

        /// <summary>The angle below the horizontal (degrees) at which the sun's path meets the planet's limb.</summary>
        static float FindSunsetAngle()
        {
            float lo = 0f, hi = 90f; // 0: above the limb, 90: well behind the planet
            for (int i = 0; i < 30; i++)
            {
                float mid = (lo + hi) / 2f;
                bool behind = Vector2.Distance(SunPathPoint(-mid), PlanetCenter) < PlanetRadius;
                if (behind) hi = mid;
                else lo = mid;
            }
            return lo;
        }

        // ------------------------------------------------------------------ building the sky

        GameObject AddLayer(string name, MeshBuilder mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_backdrop, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh.ToMesh(name);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        static Material FadeMaterial(bool additive, int queue)
        {
            var shader = Shader.Find("Waystation/Backdrop Fade");
            if (shader == null)
            {
                Debug.LogError("Waystation: shader 'Waystation/Backdrop Fade' not found.");
                return MeshBuilder.CreateMaterial(lit: false);
            }
            var m = new Material(shader) { renderQueue = queue };
            m.SetFloat("_SrcBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            return m;
        }

        GameObject AddChild(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Barely-there grid lines: where you're allowed to build.</summary>
        static void BuildGrid(MeshBuilder b, int w, int h)
        {
            const float y = -0.02f, thin = 0.02f;
            float hw = w / 2f, hh = h / 2f;
            for (int x = 0; x <= w; x++)
            {
                float px = x - hw;
                b.AddQuad(new Vector3(px - thin, y, -hh), new Vector3(px - thin, y, hh), new Vector3(px + thin, y, hh), new Vector3(px + thin, y, -hh), Vector3.up, GridLine, false);
            }
            for (int z = 0; z <= h; z++)
            {
                float pz = z - hh;
                b.AddQuad(new Vector3(-hw, y, pz - thin), new Vector3(-hw, y, pz + thin), new Vector3(hw, y, pz + thin), new Vector3(hw, y, pz - thin), Vector3.up, GridLine, false);
            }
        }

        /// <summary>Rust-red corner brackets marking the station's build area.</summary>
        static void BuildCorners(MeshBuilder b, int w, int h)
        {
            float hw = w / 2f, hh = h / 2f;
            foreach (var (cx, cz, sx, sz) in new[] { (-hw, -hh, 1f, 1f), (hw, -hh, -1f, 1f), (-hw, hh, 1f, -1f), (hw, hh, -1f, -1f) })
            {
                b.AddBox(new Vector3(cx + sx * 1f, 0f, cz), new Vector3(2f, 0.05f, 0.1f), GridCorner);
                b.AddBox(new Vector3(cx, 0f, cz + sz * 1f), new Vector3(0.1f, 0.05f, 2f), GridCorner);
            }
        }

        // The backdrop is built in camera space: X right, Y up, Z away from the camera.
        // Faces point back at the camera (-Z). It spans a 21:9 screen at the reference size with margin.

        static void Flat(MeshBuilder b, Vector3 a, Vector3 c, Vector3 d, Color color, float alpha = 0f) =>
            b.AddTriangle(a, c, d, Vector3.back, color, false, alpha);

        static void Disc(MeshBuilder b, Vector3 center, float radius, Color color, int segments = 36, float squash = 1f, float alpha = 0f)
        {
            for (int j = 0; j < segments; j++)
            {
                float a0 = 2f * Mathf.PI * j / segments, a1 = 2f * Mathf.PI * (j + 1) / segments;
                Flat(b, center, center + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0) * squash, 0f) * radius,
                    center + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1) * squash, 0f) * radius, color, alpha);
            }
        }

        /// <summary>A disc of stars, so the field can turn without showing empty corners.</summary>
        static void BuildStars(MeshBuilder b, System.Random rng)
        {
            for (int i = 0; i < 420; i++)
            {
                float a = Range(rng, 0f, 2f * Mathf.PI), d = 34f * Mathf.Sqrt((float)rng.NextDouble());
                var p = new Vector3(Mathf.Cos(a) * d, Mathf.Sin(a) * d, StarDepth);
                float r = Range(rng, 0.02f, 0.07f) * (rng.Next(12) == 0 ? 1.8f : 1f);
                var color = StarColors[rng.Next(StarColors.Length)];
                Flat(b, p + new Vector3(-r, 0f, 0f), p + new Vector3(0f, r, 0f), p + new Vector3(r, 0f, 0f), color);
                Flat(b, p + new Vector3(-r, 0f, 0f), p + new Vector3(r, 0f, 0f), p + new Vector3(0f, -r, 0f), color);
            }
        }

        static void BuildNebulae(MeshBuilder b, System.Random rng)
        {
            for (int i = 0; i < 12; i++)
            {
                var center = new Vector3(Range(rng, -28f, 28f), Range(rng, -4f, 13f), NebulaDepth + i);
                float radius = Range(rng, 6f, 13f);
                var color = NebulaColors[rng.Next(NebulaColors.Length)];
                const int sides = 9;
                for (int k = 0; k < sides; k++)
                {
                    float a0 = 2f * Mathf.PI * k / sides, a1 = 2f * Mathf.PI * (k + 1) / sides;
                    float r0 = radius * Range(rng, 0.6f, 1.1f), r1 = radius * Range(rng, 0.6f, 1.1f);
                    Flat(b, center, center + new Vector3(Mathf.Cos(a0) * r0, Mathf.Sin(a0) * r0 * 0.55f, 0f),
                        center + new Vector3(Mathf.Cos(a1) * r1, Mathf.Sin(a1) * r1 * 0.55f, 0f), color);
                }
            }
        }

        /// <summary>A white-hot core in layered glare; additive, and pushed past 1.0 so bloom catches it.</summary>
        static MeshBuilder BuildSun()
        {
            var b = new MeshBuilder();
            float[] radii = { 9f, 4.2f, 1.9f };
            float[] strength = { 0.06f, 0.16f, 0.45f };
            for (int i = 0; i < radii.Length; i++)
                Disc(b, Vector3.back * (i * 0.1f), radii[i], SunHalo[SunHalo.Length - 1 - i] * strength[i], 40);
            Disc(b, Vector3.back * 0.4f, 0.85f, SunCore * 3f, 32);
            return b;
        }

        /// <summary>Oceans, continents and cloud bands on the planet's cap, squashed toward the limb.</summary>
        static MeshBuilder BuildPlanetDay(System.Random rng)
        {
            var b = new MeshBuilder();
            var center = new Vector3(PlanetCenter.x, PlanetCenter.y, PlanetDepth - 0.5f);
            Disc(b, center, PlanetRadius, Ocean, 160, 1f, 1f);

            for (int i = 0; i < 26; i++)
                Blob(b, rng, Land[rng.Next(Land.Length)], Range(rng, 2f, 7f), 0.32f, 1f, -0.1f);
            for (int i = 0; i < 30; i++)
                Blob(b, rng, Cloud, Range(rng, 1.5f, 6f), 0.12f, 0.55f, -0.2f);
            return b;
        }

        /// <summary>A rough, squashed polygon on the visible cap, clipped to the planet's disc.</summary>
        static void Blob(MeshBuilder b, System.Random rng, Color color, float radius, float squash, float alpha, float depthOffset)
        {
            float x = Range(rng, -34f, 34f);
            float limbY = PlanetCenter.y + Mathf.Sqrt(Mathf.Max(0f, PlanetRadius * PlanetRadius - x * x));
            var c = new Vector2(x, limbY - Range(rng, 0.3f, 7f));
            const int sides = 10;
            var points = new Vector3[sides];
            for (int k = 0; k < sides; k++)
            {
                float a = 2f * Mathf.PI * k / sides;
                float r = radius * Range(rng, 0.55f, 1.15f);
                var p = c + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * squash);
                var fromCenter = p - PlanetCenter;
                if (fromCenter.magnitude > PlanetRadius - 0.05f) p = PlanetCenter + fromCenter.normalized * (PlanetRadius - 0.05f);
                points[k] = new Vector3(p.x, p.y, PlanetDepth - 0.5f + depthOffset);
            }
            var mid = new Vector3(c.x, c.y, PlanetDepth - 0.5f + depthOffset);
            if ((c - PlanetCenter).magnitude > PlanetRadius - 0.05f) return;
            for (int k = 0; k < sides; k++) Flat(b, mid, points[k], points[(k + 1) % sides], color, alpha);
        }

        /// <summary>Clusters of orange lights on the night side: cities, foundries, things burning.</summary>
        static MeshBuilder BuildCityLights(System.Random rng)
        {
            var b = new MeshBuilder();
            for (int cluster = 0; cluster < 22; cluster++)
            {
                float x = Range(rng, -34f, 34f);
                float limbY = PlanetCenter.y + Mathf.Sqrt(Mathf.Max(0f, PlanetRadius * PlanetRadius - x * x));
                var c = new Vector2(x, limbY - Range(rng, 0.4f, 6f));
                int count = rng.Next(4, 12);
                for (int i = 0; i < count; i++)
                {
                    var p = c + new Vector2(Range(rng, -1.6f, 1.6f), Range(rng, -0.3f, 0.3f));
                    if ((p - PlanetCenter).magnitude > PlanetRadius - 0.1f) continue;
                    float r = Range(rng, 0.03f, 0.09f);
                    var pos = new Vector3(p.x, p.y, PlanetDepth - 0.6f);
                    var color = CityLight * Range(rng, 0.4f, 1f);
                    Flat(b, pos + new Vector3(-r, 0f, 0f), pos + new Vector3(0f, r * 0.6f, 0f), pos + new Vector3(r, 0f, 0f), color);
                    Flat(b, pos + new Vector3(-r, 0f, 0f), pos + new Vector3(r, 0f, 0f), pos + new Vector3(0f, -r * 0.6f, 0f), color);
                }
            }
            return b;
        }

        /// <summary>A thin glowing shell along the limb, brightest at the planet's edge.</summary>
        static MeshBuilder BuildAtmosphere()
        {
            var b = new MeshBuilder();
            float[] edges = { -0.25f, 0.05f, 0.3f, 0.6f, 1.0f };
            float[] strength = { 0.35f, 0.6f, 0.3f, 0.12f };
            const int segments = 120;
            for (int band = 0; band < strength.Length; band++)
            {
                float r0 = PlanetRadius + edges[band], r1 = PlanetRadius + edges[band + 1];
                var color = Atmosphere * strength[band];
                for (int j = 0; j < segments; j++)
                {
                    float a0 = Mathf.Lerp(35f, 145f, j / (float)segments) * Mathf.Deg2Rad;
                    float a1 = Mathf.Lerp(35f, 145f, (j + 1) / (float)segments) * Mathf.Deg2Rad;
                    Vector3 P(float r, float a) => new Vector3(PlanetCenter.x + Mathf.Cos(a) * r, PlanetCenter.y + Mathf.Sin(a) * r, PlanetDepth - 0.7f);
                    Flat(b, P(r0, a0), P(r0, a1), P(r1, a1), color);
                    Flat(b, P(r0, a0), P(r1, a1), P(r1, a0), color);
                }
            }
            return b;
        }

        /// <summary>Sunrise/sunset glow: a wide, flat burst lying along the limb (placed and faded in UpdateSky).</summary>
        static MeshBuilder BuildFlare()
        {
            var b = new MeshBuilder();
            Disc(b, Vector3.zero, 9f, Flare * 0.08f, 32, 0.12f);
            Disc(b, Vector3.back * 0.05f, 4.5f, Flare * 0.2f, 32, 0.16f);
            Disc(b, Vector3.back * 0.1f, 1.6f, MeshBuilder.Hex("#ffd2a0") * 0.5f, 24, 0.35f);
            return b;
        }

        /// <summary>Silhouettes of dead ships and hulks, black against the haze.</summary>
        static void BuildWrecks(MeshBuilder b, System.Random rng)
        {
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector3(Range(rng, -26f, 26f), Range(rng, -2f, 11f), WreckDepth + i);
                float len = Range(rng, 2f, 5f), hgt = len * Range(rng, 0.15f, 0.3f);
                float tilt = Range(rng, -0.4f, 0.4f);
                var right = new Vector3(Mathf.Cos(tilt), Mathf.Sin(tilt), 0f);
                var up = new Vector3(-right.y, right.x, 0f);
                // Hull, a broken spine and a jagged stub.
                Flat(b, c - right * len / 2 - up * hgt / 2, c + right * len / 2 - up * hgt / 2, c + right * len / 2 + up * hgt / 2, Wreck);
                Flat(b, c - right * len / 2 - up * hgt / 2, c + right * len / 2 + up * hgt / 2, c - right * len / 2 + up * hgt / 2, Wreck);
                Flat(b, c + right * len / 2, c + right * (len / 2 + len * 0.4f) + up * hgt * 0.8f, c + right * len / 2 + up * hgt * 0.3f, Wreck);
                Flat(b, c - right * len * 0.2f + up * hgt / 2, c - right * len * 0.05f + up * hgt * 1.4f, c + right * len * 0.05f + up * hgt / 2, Wreck);
            }
        }

        // ------------------------------------------------------------------ distant traffic

        /// <summary>Far-off ships crossing the sky: a blinking nav light and a faint engine streak.</summary>
        void BuildTraffic(System.Random rng)
        {
            var material = FadeMaterial(additive: true, SkyQueue + 5);
            material.SetColor("_Tint", Color.white);
            for (int i = 0; i < 6; i++)
            {
                var b = new MeshBuilder();
                var nav = NavLights[rng.Next(NavLights.Length)];
                Disc(b, Vector3.zero, 0.07f, nav * 1.5f, 8);
                Flat(b, new Vector3(0f, 0.025f, 0.01f), new Vector3(-1.4f, 0f, 0.01f), new Vector3(0f, -0.025f, 0.01f), MeshBuilder.Hex("#6aa8ff") * 0.35f);
                var go = AddLayer($"Traffic {i}", b, material);
                var t = new Traveller { Transform = go.transform, Blink = Range(rng, 0f, 3f) };
                Respawn(t, rng, Range(rng, 0f, 1f));
                _traffic.Add(t);
            }
        }

        static void Respawn(Traveller t, System.Random rng, float startAt)
        {
            bool rightward = rng.Next(2) == 0;
            float y = Range(rng, -1f, 12f);
            t.Start = new Vector3(rightward ? -32f : 32f, y, TrafficDepth + Range(rng, 0f, 4f));
            float speed = Range(rng, 0.6f, 1.8f);
            t.Velocity = new Vector3(rightward ? speed : -speed, Range(rng, -0.12f, 0.12f), 0f);
            t.Life = 64f / speed;
            t.Age = t.Life * startAt;
            t.Transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(t.Velocity.y, t.Velocity.x) * Mathf.Rad2Deg);
        }

        void UpdateTraffic(float dt)
        {
            foreach (var t in _traffic)
            {
                t.Age += dt;
                if (t.Age > t.Life) Respawn(t, _trafficRng, 0f);
                t.Transform.localPosition = t.Start + t.Velocity * t.Age;
                bool on = Mathf.Repeat(Time.time + t.Blink, 1.6f) < 0.25f;
                t.Transform.localScale = on ? Vector3.one : new Vector3(1f, 0.6f, 1f) * 0.5f;
            }
        }

        // ------------------------------------------------------------------ debris

        /// <summary>Rocks and wreckage drifting around and below the build area; they catch the sun and the station's lamps.</summary>
        void BuildDebris(System.Random rng, int w, int h, Material material)
        {
            var b = new MeshBuilder();
            for (int i = 0; i < 34; i++)
            {
                float angle = Range(rng, 0f, 2f * Mathf.PI);
                float dist = Range(rng, 30f, Mathf.Max(w, h) * 0.5f + 60f);
                var p = new Vector3(Mathf.Cos(angle) * dist, Range(rng, -30f, -4f), Mathf.Sin(angle) * dist);

                b.Clear();
                if (i % 3 == 0)
                {
                    // Scrap: a twisted girder and a hull plate.
                    b.AddBox(Vector3.zero, new Vector3(Range(rng, 1.2f, 2.6f), 0.12f, 0.16f), Scrap, Range(rng, 0f, 90f));
                    b.AddBox(new Vector3(0.2f, 0.1f, 0.1f), new Vector3(0.8f, 0.05f, 0.6f), ScrapRust, Range(rng, 0f, 90f));
                }
                else
                {
                    float size = Range(rng, 0.6f, 2.2f);
                    var color = rng.Next(2) == 0 ? RockA : RockB;
                    b.AddBox(Vector3.zero, new Vector3(size, size * 0.8f, size * 0.9f), color, Range(rng, 0f, 90f));
                    b.AddBox(new Vector3(size * 0.3f, size * 0.2f, 0f), new Vector3(size * 0.7f, size * 0.6f, size * 0.7f), color, Range(rng, 0f, 90f));
                }

                var go = AddChild($"Debris {i}", b.ToMesh("Debris"), material);
                go.transform.position = p;
                go.transform.rotation = Random.rotationUniform;
                go.transform.localScale = Vector3.one * Range(rng, 2f, 3.5f); // rocks and wreckage tens of metres across
                _debris.Add((go.transform, new Vector3(Range(rng, -10f, 10f), Range(rng, -10f, 10f), Range(rng, -10f, 10f))));
            }
        }

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
