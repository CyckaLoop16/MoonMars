using UnityEngine;

namespace Regolith.View
{
    /// <summary>
    /// Civ-style tilted strategy camera orbiting a ground pivot.
    /// WASD/arrows pan, Q/E rotate, wheel zooms, right/middle drag pans.
    /// </summary>
    public sealed class StrategyCamera : MonoBehaviour
    {
        public float PanSpeed = 25f;
        public float RotateSpeed = 90f;
        public float MinDistance = 6f;
        public float MaxDistance = 90f;
        public float ClosePitch = 45f;
        public float FarPitch = 70f;

        Camera _camera;
        Vector3 _pivot;
        Rect _bounds;
        float _yaw;
        float _distance = 35f;
        Vector2 _lastMouse;

        public void Init(Camera cam, Vector3 pivot, Rect boundsXZ)
        {
            _camera = cam;
            _pivot = pivot;
            _bounds = boundsXZ;
            _lastMouse = InputShim.MousePosition();
            Apply();
        }

        public float Distance => _distance;

        /// <summary>Glide-free jump: centre the view on a world position and zoom in.</summary>
        public void FocusOn(Vector3 position)
        {
            _pivot = new Vector3(position.x, 0f, position.z);
            _distance = Mathf.Min(_distance, 12f);
            if (_camera != null) Apply();
        }

        void LateUpdate()
        {
            if (_camera == null) return;

            float dt = Time.unscaledDeltaTime;
            var yawRotation = Quaternion.Euler(0f, _yaw, 0f);
            float zoomFactor = _distance / 35f;

            Vector2 pan = InputShim.PanAxis();
            _pivot += yawRotation * new Vector3(pan.x, 0f, pan.y) * (PanSpeed * zoomFactor * dt);

            Vector2 mouse = InputShim.MousePosition();
            if (InputShim.DragHeld())
            {
                Vector2 delta = mouse - _lastMouse;
                float worldPerPixel = _distance * 0.0022f;
                _pivot -= yawRotation * new Vector3(delta.x, 0f, delta.y) * worldPerPixel;
            }
            _lastMouse = mouse;

            _yaw += InputShim.RotateAxis() * RotateSpeed * dt;

            float scroll = InputShim.Scroll();
            if (scroll != 0f)
                _distance = Mathf.Clamp(_distance * (scroll > 0f ? 0.9f : 1.1f), MinDistance, MaxDistance);

            _pivot.x = Mathf.Clamp(_pivot.x, _bounds.xMin, _bounds.xMax);
            _pivot.z = Mathf.Clamp(_pivot.z, _bounds.yMin, _bounds.yMax);

            Apply();
        }

        void Apply()
        {
            float t = Mathf.InverseLerp(MinDistance, MaxDistance, _distance);
            var rotation = Quaternion.Euler(Mathf.Lerp(ClosePitch, FarPitch, t), _yaw, 0f);
            _camera.transform.SetPositionAndRotation(_pivot - rotation * Vector3.forward * _distance, rotation);
        }
    }
}
