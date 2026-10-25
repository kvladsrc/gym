using UnityEngine;

namespace FinishTheGame
{
    /// <summary>A camera on the story's curve, as the rail cameras of the old
    /// action games: it only goes forward along the curve (its
    /// <see cref="Focus"/> is the farthest s the hero has reached), but it
    /// turns with the road, smoothly and a little ahead, and follows the hero
    /// to the sides along a rail of <see cref="rail"/> metres either way. The
    /// hero is kept in its frame (<see cref="Hero"/>), not by walls.
    ///
    /// Or, <see cref="orbit"/>: as in any game seen over the shoulder, the
    /// mouse turns it round the hero and up and down (the cursor held by the
    /// game: a click takes it, Esc lets it go; let go, the mouse turns it with
    /// its right button held), the wheel brings it nearer or further; it looks
    /// at the hero's shoulders (<see cref="height"/> up) from
    /// <see cref="distance"/>, drawn in before a wall or the ground. Its
    /// focus still only goes forward along the story.</summary>
    public class ForwardCamera : MonoBehaviour
    {
        public StoryWorld world;
        public Hero hero;
        public float height = 13f, distance = 20f, pitch = 26f;
        /// <summary>How far the camera follows the hero to the sides, m, and how fast.</summary>
        public float rail = 8f, sideEase = 2f;
        /// <summary>The road's heading is read from this far behind the focus to this far ahead, m.</summary>
        public float lookBack = 5f, lookAhead = 18f;
        /// <summary>The turn's smoothing time, s.</summary>
        public float turnTime = 0.8f;
        /// <summary>Turned by the player round the hero: degrees a pixel of
        /// the mouse; its lowest and highest pitch, degrees; its nearest and
        /// furthest, m, and metres a notch of the wheel.</summary>
        public bool orbit;
        public float mouseTurn = 0.12f;
        public Vector2 pitchRange = new Vector2(-25f, 70f), distanceRange = new Vector2(2f, 14f);
        public float zoomStep = 0.6f;

        /// <summary>The s the camera looks at, unwrapped; it never decreases.</summary>
        public float Focus { get; private set; }
        public float Heading => yaw;
        /// <summary>The camera's heading, degrees; set by the world that steers it (<see cref="CrystalWorld"/>).</summary>
        public float Yaw { get => yaw; set => yaw = value; }
        float side, yaw, turning;
        /// <summary>The mouse is held by the game (the orbit's look).</summary>
        public static bool Held => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>Its focus where the hero is now (he was put elsewhere), its heading this, degrees.</summary>
        public void Restart(float heading)
        {
            Focus = hero.S;
            yaw = heading;
        }

        void Start()
        {
            Focus = hero.S;
            side = Mathf.Clamp(hero.T, -rail, rail);
            yaw = Target();
            Place();
        }

        void LateUpdate()
        {
            Focus = Mathf.Max(Focus, hero.S);
            if (orbit)
            {
                Turn();
                return;
            }
            side = Mathf.Lerp(side, Mathf.Clamp(hero.T, -rail, rail), 1f - Mathf.Exp(-sideEase * Time.deltaTime));
            yaw = Mathf.SmoothDampAngle(yaw, Target(), ref turning, turnTime);
            Place();
        }

        void Turn()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
            if (mouse != null)
            {
                if (!Held && mouse.leftButton.wasPressedThisFrame) Cursor.lockState = CursorLockMode.Locked;
                if (Held || mouse.rightButton.isPressed)
                {
                    var delta = mouse.delta.ReadValue() * mouseTurn;
                    yaw += delta.x;
                    pitch = Mathf.Clamp(pitch - delta.y, pitchRange.x, pitchRange.y);
                }
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel != 0) distance = Mathf.Clamp(distance - Mathf.Sign(wheel) * zoomStep, distanceRange.x, distanceRange.y);
            }
            Cursor.visible = !Held;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            var pivot = hero.transform.position + Vector3.up * height;
            var back = rotation * Vector3.back;
            // Drawn in before what stands behind (a wall, the ground's rise).
            float reach = distance;
            if (Physics.SphereCast(pivot, 0.25f, back, out var hit, distance, ~0, QueryTriggerInteraction.Ignore) && hit.collider.transform.root != hero.transform.root)
                reach = Mathf.Max(0.5f, hit.distance - 0.1f);
            transform.SetPositionAndRotation(pivot + back * reach, rotation);
        }

        /// <summary>The road's heading round the focus, degrees.</summary>
        float Target()
        {
            var path = world.Path;
            var way = world.FromLevelWay(path.Position(Focus + lookAhead) - path.Position(Focus - lookBack));
            return Mathf.Atan2(way.x, way.z) * Mathf.Rad2Deg;
        }

        void Place()
        {
            var path = world.Path;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            var forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var at = world.FromLevel(path.At(Focus, side));
            transform.SetPositionAndRotation(at - forward * distance + Vector3.up * height, rotation);
        }
    }
}
