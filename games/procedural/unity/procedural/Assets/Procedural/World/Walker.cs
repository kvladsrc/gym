using UnityEngine;
using UnityEngine.InputSystem;

namespace Procedural.World
{
    /// <summary>First-person walking: WASD, Shift to run, Space to jump, right mouse to look.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class Walker : MonoBehaviour
    {
        public float walkSpeed = 4f;
        public float runSpeed = 9f;
        public float jumpHeight = 1.2f;
        public float gravity = -20f;
        public float lookSensitivity = 0.12f;
        public Transform view;

        /// <summary>When false, <see cref="moveInput"/> replaces the keyboard and mouse.</summary>
        public bool useInput = true;

        /// <summary>Movement override for tests: x = right, y = forward.</summary>
        public Vector2 moveInput;

        /// <summary>Degrees per second of turning when <see cref="useInput"/> is false.</summary>
        public float turnInput;

        /// <summary>Whether the controller touched ground during the last move.</summary>
        public bool isGrounded { get; private set; }

        CharacterController controller;
        float yaw;
        float pitch;
        float verticalVelocity;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.08f;
        }

        void Start()
        {
            yaw = transform.eulerAngles.y;
            if (view == null)
            {
                var camera = Camera.main;
                if (camera != null)
                {
                    view = camera.transform;
                    // The scene camera flies when there is no walker; on foot it follows the walker only.
                    var fly = camera.GetComponent<FlyCamera>();
                    if (fly != null)
                        fly.enabled = false;
                    view.SetParent(transform, false);
                    view.localPosition = new Vector3(0f, 1.65f, 0f);
                    view.localRotation = Quaternion.identity;
                }
            }

            if (view != null)
            {
                var euler = view.localEulerAngles;
                pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            }

            if (transform.position == Vector3.zero)
                PlaceOnTerrain();
            if (useInput)
                CursorLock.Lock();
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            // The locked cursor steers the view; with the cursor freed, the right button still does.
            bool look = useInput && (CursorLock.Update(keyboard, mouse) || (mouse != null && mouse.rightButton.isPressed));
            if (look && mouse != null)
            {
                var delta = mouse.delta.ReadValue() * lookSensitivity;
                yaw += delta.x;
                pitch = Mathf.Clamp(pitch - delta.y, -85f, 85f);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                if (view != null)
                    view.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            isGrounded = controller.isGrounded;
            if (isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;

            if (useInput && isGrounded && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            if (!useInput && turnInput != 0f)
            {
                yaw += turnInput * deltaTime;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            Vector2 input;
            bool running = false;
            if (useInput)
            {
                input = Vector2.zero;
                if (keyboard != null)
                {
                    if (keyboard.wKey.isPressed) input.y += 1f;
                    if (keyboard.sKey.isPressed) input.y -= 1f;
                    if (keyboard.aKey.isPressed) input.x -= 1f;
                    if (keyboard.dKey.isPressed) input.x += 1f;
                    running = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                }
            }
            else
            {
                input = moveInput;
            }

            input = Vector2.ClampMagnitude(input, 1f);
            float speed = running ? runSpeed : walkSpeed;
            var direction = Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.x, 0f, input.y);
            var displacement = direction * (speed * deltaTime);
            verticalVelocity += gravity * deltaTime;
            displacement.y = verticalVelocity * deltaTime;

            controller.Move(displacement);
            isGrounded = controller.isGrounded;
        }

        void PlaceOnTerrain()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return;

            var data = terrain.terrainData;
            Vector3 position = terrain.transform.position;
            var center = new Vector3(position.x + data.size.x * 0.5f, 0f, position.z + data.size.z * 0.5f);
            var origin = new Vector3(center.x, position.y + data.size.y + 1f, center.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, data.size.y + 2f, ~0,
                    QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point + Vector3.up * 0.1f;
                verticalVelocity = 0f;
            }
        }
    }
}
