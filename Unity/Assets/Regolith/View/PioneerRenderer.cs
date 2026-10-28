using System.Collections.Generic;
using System.Linq;
using Regolith.Sim;
using UnityEngine;

namespace Regolith.View
{
    /// <summary>
    /// Pioneers as little figures on the map. Each walks to where they are assigned (their module,
    /// or their colony's Hab Core) and moves around there. Movement is presentation only;
    /// the sim just knows the assignment.
    ///
    /// Art drop-in slot: <c>Resources/Pioneers/&lt;Expertise&gt;.prefab</c> (e.g. <c>Engineer.prefab</c>) or
    /// <c>Resources/Pioneers/Pioneer.prefab</c> for all; pivot at the feet, about 0.25 units tall.
    /// Until then each is a coloured placeholder figure.
    /// </summary>
    public sealed class PioneerRenderer : MonoBehaviour
    {
        const float WalkSpeed = 1.2f;
        const float WanderSpeed = 0.25f;
        const float WanderRadius = 0.4f;
        const float ArriveDistance = 0.6f;

        sealed class Walker
        {
            public GameObject GameObject;
            public Vector3 Position;
            public Vector3 WanderTarget;
            public float IdleUntil;
        }

        static readonly Dictionary<Expertise, Color> Colors = new Dictionary<Expertise, Color>
        {
            { Expertise.Engineer, new Color(1f, 0.6f, 0.2f) },
            { Expertise.Scientist, new Color(0.72f, 0.5f, 1f) },
            { Expertise.Explorer, new Color(0.4f, 0.9f, 0.4f) },
            { Expertise.Administrator, new Color(0.35f, 0.62f, 1f) },
        };

        readonly Dictionary<int, Walker> _walkers = new Dictionary<int, Walker>();
        readonly Dictionary<Expertise, Mesh> _placeholderMeshes = new Dictionary<Expertise, Mesh>();
        Material _material;
        GameState _state;

        public static Color ColorOf(Expertise e) => Colors[e];

        void Awake()
        {
            _material = HexMeshBuilder.CreateMaterial();
            var builder = new HexMeshBuilder();
            foreach (var kv in Colors)
            {
                builder.Clear();
                builder.AddPrism(Vector3.zero, 0f, 0.17f, 0.06f, kv.Value);                 // suit
                builder.AddPrism(Vector3.zero, 0.17f, 0.25f, 0.045f, new Color(0.95f, 0.95f, 0.95f)); // helmet
                var mesh = new Mesh { name = $"Pioneer {kv.Key}" };
                builder.ApplyTo(mesh);
                _placeholderMeshes[kv.Key] = mesh;
            }
        }

        public void Bind(GameState state)
        {
            foreach (var w in _walkers.Values) Destroy(w.GameObject);
            _walkers.Clear();
            _state = state;
        }

        public bool TryGetPosition(int pioneerId, out Vector3 position)
        {
            if (_walkers.TryGetValue(pioneerId, out var w))
            {
                position = w.Position;
                return true;
            }
            position = default;
            return false;
        }

        /// <summary>The tile a Pioneer belongs on: their module, or their colony's Hab Core.</summary>
        public static HexCoord? AnchorOf(GameState s, Pioneer p)
        {
            if (p.Assignment.Kind == AssignmentKind.Module) return p.Assignment.Module;

            var colony = s.GetColony(p.Assignment.Kind == AssignmentKind.Administrator ? p.Assignment.ColonyId : p.HomeColonyId)
                         ?? s.Colonies.FirstOrDefault(c => c.FactionId == p.FactionId);
            return colony?.Center;
        }

        void Update()
        {
            if (_state == null) return;

            var alive = new HashSet<int>();
            foreach (var p in _state.Pioneers.Where(p => p.IsAlive))
            {
                var anchor = AnchorOf(_state, p);
                if (anchor == null) continue;
                alive.Add(p.Id);

                var home = HexMapRenderer.ToWorld(anchor.Value);
                if (!_walkers.TryGetValue(p.Id, out var w))
                {
                    w = CreateWalker(p, home);
                    _walkers[p.Id] = w;
                }
                Step(w, home);
            }

            foreach (var id in _walkers.Keys.Where(id => !alive.Contains(id)).ToList())
            {
                Destroy(_walkers[id].GameObject);
                _walkers.Remove(id);
            }
        }

        Walker CreateWalker(Pioneer p, Vector3 home)
        {
            var prefab = Resources.Load<GameObject>($"Pioneers/{p.Expertise}") ?? Resources.Load<GameObject>("Pioneers/Pioneer");
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, transform);
            }
            else
            {
                go = new GameObject();
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = _placeholderMeshes[p.Expertise];
                go.AddComponent<MeshRenderer>().sharedMaterial = _material;
            }
            go.name = $"Pioneer {p.Name}";

            var start = home + RandomOffset();
            return new Walker { GameObject = go, Position = start, WanderTarget = start };
        }

        void Step(Walker w, Vector3 home)
        {
            var flatToHome = new Vector3(home.x - w.Position.x, 0f, home.z - w.Position.z);
            Vector3 target;
            float speed;

            if (flatToHome.magnitude > ArriveDistance)
            {
                // Reassigned: walk across the map to the new post.
                target = home;
                speed = WalkSpeed;
                w.WanderTarget = home + RandomOffset();
            }
            else
            {
                // At their post: potter about between work spots, pausing now and then.
                if (Time.time < w.IdleUntil) { Place(w, w.Position); return; }
                target = w.WanderTarget;
                speed = WanderSpeed;
                if (new Vector3(target.x - w.Position.x, 0f, target.z - w.Position.z).magnitude < 0.03f)
                {
                    w.WanderTarget = home + RandomOffset();
                    w.IdleUntil = Time.time + Random.Range(0.5f, 3f);
                }
            }

            var step = new Vector3(target.x - w.Position.x, 0f, target.z - w.Position.z);
            var move = Vector3.ClampMagnitude(step, speed * Time.deltaTime);
            if (move.sqrMagnitude > 1e-8f) w.GameObject.transform.rotation = Quaternion.LookRotation(move.normalized, Vector3.up);
            Place(w, w.Position + move);
        }

        void Place(Walker w, Vector3 position)
        {
            var tile = _state.Map.GetTile(HexMapRenderer.FromWorld(position));
            position.y = tile != null ? HexMapRenderer.TopHeight(tile) : 0f;
            w.Position = position;
            w.GameObject.transform.position = position;
        }

        static Vector3 RandomOffset()
        {
            var r = Random.insideUnitCircle * WanderRadius;
            return new Vector3(r.x, 0f, r.y);
        }
    }
}
