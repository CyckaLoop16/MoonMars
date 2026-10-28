using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Regolith.View
{
    /// <summary>
    /// Thin wrapper so the prototype runs with either the Input System package or the legacy
    /// Input Manager, whichever "Active Input Handling" is set to. Replace with Input Actions
    /// once controls are rebindable.
    /// </summary>
    public static class InputShim
    {
        /// <summary>WASD / arrows. x = right, y = forward.</summary>
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

        /// <summary>Q / E. Positive = rotate clockwise.</summary>
        public static float RotateAxis()
        {
            float r = 0f;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return 0f;
            if (k.qKey.isPressed) r -= 1f;
            if (k.eKey.isPressed) r += 1f;
#else
            if (Input.GetKey(KeyCode.Q)) r -= 1f;
            if (Input.GetKey(KeyCode.E)) r += 1f;
#endif
            return r;
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

        /// <summary>Enter: end the turn.</summary>
        public static bool EndTurnPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
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
