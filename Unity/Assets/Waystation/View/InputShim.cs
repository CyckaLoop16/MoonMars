using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Waystation.View
{
    /// <summary>
    /// Thin wrapper so the prototype runs with either the Input System package or the legacy
    /// Input Manager, whichever "Active Input Handling" is set to. Replace with Input Actions
    /// once controls are rebindable.
    /// </summary>
    public static class InputShim
    {
        /// <summary>WASD / arrows. x = right, y = forward (screen-relative).</summary>
        public static Vector2 PanAxis()
        {
            float x = 0f, y = 0f;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return Vector2.zero;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) y -= 1f;
            if (k.wKey.isPressed || k.upArrowKey.isPressed) y += 1f;
#else
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
#endif
            return new Vector2(x, y);
        }

        /// <summary>-1, 0 or +1 (scroll up = zoom in).</summary>
        public static float Scroll()
        {
#if ENABLE_INPUT_SYSTEM
            float s = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
            float s = Input.mouseScrollDelta.y;
#endif
            return s > 0f ? 1f : s < 0f ? -1f : 0f;
        }

        public static Vector2 MousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        /// <summary>Right or middle mouse button: drag to pan.</summary>
        public static bool DragHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m != null && (m.rightButton.isPressed || m.middleButton.isPressed);
#else
            return Input.GetMouseButton(1) || Input.GetMouseButton(2);
#endif
        }

        /// <summary>Left click this frame: select a tile.</summary>
        public static bool SelectPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        /// <summary>Left button held (drag-placing corridors).</summary>
        public static bool SelectHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
            return Input.GetMouseButton(0);
#endif
        }

        /// <summary>Q / E this frame: -1 or +1 (turn the camera 90°), else 0.</summary>
        public static int RotatePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return 0;
            if (k.qKey.wasPressedThisFrame) return -1;
            if (k.eKey.wasPressedThisFrame) return 1;
#else
            if (Input.GetKeyDown(KeyCode.Q)) return -1;
            if (Input.GetKeyDown(KeyCode.E)) return 1;
#endif
            return 0;
        }

        public static bool PausePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }

        /// <summary>Number key 1–3 this frame (game speed), or 0.</summary>
        public static int SpeedPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return 0;
            if (k.digit1Key.wasPressedThisFrame) return 1;
            if (k.digit2Key.wasPressedThisFrame) return 2;
            if (k.digit3Key.wasPressedThisFrame) return 3;
#else
            if (Input.GetKeyDown(KeyCode.Alpha1)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha2)) return 2;
            if (Input.GetKeyDown(KeyCode.Alpha3)) return 3;
#endif
            return 0;
        }

        public static bool CancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        /// <summary>Hotkeys: P pad, H hangar, C walkway (catwalk), F foundation, X demolish, T clear next hail. Returns the letter, or '\0'.</summary>
        public static char ToolPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return '\0';
            if (k.pKey.wasPressedThisFrame) return 'P';
            if (k.hKey.wasPressedThisFrame) return 'H';
            if (k.cKey.wasPressedThisFrame) return 'C';
            if (k.xKey.wasPressedThisFrame) return 'X';
            if (k.fKey.wasPressedThisFrame) return 'F';
            if (k.tKey.wasPressedThisFrame) return 'T';
#else
            if (Input.GetKeyDown(KeyCode.P)) return 'P';
            if (Input.GetKeyDown(KeyCode.H)) return 'H';
            if (Input.GetKeyDown(KeyCode.C)) return 'C';
            if (Input.GetKeyDown(KeyCode.X)) return 'X';
            if (Input.GetKeyDown(KeyCode.F)) return 'F';
            if (Input.GetKeyDown(KeyCode.T)) return 'T';
#endif
            return '\0';
        }

        public static bool RegeneratePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }
    }
}
