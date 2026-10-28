using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Waystation.Sim;

namespace Waystation.View
{
    /// <summary>
    /// Ships flying in from deep space, circling in the holding pattern, settling onto their pad,
    /// and leaving again. Motion is presentation only: positions are derived from each ship's sim state.
    ///
    /// Art drop-in slots: Resources/Ships/Shuttle.prefab, Resources/Ships/Hauler.prefab
    /// (nose along +Z, pivot at the bottom of the landing gear, ~1–2 units long).
    /// </summary>
    public sealed class ShipRenderer : MonoBehaviour
    {
        const float PadHeight = 0.05f;

        sealed class Visual
        {
            public GameObject GameObject;
            public Vector3 Position;
            public Quaternion Rotation = Quaternion.identity;
        }

        static readonly (string primary, string secondary)[] Liveries =
        {
            ("#f2f2f2", "#ff8a3d"), ("#ffd23f", "#5b6474"), ("#2ec4b6", "#f2f2f2"),
            ("#e63946", "#1d1f2b"), ("#8e6cff", "#ffcf56"), ("#9be564", "#2b2d42"),
        };
        static readonly Color Glass = MeshBuilder.Hex("#2fd6ff");
        static readonly Color Engine = MeshBuilder.Hex("#2a2a35");
        static readonly Color Glow = MeshBuilder.Hex("#ffb347");

        readonly Dictionary<int, Visual> _visuals = new Dictionary<int, Visual>();
        readonly Dictionary<(ShipClass, int), Mesh> _meshes = new Dictionary<(ShipClass, int), Mesh>();
        readonly Dictionary<ShipClass, GameObject> _prefabs = new Dictionary<ShipClass, GameObject>();
        Material _material;
        Station _station;
        int _gridWidth, _gridHeight;

        void Awake()
        {
            _material = MeshBuilder.CreateMaterial();
        }

        public void Bind(Station station)
        {
            foreach (var v in _visuals.Values) Destroy(v.GameObject);
            _visuals.Clear();
            _station = station;
            _gridWidth = station.Width;
            _gridHeight = station.Height;
        }

        public bool TryGetPosition(int shipId, out Vector3 position)
        {
            position = default;
            if (!_visuals.TryGetValue(shipId, out var v)) return false;
            position = v.Position;
            return true;
        }

        void Update()
        {
            if (_station == null) return;
            float dt = Time.deltaTime;

            var holding = _station.Holding.Select(s => s.Id).ToList();
            var alive = new HashSet<int>();
            foreach (var ship in _station.Ships)
            {
                alive.Add(ship.Id);
                if (!_visuals.TryGetValue(ship.Id, out var v))
                {
                    v = Create(ship);
                    _visuals[ship.Id] = v;
                }

                int slot = holding.IndexOf(ship.Id);
                if (slot < 0) slot = ship.Id % Rules.MaxHolding;
                var target = Target(ship, slot);

                float k = ship.State == ShipState.Landing || ship.State == ShipState.Servicing ? 6f : 2.5f;
                var previous = v.Position;
                v.Position = Vector3.Lerp(v.Position, target, 1f - Mathf.Exp(-k * dt));

                // Face the direction of travel; hold heading while parked.
                var velocity = v.Position - previous;
                velocity.y = 0f;
                if (velocity.sqrMagnitude > 1e-6f && ship.State != ShipState.Servicing)
                    v.Rotation = Quaternion.Slerp(v.Rotation, Quaternion.LookRotation(velocity), 1f - Mathf.Exp(-5f * dt));

                v.GameObject.transform.SetPositionAndRotation(v.Position, v.Rotation);
            }

            foreach (var id in _visuals.Keys.Where(id => !alive.Contains(id)).ToList())
            {
                Destroy(_visuals[id].GameObject);
                _visuals.Remove(id);
            }
        }

        Vector3 Target(Ship ship, int slot)
        {
            float p = (float)ship.StateProgress;
            var pad = PadPosition(ship);

            switch (ship.State)
            {
                case ShipState.Approaching:
                    return Vector3.Lerp(EntryPoint(ship), HoldingPoint(slot), EaseOut(p));
                case ShipState.Holding:
                    return HoldingPoint(slot);
                case ShipState.Landing:
                    return pad + Vector3.up * (PadHeight + 4f * (1f - EaseOut(p)));
                case ShipState.Servicing:
                    return pad + Vector3.up * (PadHeight + 0.03f * Mathf.Sin(Time.time * 2f + ship.Id));
                case ShipState.Departing:
                {
                    var from = ship.BuildingId.HasValue ? pad : HoldingPoint(slot);
                    return from + Vector3.up * (PadHeight + 6f * p * p) + ExitDirection(ship) * (40f * p * p * p);
                }
                default:
                    return pad;
            }
        }

