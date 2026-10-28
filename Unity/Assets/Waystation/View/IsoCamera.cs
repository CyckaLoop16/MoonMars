using UnityEngine;

namespace Waystation.View
{
    /// <summary>
    /// Orthographic isometric camera. WASD/arrows or right-drag to pan, wheel to zoom,
    /// Q/E to turn the view in 90° steps.
    /// </summary>
    public sealed class IsoCamera : MonoBehaviour
    {
        public float Pitch = 35f;
        public float PanSpeed = 14f;
        public float MinSize = 4f;
        public float MaxSize = 28f;
        public float RotateSmoothing = 10f;

        Camera _camera;
        Vector3 _pivot;
        Rect _bounds;
        float _targetYaw = 45f;
        float _yaw = 45f;
        float _size = 11f;
        Vector2 _lastMouse;

        public void Init(Camera cam, Vector3 pivot, Rect boundsXZ)
        {
            _camera = cam;
            _camera.orthographic = true;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 500f;
            _pivot = pivot;
            _bounds = boundsXZ;
            _lastMouse = InputShim.MousePosition();
            Apply();
        }

        /// <summary>Centre the view on a world position (zooming in a little if far out).</summary>
        public void FocusOn(Vector3 world)
        {
            _pivot = new Vector3(world.x, 0f, world.z);
            _size = Mathf.Min(_size, 9f);
            if (_camera != null) Apply();
        }

        void LateUpdate()
        {
            if (_camera == null) return;
            float dt = Time.unscaledDeltaTime;

            int turn = InputShim.RotatePressed();
            if (turn != 0) _targetYaw += 90f * turn;
            _yaw = Mathf.Lerp(_yaw, _targetYaw, 1f - Mathf.Exp(-RotateSmoothing * dt));

            var flatYaw = Quaternion.Euler(0f, _yaw, 0f);
            Vector2 pan = InputShim.PanAxis();
            _pivot += flatYaw * new Vector3(pan.x, 0f, pan.y) * (PanSpeed * (_size / 11f) * dt);

            Vector2 mouse = InputShim.MousePosition();
            if (InputShim.DragHeld())
            {
                Vector2 delta = mouse - _lastMouse;
                float worldPerPixel = 2f * _size / Screen.height;
                _pivot -= flatYaw * new Vector3(delta.x, 0f, delta.y / Mathf.Sin(Pitch * Mathf.Deg2Rad)) * worldPerPixel;
            }
            _lastMouse = mouse;

            float scroll = InputShim.Scroll();
            if (scroll != 0f) _size = Mathf.Clamp(_size * (scroll > 0f ? 0.88f : 1.12f), MinSize, MaxSize);

            _pivot.x = Mathf.Clamp(_pivot.x, _bounds.xMin, _bounds.xMax);
            _pivot.z = Mathf.Clamp(_pivot.z, _bounds.yMin, _bounds.yMax);
            Apply();
        }

        void Apply()
        {
            var rotation = Quaternion.Euler(Pitch, _yaw, 0f);
            _camera.transform.SetPositionAndRotation(_pivot - rotation * Vector3.forward * 100f, rotation);
            _camera.orthographicSize = _size;
        }
    }
}
