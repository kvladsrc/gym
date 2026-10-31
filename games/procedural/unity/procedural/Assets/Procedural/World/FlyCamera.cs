using UnityEngine;
using UnityEngine.InputSystem;

namespace Procedural.World
{
    /// <summary>Free flight for looking at a level: WASD, Q/E down/up, mouse to look (Esc frees the cursor), Shift fast.</summary>
    public class FlyCamera : MonoBehaviour
    {
        public float speed = 30f;
        public float fastMultiplier = 5f;
        public float lookSensitivity = 0.15f;

        float yaw;
        float pitch;

        void OnEnable()
        {
            var euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            CursorLock.Lock();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null) return;

            bool look = CursorLock.Update(keyboard, mouse) || (mouse != null && mouse.rightButton.isPressed);
            if (look && mouse != null)
            {
                var delta = mouse.delta.ReadValue() * lookSensitivity;
                yaw += delta.x;
                pitch = Mathf.Clamp(pitch - delta.y, -89f, 89f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            var move = Vector3.zero;
            if (keyboard.wKey.isPressed) move += Vector3.forward;
            if (keyboard.sKey.isPressed) move += Vector3.back;
            if (keyboard.aKey.isPressed) move += Vector3.left;
            if (keyboard.dKey.isPressed) move += Vector3.right;
            if (keyboard.eKey.isPressed) move += Vector3.up;
            if (keyboard.qKey.isPressed) move += Vector3.down;
            float v = speed * (keyboard.leftShiftKey.isPressed ? fastMultiplier : 1f);
            transform.position += transform.TransformDirection(move) * (v * Time.deltaTime);
        }
    }
}