        Vector3 PadPosition(Ship ship)
        {
            var b = ship.BuildingId.HasValue ? _station.GetBuilding(ship.BuildingId.Value) : null;
            return b != null ? BuildingRenderer.Center(b, _gridWidth, _gridHeight) : Vector3.zero;
        }

        Vector3 HoldingPoint(int slot)
        {
            float radius = Mathf.Max(_gridWidth, _gridHeight) * 0.5f + 3f + (slot % 2) * 1.5f;
            float angle = Time.time * 0.12f + slot * (2f * Mathf.PI / Rules.MaxHolding);
            return new Vector3(Mathf.Cos(angle) * radius, 3.5f + (slot % 3) * 0.8f, Mathf.Sin(angle) * radius);
        }

        static Vector3 EntryPoint(Ship ship)
        {
            float a = ship.Id * 2.39996f; // golden angle: arrivals come from all around
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 60f + Vector3.up * 8f;
        }

        static Vector3 ExitDirection(Ship ship)
        {
            float a = ship.Id * 2.39996f + 2.2f;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        Visual Create(Ship ship)
        {
            if (!_prefabs.TryGetValue(ship.Class, out var prefab))
                _prefabs[ship.Class] = prefab = Resources.Load<GameObject>($"Ships/{ship.Class}");

            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, transform);
            }
            else
            {
                go = new GameObject();
                go.transform.SetParent(transform, false);
                var key = (ship.Class, ship.Livery % Liveries.Length);
                if (!_meshes.TryGetValue(key, out var mesh)) _meshes[key] = mesh = Placeholder(key.Item1, key.Item2);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = _material;
            }
            go.name = $"{ship.Class} {ship.Name}";

            var start = EntryPoint(ship);
            return new Visual { GameObject = go, Position = start, Rotation = Quaternion.LookRotation(-start.normalized) };
        }

        static Mesh Placeholder(ShipClass cls, int livery)
        {
            var main = MeshBuilder.Hex(Liveries[livery].primary);
            var trim = MeshBuilder.Hex(Liveries[livery].secondary);
            var b = new MeshBuilder();

            if (cls == ShipClass.Shuttle)
            {
                b.AddBox(new Vector3(0f, 0.22f, 0f), new Vector3(0.5f, 0.32f, 1.1f), main);
                b.AddSpike(new Vector3(0f, 0.22f, 0.55f), new Vector3(0f, 0.2f, 1.0f), 0.22f, 4, main, 45f);
                b.AddBox(new Vector3(0f, 0.42f, 0.22f), new Vector3(0.3f, 0.14f, 0.36f), Glass);
                b.AddBox(new Vector3(0f, 0.2f, -0.1f), new Vector3(1.5f, 0.06f, 0.45f), trim);
                b.AddBox(new Vector3(0.72f, 0.32f, -0.2f), new Vector3(0.06f, 0.3f, 0.3f), trim);
                b.AddBox(new Vector3(-0.72f, 0.32f, -0.2f), new Vector3(0.06f, 0.3f, 0.3f), trim);
                foreach (float x in new[] { -0.15f, 0.15f })
                {
                    b.AddBox(new Vector3(x, 0.22f, -0.62f), new Vector3(0.18f, 0.18f, 0.24f), Engine);
                    b.AddBox(new Vector3(x, 0.22f, -0.75f), new Vector3(0.13f, 0.13f, 0.04f), Glow);
                }
            }
            else
            {
                b.AddBox(new Vector3(0f, 0.42f, 0f), new Vector3(1.0f, 0.7f, 2.0f), main);
                b.AddBox(new Vector3(0f, 0.62f, 1.1f), new Vector3(0.6f, 0.36f, 0.4f), Glass);
                b.AddBox(new Vector3(0.78f, 0.36f, -0.1f), new Vector3(0.48f, 0.52f, 1.4f), trim);
                b.AddBox(new Vector3(-0.78f, 0.36f, -0.1f), new Vector3(0.48f, 0.52f, 1.4f), trim);
                b.AddBox(new Vector3(0f, 0.82f, -0.5f), new Vector3(0.12f, 0.35f, 0.7f), trim);
                foreach (float x in new[] { -0.3f, 0f, 0.3f })
                {
                    b.AddBox(new Vector3(x, 0.4f, -1.08f), new Vector3(0.24f, 0.24f, 0.18f), Engine);
                    b.AddBox(new Vector3(x, 0.4f, -1.19f), new Vector3(0.18f, 0.18f, 0.04f), Glow);
                }
            }
            return b.ToMesh($"{cls} {livery}");
        }
    }
}
