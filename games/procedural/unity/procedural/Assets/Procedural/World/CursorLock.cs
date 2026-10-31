using UnityEngine;
using UnityEngine.InputSystem;

namespace Procedural.World
{
    /// <summary>
    /// Shooter-style cursor: locked and hidden while playing, so the mouse always
    /// steers the camera. Esc frees it; a left click takes it back.
    /// </summary>
    static class CursorLock
    {
        /// <summary>Applies Esc and left click; true while the mouse steers the camera.</summary>
        public static bool Update(Keyboard keyboard, Mouse mouse)
        {
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                Release();
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                Lock();
            return Cursor.lockState == CursorLockMode.Locked;
        }

        public static void Lock()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public static void Release()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
