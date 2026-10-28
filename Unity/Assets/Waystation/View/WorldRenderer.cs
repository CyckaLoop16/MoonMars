using System.Collections.Generic;
using UnityEngine;

namespace Waystation.View
{
    /// <summary>
    /// The space around the station: a holographic build grid, stars, nebula clouds, a ringed
    /// planet far below and slowly tumbling asteroids, in a bright No Man's Sky-style palette.
    /// Placeholder art; the planet, asteroids and backdrop are obvious candidates for GPT replacements.
    /// </summary>
    public sealed class WorldRenderer : MonoBehaviour
    {
        public static readonly Color SpaceColor = MeshBuilder.Hex("#160f33");

        static readonly Color GridLine = MeshBuilder.Hex("#2b5d86");
        static readonly Color GridCorner = MeshBuilder.Hex("#55d6ff");
        static readonly Color[] StarColors = { MeshBuilder.Hex("#ffffff"), MeshBuilder.Hex("#bfefff"), MeshBuilder.Hex("#ffd6f3"), MeshBuilder.Hex("#fff1b8") };
        static readonly Color[] NebulaColors = { MeshBuilder.Hex("#2a1a5c"), MeshBuilder.Hex("#3b1d63"), MeshBuilder.Hex("#13345e"), MeshBuilder.Hex("#4a1f5e") };
        static readonly Color PlanetLow = MeshBuilder.Hex("#f29a5c");
        static readonly Color PlanetMid = MeshBuilder.Hex("#f6c27a");
        static readonly Color PlanetHigh = MeshBuilder.Hex("#3fc7b4");
        static readonly Color Ring = MeshBuilder.Hex("#d9a8ff");
        static readonly Color AsteroidA = MeshBuilder.Hex("#6b5a8a");
        static readonly Color AsteroidB = MeshBuilder.Hex("#8a6f6a");

        /// <summary>The backdrop is laid out for this orthographic size, then scaled with the zoom.</summary>
        const float ReferenceSize = 11f;
        /// <summary>Backdrop depths in front of the camera (its far plane is 500).</summary>
        const float PlanetDepth = 380f, StarDepth = 400f, NebulaDepth = 420f;

        readonly List<(Transform transform, Vector3 spin)> _asteroids = new List<(Transform, Vector3)>();
        Camera _camera;
        Transform _backdrop;

        public void Build(int gridWidth, int gridHeight, int seed, Camera camera)
        {
            foreach (Transform child in transform) Destroy(child.gameObject);
            if (_backdrop != null) Destroy(_backdrop.gameObject);
            _asteroids.Clear();
            _camera = camera;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = SpaceColor;

            var material = MeshBuilder.CreateMaterial();
            var rng = new System.Random(seed * 31 + 7);

            // Stars, nebulae and the planet ride along with the camera, like a skybox.
            var backdrop = new MeshBuilder();
            BuildNebulae(backdrop, rng);
            BuildStars(backdrop, rng);
            BuildPlanet(backdrop);
            _backdrop = AddChild("Backdrop", backdrop.ToMesh("Backdrop"), material).transform;
            _backdrop.SetParent(_camera.transform, false);

            var grid = new MeshBuilder();
            BuildGrid(grid, gridWidth, gridHeight);
            AddChild("BuildGrid", grid.ToMesh("BuildGrid"), material);

            BuildAsteroids(rng, gridWidth, gridHeight, material);
        }

        /// <summary>World position of a build-grid cell's centre (the grid is centred on the origin).</summary>
        public static Vector3 CellCenter(int x, int y, int gridWidth, int gridHeight) =>
            new Vector3(x + 0.5f - gridWidth / 2f, 0f, y + 0.5f - gridHeight / 2f);

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var (t, spin) in _asteroids) t.Rotate(spin * dt, Space.Self);
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

