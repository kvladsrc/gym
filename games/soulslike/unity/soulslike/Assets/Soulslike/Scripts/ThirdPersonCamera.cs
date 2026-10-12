using UnityEngine;
using UnityEngine.InputSystem;

namespace Soulslike
{
    /// <summary>Orbit camera behind the player; pulled in when a wall is between.</summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 5f;
        public float height = 1.6f;
        public float sensitivity = 0.12f;
        public LayerMask obstacles = ~0;

        float yaw;
        float pitch = 12f;

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (target != null) yaw = target.eulerAngles.y;
        }

        void LateUpdate()
        {
            if (target == null) return;
            var mouse = Mouse.current;
            // Esc opens the pause menu (PauseMenu), which frees the cursor;
            // otherwise a click back in the game window relocks it (after
            // switching windows).
            if (PauseMenu.Open) return;
            if (mouse != null && Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * sensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -20f, 60f);
            }

            Vector3 pivot = target.position + Vector3.up * height;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 wanted = pivot - rotation * Vector3.forward * distance;
            float reach = distance;
            // Only the level pulls the camera in, not characters walking past.
            foreach (var hit in Physics.SphereCastAll(pivot, 0.25f, wanted - pivot, distance, obstacles, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider is CharacterController || hit.transform.root == target.root) continue;
                reach = Mathf.Min(reach, Mathf.Max(0.3f, hit.distance - 0.1f));
            }
            transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * reach, rotation);
        }

        /// <summary>Camera-relative ground direction for the player's input.</summary>
        public Vector3 Flatten(Vector2 input)
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            return forward * input.y + right * input.x;
        }

        public void LookAlong(float newYaw)
        {
            yaw = newYaw;
            pitch = 12f;
        }
    }
}
