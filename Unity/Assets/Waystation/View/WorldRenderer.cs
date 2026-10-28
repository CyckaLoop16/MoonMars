using System.Collections.Generic;
using UnityEngine;

namespace Waystation.View
{
    /// <summary>
    /// The space around the station, grim-dark: a cold void, a dying red giant, a dark planet with only
    /// a thin lit crescent, rust-brown nebula haze, dim stars, distant wreck silhouettes, a faint build
    /// grid, and tumbling rocks and scrap that catch the station's lamps.
    /// Placeholder art; the backdrop, wrecks and debris are obvious candidates for GPT replacements.
    /// </summary>
    public sealed class WorldRenderer : MonoBehaviour
    {
        public static readonly Color SpaceColor = MeshBuilder.Hex("#040506");

        static readonly Color GridLine = MeshBuilder.Hex("#141c22");
        static readonly Color GridCorner = MeshBuilder.Hex("#5a1e18");
        static readonly Color[] StarColors = { MeshBuilder.Hex("#9a9a9a"), MeshBuilder.Hex("#7f8a90"), MeshBuilder.Hex("#a08878"), MeshBuilder.Hex("#c8c8c8") };
        static readonly Color[] NebulaColors = { MeshBuilder.Hex("#140808"), MeshBuilder.Hex("#1a0c0a"), MeshBuilder.Hex("#0b1012"), MeshBuilder.Hex("#160a0e") };
        static readonly Color StarCore = MeshBuilder.Hex("#ff6a2a");
        static readonly Color[] StarHalo = { MeshBuilder.Hex("#a5301a"), MeshBuilder.Hex("#5e1a10"), MeshBuilder.Hex("#2a0c08"), MeshBuilder.Hex("#120604") };
        static readonly Color PlanetDark = MeshBuilder.Hex("#0d0e10");
        static readonly Color PlanetRim = MeshBuilder.Hex("#8a3a22");
        static readonly Color PlanetRimFaint = MeshBuilder.Hex("#3a1a12");
        static readonly Color Ring = MeshBuilder.Hex("#2a2420");
        static readonly Color Wreck = MeshBuilder.Hex("#0a0b0c");
        static readonly Color RockA = MeshBuilder.Hex("#3a3633");
        static readonly Color RockB = MeshBuilder.Hex("#2e2a28");
        static readonly Color Scrap = MeshBuilder.Hex("#4a4d52");
        static readonly Color ScrapRust = MeshBuilder.Hex("#5a2a16");

        /// <summary>The backdrop is laid out for this orthographic size, then scaled with the zoom.</summary>
        const float ReferenceSize = 11f;
        /// <summary>Backdrop depths in front of the camera (its far plane is 500).</summary>
        const float WreckDepth = 360f, PlanetDepth = 380f, SunDepth = 390f, StarDepth = 400f, NebulaDepth = 420f;

        readonly List<(Transform transform, Vector3 spin)> _debris = new List<(Transform, Vector3)>();
        Camera _camera;
        Transform _backdrop;

        public void Build(int gridWidth, int gridHeight, int seed, Camera camera)
        {
            foreach (Transform child in transform) Destroy(child.gameObject);
            if (_backdrop != null) Destroy(_backdrop.gameObject);
            _debris.Clear();
            _camera = camera;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = MeshBuilder.ToRenderSpace(SpaceColor);

            var unlit = MeshBuilder.CreateMaterial(lit: false);
            var lit = MeshBuilder.CreateMaterial(lit: true);
            var rng = new System.Random(seed * 31 + 7);

            // The far backdrop rides along with the camera, like a skybox.
            var backdrop = new MeshBuilder();
            BuildNebulae(backdrop, rng);
            BuildStars(backdrop, rng);
            BuildDyingStar(backdrop);
            BuildPlanet(backdrop);
            BuildWrecks(backdrop, rng);
            _backdrop = AddChild("Backdrop", backdrop.ToMesh("Backdrop"), unlit).transform;
            _backdrop.SetParent(_camera.transform, false);

            var grid = new MeshBuilder();
            BuildGrid(grid, gridWidth, gridHeight);
            AddChild("BuildGrid", grid.ToMesh("BuildGrid"), unlit);

            BuildDebris(rng, gridWidth, gridHeight, lit);
        }