        /// <summary>Faint holographic lines with brighter corner brackets: where you're allowed to build.</summary>
        static void BuildGrid(MeshBuilder b, int w, int h)
        {
            const float y = -0.02f, thin = 0.025f;
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
                b.AddBox(new Vector3(cx + sx * 1f, 0f, cz), new Vector3(2f, 0.06f, 0.12f), GridCorner);
                b.AddBox(new Vector3(cx, 0f, cz + sz * 1f), new Vector3(0.12f, 0.06f, 2f), GridCorner);
            }
        }

        // The backdrop is built in camera space: X right, Y up, Z away from the camera.
        // Faces point back at the camera (-Z). It spans a 21:9 screen at the reference size with margin.

        static void Flat(MeshBuilder b, Vector3 a, Vector3 c, Vector3 d, Color color) =>
            b.AddTriangle(a, c, d, Vector3.back, color, false);

        static void BuildStars(MeshBuilder b, System.Random rng)
        {
            for (int i = 0; i < 420; i++)
            {
                var p = new Vector3(Range(rng, -30f, 30f), Range(rng, -14f, 14f), StarDepth);
                float r = Range(rng, 0.03f, 0.09f);
                var color = StarColors[rng.Next(StarColors.Length)];
                Flat(b, p + new Vector3(-r, 0f, 0f), p + new Vector3(0f, r, 0f), p + new Vector3(r, 0f, 0f), color);
                Flat(b, p + new Vector3(-r, 0f, 0f), p + new Vector3(r, 0f, 0f), p + new Vector3(0f, -r, 0f), color);
            }
        }

        static void BuildNebulae(MeshBuilder b, System.Random rng)
        {
            for (int i = 0; i < 9; i++)
            {
                var center = new Vector3(Range(rng, -28f, 28f), Range(rng, -12f, 12f), NebulaDepth + i);
                float radius = Range(rng, 5f, 11f);
                var color = NebulaColors[rng.Next(NebulaColors.Length)];
                const int sides = 9;
                for (int k = 0; k < sides; k++)
                {
                    float a0 = 2f * Mathf.PI * k / sides, a1 = 2f * Mathf.PI * (k + 1) / sides;
                    float r0 = radius * Range(rng, 0.65f, 1.1f), r1 = radius * Range(rng, 0.65f, 1.1f);
                    Flat(b, center, center + new Vector3(Mathf.Cos(a0) * r0, Mathf.Sin(a0) * r0 * 0.6f, 0f),
                        center + new Vector3(Mathf.Cos(a1) * r1, Mathf.Sin(a1) * r1 * 0.6f, 0f), color);
                }
            }
        }

        /// <summary>A banded gas giant with a ring, low in one corner of the view.</summary>
        static void BuildPlanet(MeshBuilder b)
        {
            var center = new Vector3(15f, -9f, PlanetDepth);
            const float radius = 7.5f;
            const int segments = 40;

            // Ring behind the planet (upper half of the ellipse), then the disc, then the ring in front.
            AddRing(b, center, radius, back: true);
            for (int j = 0; j < segments; j++)
            {
                float a0 = 2f * Mathf.PI * j / segments, a1 = 2f * Mathf.PI * (j + 1) / segments;
                var p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * radius;
                var p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * radius;
                // Bands by height; the lit side (upper left) is brighter.
                float band = (p0.y + p1.y) * 0.5f / radius;
                var color = band > 0.45f ? PlanetHigh : band > -0.1f ? PlanetMid : PlanetLow;
                float light = 0.75f + 0.25f * Vector3.Dot(((p0 + p1) * 0.5f).normalized, new Vector3(-0.6f, 0.8f, 0f));
                Flat(b, center - Vector3.forward * 0.1f, center + p0, center + p1, color * light);
            }
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
                var color = j % 2 == 0 ? Ring : Color.Lerp(Ring, PlanetMid, 0.35f);
                Flat(b, R(radius * 1.3f, a0), R(radius * 1.3f, a1), R(radius * 1.9f, a1), color);
                Flat(b, R(radius * 1.3f, a0), R(radius * 1.9f, a1), R(radius * 1.9f, a0), color);
            }
        }

        void BuildAsteroids(System.Random rng, int w, int h, Material material)
        {
            var b = new MeshBuilder();
            for (int i = 0; i < 28; i++)
            {
                // Scattered around (and below) the build area, never inside it.
                float angle = Range(rng, 0f, 2f * Mathf.PI);
                float dist = Mathf.Max(w, h) * 0.5f + Range(rng, 4f, 30f);
                var p = new Vector3(Mathf.Cos(angle) * dist, Range(rng, -14f, -3f), Mathf.Sin(angle) * dist);

                b.Clear();
                float size = Range(rng, 0.6f, 2.4f);
                var color = rng.Next(2) == 0 ? AsteroidA : AsteroidB;
                b.AddBox(Vector3.zero, new Vector3(size, size * 0.8f, size * 0.9f), color, Range(rng, 0f, 90f));
                b.AddBox(new Vector3(size * 0.3f, size * 0.2f, 0f), new Vector3(size * 0.7f, size * 0.6f, size * 0.7f), color, Range(rng, 0f, 90f));

                var go = AddChild($"Asteroid {i}", b.ToMesh("Asteroid"), material);
                go.transform.position = p;
                go.transform.rotation = Random.rotationUniform;
                _asteroids.Add((go.transform, new Vector3(Range(rng, -12f, 12f), Range(rng, -12f, 12f), Range(rng, -12f, 12f))));
            }
        }

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
