using System.Collections.Generic;
using UnityEngine;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>
    /// Crew and passengers walking the station: out of their ship, along walkways and foundations to
    /// service buildings, waiting at the door when it's full, and back. Hidden while inside a building.
    /// Each role has its own look (USM marines in olive armour, Guild explorers in white suits, executives
    /// in black and gold…) with a glowing visor or collar light so they read in the eclipse.
    ///
    /// Art drop-in slots: Resources/People/&lt;ShipRole&gt;.prefab (e.g. Military.prefab), or
    /// Resources/People/Person.prefab for everyone. Pivot at the feet, facing +Z, ~0.35 units tall.
    /// </summary>
    public sealed class PeopleRenderer : MonoBehaviour
    {
        const float DeckHeight = 0.02f;
        const float Smoothing = 14f;

        static readonly Dictionary<ShipRole, (string body, string trim, string light)[]> Looks = new Dictionary<ShipRole, (string, string, string)[]>
        {
            [ShipRole.Traveller] = new[] { ("#7a5a3c", "#3b3b3b", "#ffcf8a"), ("#3d5f55", "#2d2f33", "#ffcf8a"), ("#6b4a62", "#3b3b3b", "#ffcf8a"), ("#8a7a52", "#4b5320", "#ffcf8a") },
            [ShipRole.Freighter] = new[] { ("#c46a1e", "#2d2f33", "#ffb020"), ("#a85a1a", "#3b3b3b", "#ffb020") },
            [ShipRole.Merchant] = new[] { ("#3f6b45", "#b8942a", "#7dff9a") },
            [ShipRole.Military] = new[] { ("#4a5238", "#2b2f22", "#5ab4ff") },
            [ShipRole.Explorer] = new[] { ("#d8d4c8", "#d0611f", "#ffd08a") },
            [ShipRole.Executive] = new[] { ("#1e1f24", "#b8902a", "#e8e4ff") },
        };

        readonly Dictionary<int, Transform> _people = new Dictionary<int, Transform>();
        readonly Dictionary<(ShipRole, int), Mesh> _meshes = new Dictionary<(ShipRole, int), Mesh>();
        readonly Dictionary<ShipRole, GameObject> _prefabs = new Dictionary<ShipRole, GameObject>();
        readonly HashSet<int> _seen = new HashSet<int>();
        readonly List<int> _gone = new List<int>();
        Material _material;
        Station _station;

        void Awake() => _material = MeshBuilder.CreateMaterial();

        public void Bind(Station station)
        {
            foreach (var t in _people.Values) if (t != null) Destroy(t.gameObject);
            _people.Clear();
            _station = station;
        }

        void LateUpdate()
        {
            if (_station == null) return;
            float dt = Time.deltaTime;
            float k = 1f - Mathf.Exp(-Smoothing * dt);
            _seen.Clear();

            foreach (var p in _station.People)
            {
                if (p.State == PersonState.Visiting) continue; // inside
                _seen.Add(p.Id);
                bool fresh = !_people.TryGetValue(p.Id, out var t);
                if (fresh) _people[p.Id] = t = Create(p);

                var target = WorldPosition(p);
                t.position = fresh ? target : Vector3.Lerp(t.position, target, k);

                var (dx, dy) = p.Heading;
                if (dx != 0 || dy != 0)
                    t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(new Vector3(dx, 0f, dy)), k);

                // A little bob while walking.
                bool walking = p.State == PersonState.Walking || p.State == PersonState.Returning;
                var body = t.GetChild(0);
                body.localPosition = walking ? new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.time * 11f + p.Id)) * 0.02f, 0f) : Vector3.zero;
            }

            _gone.Clear();
            foreach (var kv in _people) if (!_seen.Contains(kv.Key)) _gone.Add(kv.Key);
            foreach (var id in _gone)
            {
                if (_people[id] != null) Destroy(_people[id].gameObject);
                _people.Remove(id);
            }
        }

        /// <summary>Their spot in the world, spread out a little so a group doesn't stand in one place.</summary>
        Vector3 WorldPosition(Person p)
        {
            var (x, y) = p.Position;
            float spread = p.State == PersonState.Queueing ? 0.16f : 0.11f;
            float angle = (p.Slot * 2.4f + p.Id * 0.7f) % (2f * Mathf.PI);
            float radius = p.Slot == 0 && p.State != PersonState.Queueing ? 0f : spread * (1 + p.Slot % 2);
            return new Vector3((float)x - _station.Width / 2f + Mathf.Cos(angle) * radius, DeckHeight,
                (float)y - _station.Height / 2f + Mathf.Sin(angle) * radius);
        }

        Transform Create(Person p)
        {
            var root = new GameObject($"Person {p.Id} ({p.Role})").transform;
            root.SetParent(transform, false);

            if (!_prefabs.TryGetValue(p.Role, out var prefab))
            {
                prefab = Resources.Load<GameObject>($"People/{p.Role}");
                if (prefab == null) prefab = Resources.Load<GameObject>("People/Person");
                _prefabs[p.Role] = prefab;
            }

            GameObject body;
            if (prefab != null)
            {
                body = Instantiate(prefab, root);
            }
            else
            {
                body = new GameObject("Body");
                body.transform.SetParent(root, false);
                var looks = Looks[p.Role];
                var key = (p.Role, p.Id % looks.Length);
                if (!_meshes.TryGetValue(key, out var mesh)) _meshes[key] = mesh = Placeholder(looks[key.Item2]);
                body.AddComponent<MeshFilter>().sharedMesh = mesh;
                body.AddComponent<MeshRenderer>().sharedMaterial = _material;
            }
            body.transform.localPosition = Vector3.zero;
            return root;
        }

        /// <summary>A small blocky figure: boots, legs, torso with a pack, helmet with a glowing visor.</summary>
        static Mesh Placeholder((string body, string trim, string light) look)
        {
            var main = MeshBuilder.Hex(look.body);
            var trim = MeshBuilder.Hex(look.trim);
            var glow = MeshBuilder.Hex(look.light);
            var b = new MeshBuilder();
            b.AddBox(new Vector3(-0.03f, 0.065f, 0f), new Vector3(0.045f, 0.13f, 0.05f), trim);
            b.AddBox(new Vector3(0.03f, 0.065f, 0f), new Vector3(0.045f, 0.13f, 0.05f), trim);
            b.AddBox(new Vector3(0f, 0.19f, 0f), new Vector3(0.12f, 0.13f, 0.08f), main);
            b.AddBox(new Vector3(0f, 0.2f, -0.055f), new Vector3(0.09f, 0.1f, 0.04f), trim);
            b.AddBox(new Vector3(-0.075f, 0.18f, 0f), new Vector3(0.03f, 0.11f, 0.04f), main);
            b.AddBox(new Vector3(0.075f, 0.18f, 0f), new Vector3(0.03f, 0.11f, 0.04f), main);
            b.AddBox(new Vector3(0f, 0.3f, 0f), new Vector3(0.08f, 0.08f, 0.08f), main);
            b.AddBox(new Vector3(0f, 0.305f, 0.04f), new Vector3(0.06f, 0.025f, 0.01f), glow, 0f, 1f);
            return b.ToMesh("Person");
        }
    }
}