        /// <summary>World position of a build-grid cell's centre (the grid is centred on the origin).</summary>
        public static Vector3 CellCenter(int x, int y, int gridWidth, int gridHeight) =>
            new Vector3(x + 0.5f - gridWidth / 2f, 0f, y + 0.5f - gridHeight / 2f);

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var (t, spin) in _debris) t.Rotate(spin * dt, Space.Self);
        }

        void LateUpdate()
        {
            if (_backdrop == null || _camera == null) return;
            float k = _camera.orthographicSize / ReferenceSize;
            _backdrop.localScale = new Vector3(k, k, 1f);
        }

        GameObject AddChild(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Barely-there grid lines with rust-red corner brackets: where you're allowed to build.</summary>
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

            foreach (var (cx, cz, sx, sz) in new[] { (-hw, -hh, 1f, 1f), (hw, -hh, -1f, 1f), (-hw, hh, 1f, -1f), (hw, hh, -1f, -1f) })
            {
                b.AddBox(new Vector3(cx + sx * 1f, 0f, cz), new Vector3(2f, 0.05f, 0.1f), GridCorner);
                b.AddBox(new Vector3(cx, 0f, cz + sz * 1f), new Vector3(0.1f, 0.05f, 2f), GridCorner);
            }
        }

        // The backdrop is built in camera space: X right, Y up, Z away from the camera.
        // Faces point back at the camera (-Z). It spans a 21:9 screen at the reference size with margin.

        static void Flat(MeshBuilder b, Vector3 a, Vector3 c, Vector3 d, Color color) =>
            b.AddTriangle(a, c, d, Vector3.back, color, false);

        static void Disc(MeshBuilder b, Vector3 center, float radius, Color color, int segments = 36, float squash = 1f)
        {
            for (int j = 0; j < segments; j++)
            {
                float a0 = 2f * Mathf.PI * j / segments, a1 = 2f * Mathf.PI * (j + 1) / segments;
                Flat(b, center, center + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0) * squash, 0f) * radius,
                    center + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1) * squash, 0f) * radius, color);
            }
        }

        static void BuildStars(MeshBuilder b, System.Random rng)
        {
            for (int i = 0; i < 220; i++)
            {
                var p = new Vector3(Range(rng, -30f, 30f), Range(rng, -14f, 14f), StarDepth);
                float r = Range(rng, 0.02f, 0.06f);
                var color = StarColors[rng.Next(StarColors.Length)];
                Flat(b, p + new Vector3(-r, 0f, 0f), p + new Vector3(0f, r, 0f), p + new Vector3(r, 0f, 0f), color);
                Flat(b, p + new Vector3(-r, 0f, 0f), p + new Vector3(r, 0f, 0f), p + new Vector3(0f, -r, 0f), color);
            }
        }

        static void BuildNebulae(MeshBuilder b, System.Random rng)
        {
            for (int i = 0; i < 10; i++)
            {
                var center = new Vector3(Range(rng, -28f, 28f), Range(rng, -12f, 12f), NebulaDepth + i);
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

        /// <summary>A bloated red giant near the end of its life, upper left: a hot core inside fading halos.</summary>
        static void BuildDyingStar(MeshBuilder b)
        {
            var center = new Vector3(-17f, 8.5f, SunDepth);
            float[] radii = { 9f, 6.5f, 4.6f, 3.3f };
            for (int i = 0; i < radii.Length; i++)
                Disc(b, center + Vector3.back * (i * 0.2f), radii[i], StarHalo[StarHalo.Length - 1 - i], 40);
            Disc(b, center + Vector3.back * 1f, 2.3f, StarCore, 40);
            // Dark sunspots.
            Disc(b, center + new Vector3(0.6f, -0.4f, -1.1f), 0.45f, StarHalo[0], 12);
            Disc(b, center + new Vector3(-0.8f, 0.7f, -1.1f), 0.3f, StarHalo[0], 12);
        }

        /// <summary>A dark world lower right, lit only along the thin edge facing the dying star.</summary>
        static void BuildPlanet(MeshBuilder b)
        {
            var center = new Vector3(15f, -9f, PlanetDepth);
            const float radius = 7.5f;

            AddRing(b, center, radius, back: true);
            // Crescent: a faint and a bright rim disc offset toward the star, then the dark body on top.
            var towardStar = new Vector3(-0.85f, 0.55f, 0f);
            Disc(b, center + towardStar * 0.32f + Vector3.forward * 0.05f, radius, PlanetRimFaint, 48);
            Disc(b, center + towardStar * 0.16f, radius, PlanetRim, 48);
            Disc(b, center + Vector3.back * 0.1f, radius, PlanetDark, 48);
            AddRing(b, center - Vector3.forward * 0.2f, radius, back: false);
        }

        static void AddRing(MeshBuilder b, Vector3 center, float radius, bool back)
        {
            const int segments = 48;
            for (int j = 0; j < segments; j++)
            {
                float a0 = 2f * Mathf.PI * j / segments, a1 = 2f * Mathf.PI * (j + 1) / segments;
                bool upper = Mathf.Sin((a0 + a1) * 0.5f) > 0f;
                if (upper != back) continue;

                Vector3 R(float r, float a) => center + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.22f - Mathf.Cos(a) * r * 0.12f, 0f);
                var color = j % 3 == 0 ? Color.Lerp(Ring, PlanetRim, 0.25f) : Ring;
                Flat(b, R(radius * 1.3f, a0), R(radius * 1.3f, a1), R(radius * 1.9f, a1), color);
                Flat(b, R(radius * 1.3f, a0), R(radius * 1.9f, a1), R(radius * 1.9f, a0), color);
            }
        }

        /// <summary>Silhouettes of dead ships and hulks, black against the haze.</summary>
        static void BuildWrecks(MeshBuilder b, System.Random rng)
        {
            for (int i = 0; i < 5; i++)
            {
                var c = new Vector3(Range(rng, -26f, 26f), Range(rng, -11f, 11f), WreckDepth + i);
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

        /// <summary>Rocks and wreckage drifting around and below the build area; they catch the station's lamps.</summary>
        void BuildDebris(System.Random rng, int w, int h, Material material)
        {
            var b = new MeshBuilder();
            for (int i = 0; i < 34; i++)
            {
                float angle = Range(rng, 0f, 2f * Mathf.PI);
                float dist = Mathf.Max(w, h) * 0.5f + Range(rng, 2f, 26f);
                var p = new Vector3(Mathf.Cos(angle) * dist, Range(rng, -12f, -1.5f), Mathf.Sin(angle) * dist);

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
                _debris.Add((go.transform, new Vector3(Range(rng, -10f, 10f), Range(rng, -10f, 10f), Range(rng, -10f, 10f))));
            }
        }

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
