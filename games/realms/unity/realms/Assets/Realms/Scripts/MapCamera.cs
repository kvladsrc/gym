using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Realms
{
    /// <summary>
    /// The strategy camera: looks at a point on the map from a distance that the wheel changes;
    /// tilts down as it pulls back. Pans with WASD or the arrows and by dragging with the right
    /// mouse button; zooms towards the cursor; turns around the point with Q and E (or Z and X)
    /// or by dragging with the middle button (it starts facing north; Backspace faces north again). Moves smoothly towards its goal. A town
    /// or hero screen holds it close on its subject (<see cref="Hold"/>), lower and swaying.
    /// It keeps over the land the players have seen (<see cref="SetSeen"/>) and zooms between
    /// fixed limits, <see cref="minDistance"/> and <see cref="Highest"/>, whatever is seen.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class MapCamera : MonoBehaviour
    {
        /// <summary>
        /// The nearest distance, and the distance at which the camera has tilted all the way down
        /// (<see cref="farPitch"/>); it may pull back further, to <see cref="Highest"/>, looking
        /// down as steeply.
        /// </summary>
        public float minDistance = 6f, maxDistance = 75f;

        /// <summary>
        /// The farthest the camera pulls back, always: from here a map of the usual size fits the
        /// view. The limit once followed the seen land and the share of the view the clouds over
        /// the unseen land filled, measured over the middle of the seen land; once a hero had gone
        /// far, that middle lay by unseen land, and in the family's game of 2026-11-12 the camera
        /// was held at 12 from about turn 50, the wheel pulling back no more. The players asked
        /// for a higher camera and for fixed limits instead (2026-11-12).
        /// </summary>
        public const float Highest = 90f;
        public float closePitch = 44f, farPitch = 66f;
        public float smoothing = 12f;
        public float turnSpeed = 100f;

        public Vector2 Target => target;
        public float Distance => distance;

        /// <summary>The way the camera faces, degrees clockwise from north.</summary>
        public float Yaw => yaw;

        /// <summary>Set by the game when the pointer is over the interface.</summary>
        public bool PointerOverUi { get; set; }

        /// <summary>Set when the camera should ignore the keyboard (a dialog is open).</summary>
        public bool KeyboardBlocked { get; set; }

        /// <summary>
        /// Set while the capture run moves the camera: it ignores the keyboard and the mouse, which
        /// otherwise zoomed and panned the shots when someone used the computer meanwhile (shots29).
        /// </summary>
        public bool Scripted { get; set; }

        /// <summary>Where a screen holds the camera, and where the map was before it.</summary>
        sealed class Held
        {
            public Vector2 Point, BackTarget;
            public float Near, Far, Share, Facing, Sway, BaseSway, Clock, BackDistance, BackYaw;
        }

        /// <summary>Seconds for one sway of a held camera, there and back.</summary>
        const float SwayPeriod = 28f;

        /// <summary>How far past the seen land the camera's point may go, world units (2-3 cells).</summary>
        const float SeenMargin = 4f;


        Held held;

        /// <summary>0 on the map, 1 held by a screen; eases between, for the pitch.</summary>
        float hold;
        float heldPitch = 34f, heldHeight = 0.2f;

        /// <summary>The held subject's cutaway (see <see cref="Hold"/>), kept while the camera eases back.</summary>
        Vector4 cut;
        static readonly int CutId = Shader.PropertyToID("_RealmsCut"), CutEyeId = Shader.PropertyToID("_RealmsCutEye");

        Camera cam;
        Vector2 target, goalTarget;
        float distance = 40f, goalDistance = 40f;
        float yaw, goalYaw;
        Rect bounds = new Rect(0, 0, 100, 50);
        Rect seen;
        bool dragging, turning;
        float turnFrom;
        Vector3 dragGround;
        UniversalRenderPipelineAsset pipeline;

        void Awake()
        {
            cam = GetComponent<Camera>();
            pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        }

        /// <summary>No cutaway while this camera does not drive the view (a battle's own camera).</summary>
        void OnDisable() => Shader.SetGlobalVector(CutEyeId, Vector4.zero);

        public void SetBounds(Rect rect) => bounds = rect;

        /// <summary>
        /// The land the players have seen, a box in x and z (empty: the whole map): the camera's
        /// point stays over the box and a margin of a few cells round it.
        /// </summary>
        public void SetSeen(Rect area)
        {
            seen = area;
            goalTarget = Clamp(goalTarget);
            goalDistance = ClampDistance(goalDistance);
        }

        /// <summary>The middle of the seen land (see <see cref="SetSeen"/>), or of the world before any is seen.</summary>
        public Vector2 SeenMiddle => seen.width > 0f ? seen.center : bounds.center;

        /// <summary>Flies to a point; <paramref name="snap"/> jumps there at once.</summary>
        public void Focus(Vector2 point, float newDistance, bool snap = false)
        {
            if (held != null)
            {
                // A screen holds the camera: the map comes back here when it lets go.
                held.BackTarget = Clamp(point);
                held.BackDistance = Mathf.Clamp(newDistance, minDistance, Highest);
                return;
            }
            goalTarget = Clamp(point);
            goalDistance = Mathf.Clamp(newDistance, minDistance, Highest);
            if (snap)
            {
                target = goalTarget;
                distance = goalDistance;
                Apply();
            }
        }

        public void FocusKeepZoom(Vector2 point) => Focus(point, goalDistance);

        /// <summary>
        /// The point <paramref name="by"/> nearer the camera than <paramref name="point"/>: a
        /// subject standing on the point then sits in the middle of the screen.
        /// </summary>
        public Vector2 Nearer(Vector2 point, float by) => point - Forward(goalYaw) * by;

        /// <summary>Turns back to face north, the shorter way round.</summary>
        public void FaceNorth() => goalYaw = Mathf.Round(goalYaw / 360f) * 360f;

        /// <summary>
        /// Turns back to the default angle (Backspace, asked for by the players on 2026-11-11):
        /// north on the map; on a town or hero screen, the side the screen chose, swaying again.
        /// </summary>
        public void FaceDefault()
        {
            if (held == null)
            {
                FaceNorth();
                return;
            }
            // The nearer way round; the sway then goes on from the same turn of the circle.
            held.Facing = goalYaw + Mathf.DeltaAngle(goalYaw, held.Facing);
            goalYaw = held.Facing;
            held.Sway = held.BaseSway;
            held.Clock = 0f;
        }

        /// <summary>Faces <paramref name="degrees"/> clockwise from north; <paramref name="snap"/> at once.</summary>
        public void Face(float degrees, bool snap = false)
        {
            goalYaw += Mathf.DeltaAngle(goalYaw, degrees);
            if (snap)
            {
                yaw = goalYaw;
                Apply();
            }
        }

        /// <summary>
        /// Pans as W A S D do for <paramref name="dt"/> seconds (x right, y up the screen, the way
        /// the camera faces); <paramref name="fast"/> is Shift. The keys and the promo's player use it.
        /// </summary>
        public void Pan(Vector2 move, float dt, bool fast = false)
        {
            if (move == Vector2.zero || held != null)
                return;
            Vector2 forward = Forward(goalYaw), right = new Vector2(forward.y, -forward.x);
            Vector2 step = (right * move.x + forward * move.y).normalized;
            goalTarget = Clamp(goalTarget + step * (goalDistance * 0.9f * (fast ? 2f : 1f) * dt));
        }

        /// <summary>Turns as Q (+1) and E (-1) do for <paramref name="dt"/> seconds, stopping a screen's sway.</summary>
        public void TurnKeys(float turn, float dt, bool fast = false)
        {
            if (turn == 0f)
                return;
            goalYaw += turn * turnSpeed * (fast ? 2f : 1f) * dt;
            if (held != null)
                held.Sway = 0f;
        }

        /// <summary>Zooms as + (-1) and - (+1) do for <paramref name="dt"/> seconds.</summary>
        public void ZoomKeys(float zoom, float dt)
        {
            if (zoom != 0f)
                goalDistance = ClampDistance(goalDistance * Mathf.Pow(2.2f, zoom * dt));
        }

        /// <summary>The mouse wheel's <paramref name="notches"/> (positive: nearer), towards the ground under <paramref name="pointer"/>.</summary>
        public void Wheel(float notches, Vector2 pointer)
        {
            bool hadGround = GroundUnder(pointer, out Vector3 before);
            float oldGoal = goalDistance;
            goalDistance = ClampDistance(goalDistance * Mathf.Pow(0.82f, notches));
            if (hadGround && !dragging && held == null)
            {
                // Zoom towards the cursor: move the target along with the change of distance.
                float share = 1f - goalDistance / oldGoal;
                Vector2 toCursor = new Vector2(before.x, before.z) - goalTarget;
                goalTarget = Clamp(goalTarget + toCursor * share);
            }
        }

        /// <summary>A screen is holding the camera.</summary>
        public bool Holding => held != null;

        /// <summary>
        /// Holds the camera on <paramref name="point"/> (x, height, z) for a screen: low (<paramref name="pitch"/>),
        /// zooming only between <paramref name="near"/> and <paramref name="far"/>, facing
        /// <paramref name="facing"/> and swaying slowly <paramref name="sway"/> degrees either side
        /// until the players turn it themselves, with the point <paramref name="share"/> of the
        /// half-width right of the middle (negative: left) to leave room for the screen's panel.
        /// The plants rooted in front of the point go whole (the prop shader's cutaway) in a band
        /// <paramref name="clearWidth"/> either side of the line to the camera, from
        /// <paramref name="clearFrom"/> away from the point on. <see cref="Release"/> goes back.
        /// </summary>
        public void Hold(Vector3 point, float distance, float near, float far, float pitch, float share, float facing, float sway,
            float clearFrom, float clearWidth)
        {
            held ??= new Held { BackTarget = goalTarget, BackDistance = goalDistance, BackYaw = goalYaw };
            cut = new Vector4(point.x, point.z, clearFrom, clearWidth);
            held.Point = new Vector2(point.x, point.z);
            heldHeight = point.y;
            held.Near = near;
            held.Far = far;
            held.Share = share;
            held.Facing = goalYaw + Mathf.DeltaAngle(goalYaw, facing);
            held.Sway = sway;
            held.BaseSway = sway;
            held.Clock = 0f;
            goalYaw = held.Facing;
            heldPitch = pitch;
            goalDistance = Mathf.Clamp(distance, near, far);
        }

        /// <summary>
        /// Where the camera stands over the map, or will stand once the screen holding it lets it
        /// go, for the saves: the point, the distance and the facing it is heading to.
        /// </summary>
        public void MapPose(out Vector2 point, out float mapDistance, out float facing)
        {
            if (held != null)
            {
                point = held.BackTarget;
                mapDistance = held.BackDistance;
                facing = held.BackYaw;
            }
            else
            {
                point = goalTarget;
                mapDistance = goalDistance;
                facing = goalYaw;
            }
        }

        /// <summary>
        /// Back to the map as it was before the screen, facing the way it faced then: the players
        /// found the map left turned at the screen's angle (2026-11-11).
        /// </summary>
        public void Release()
        {
            if (held == null)
                return;
            goalTarget = held.BackTarget;
            goalDistance = held.BackDistance;
            goalYaw += Mathf.DeltaAngle(goalYaw, held.BackYaw);
            held = null;
        }

        static Vector2 Forward(float degrees) => new Vector2(Mathf.Sin(degrees * Mathf.Deg2Rad), Mathf.Cos(degrees * Mathf.Deg2Rad));

        float ClampDistance(float d) => held != null ? Mathf.Clamp(d, held.Near, held.Far) : Mathf.Clamp(d, minDistance, Highest);

        /// <summary>The point kept over the map and over the seen land with its margin.</summary>
        Vector2 Clamp(Vector2 p)
        {
            Rect r = bounds;
            if (seen.width > 0f)
                r = Rect.MinMaxRect(Mathf.Max(r.xMin, seen.xMin - SeenMargin), Mathf.Max(r.yMin, seen.yMin - SeenMargin),
                    Mathf.Min(r.xMax, seen.xMax + SeenMargin), Mathf.Min(r.yMax, seen.yMax + SeenMargin));
            return new Vector2(Mathf.Clamp(p.x, r.xMin, Mathf.Max(r.xMin, r.xMax)), Mathf.Clamp(p.y, r.yMin, Mathf.Max(r.yMin, r.yMax)));
        }

        float Pitch(float d) => Mathf.Lerp(closePitch, farPitch, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minDistance, maxDistance, d)));

        void Update()
        {
            float dt = Mathf.Min(ViewClock.Delta, 0.05f);
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard != null && !KeyboardBlocked && !Scripted)
            {
                Vector2 move = Vector2.zero;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1;
                bool fast = keyboard.leftShiftKey.isPressed;
                // The players' speeds from the settings (the interface plan's 3.5); 5 is as before.
                Pan(move, dt * GameSettings.MoveFactor, fast);
                // Q (and Z) turns the map towards the left hand, E (and X) towards the right: the
                // other way round felt backwards to the players (2026-11-10).
                float turn = 0f;
                if (keyboard.qKey.isPressed || keyboard.zKey.isPressed) turn += 1;
                if (keyboard.eKey.isPressed || keyboard.xKey.isPressed) turn -= 1;
                TurnKeys(turn, dt * GameSettings.TurnFactor, fast);
                float zoom = 0f;
                if (keyboard.equalsKey.isPressed || keyboard.numpadPlusKey.isPressed) zoom -= 1;
                if (keyboard.minusKey.isPressed || keyboard.numpadMinusKey.isPressed) zoom += 1;
                ZoomKeys(zoom, dt);
            }

            if (mouse != null && !Scripted)
            {
                Vector2 pointer = mouse.position.ReadValue();
                bool dragButton = mouse.rightButton.isPressed;
                if (dragButton && !dragging && held == null && !PointerOverUi && GroundUnder(pointer, out dragGround))
                    dragging = true;
                if (!dragButton)
                    dragging = false;
                if (dragging && GroundUnder(pointer, out Vector3 now))
                {
                    // Keep the grabbed point of the ground under the pointer.
                    Vector3 delta = dragGround - now;
                    goalTarget = Clamp(goalTarget + new Vector2(delta.x, delta.z));
                    target = goalTarget;
                    Apply();
                }

                // The middle button turns the map like a table: across the screen is half a turn.
                bool turnButton = mouse.middleButton.isPressed;
                if (turnButton && !turning && !PointerOverUi)
                {
                    turning = true;
                    turnFrom = pointer.x;
                }
                if (!turnButton)
                    turning = false;
                if (turning)
                {
                    goalYaw += (pointer.x - turnFrom) / Mathf.Max(Screen.width, 1) * 180f;
                    turnFrom = pointer.x;
                    yaw = goalYaw;
                    if (held != null)
                        held.Sway = 0f;
                }

                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f && !PointerOverUi)
                {
                    // Some platforms report 120 per notch, others 1.
                    float notches = Mathf.Abs(scroll) > 20f ? scroll / 120f : scroll;
                    Wheel(Mathf.Clamp(notches, -3f, 3f), pointer);
                }
            }

            // The pointer at the screen's edge moves the map when the players turn it on in the
            // settings: only in a focused window, not while a dialog, the menu or a screen holds
            // the map, nor while it is dragged or turned.
            if (GameSettings.EdgePan && mouse != null && !KeyboardBlocked && !Scripted && !dragging && !turning && Application.isFocused)
                Pan(EdgeMove(mouse.position.ReadValue()), dt * GameSettings.MoveFactor);

            if (held != null)
            {
                // Sway round the subject and keep it beside the screen's panel.
                if (held.Sway > 0f)
                {
                    held.Clock += dt;
                    goalYaw = held.Facing + held.Sway * Mathf.Sin(held.Clock * (2f * Mathf.PI / SwayPeriod));
                }
                Vector2 forward = Forward(goalYaw), right = new Vector2(forward.y, -forward.x);
                float halfWidth = goalDistance * Mathf.Tan(Camera.VerticalToHorizontalFieldOfView(cam.fieldOfView, cam.aspect) * 0.5f * Mathf.Deg2Rad);
                goalTarget = held.Point - right * (held.Share * halfWidth);
            }

            float k = 1f - Mathf.Exp(-smoothing * dt);
            hold = Mathf.MoveTowards(hold, held != null ? 1f : 0f, dt * 1.6f);
            target = Vector2.Lerp(target, goalTarget, k);
            distance = Mathf.Lerp(distance, goalDistance, k);
            yaw = Mathf.Lerp(yaw, goalYaw, k);
            Apply();
        }

        /// <summary>Which way the pointer at the screen's edge moves the map: a few pixels at each edge, nothing outside the window.</summary>
        static Vector2 EdgeMove(Vector2 pointer)
        {
            if (pointer.x < 0f || pointer.y < 0f || pointer.x >= Screen.width || pointer.y >= Screen.height)
                return Vector2.zero;
            float edge = Mathf.Max(3f, Screen.height * 0.005f);
            var move = Vector2.zero;
            if (pointer.x <= edge)
                move.x -= 1;
            else if (pointer.x >= Screen.width - 1 - edge)
                move.x += 1;
            if (pointer.y <= edge)
                move.y -= 1;
            else if (pointer.y >= Screen.height - 1 - edge)
                move.y += 1;
            return move;
        }

        void Apply()
        {
            float pitch = Mathf.Lerp(Pitch(distance), heldPitch, Mathf.SmoothStep(0f, 1f, hold));
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = new Vector3(target.x, Mathf.Lerp(0.2f, heldHeight, hold), target.y);
            Vector3 position = focus - rotation * Vector3.forward * distance;
            transform.SetPositionAndRotation(position, rotation);
            Shader.SetGlobalVector(CutId, cut);
            Shader.SetGlobalVector(CutEyeId, new Vector4(position.x, position.z, Mathf.SmoothStep(0f, 1f, hold), 0f));
            cam.nearClipPlane = Mathf.Max(0.1f, distance * 0.02f);
            cam.farClipPlane = distance * 4f + 120f;
            if (pipeline != null)
                pipeline.shadowDistance = distance * 1.9f + 12f;
        }

        /// <summary>The point on the sea level plane under a screen position.</summary>
        public bool GroundUnder(Vector2 screen, out Vector3 point)
        {
            Ray ray = cam.ScreenPointToRay(screen);
            point = default;
            if (ray.direction.y > -0.001f)
                return false;
            float t = -ray.origin.y / ray.direction.y;
            point = ray.origin + ray.direction * t;
            return true;
        }

        /// <summary>The corners of the view on the sea plane, for the minimap.</summary>
        public bool ViewCorners(Vector3[] corners)
        {
            var screen = new[] { new Vector2(0, 0), new Vector2(Screen.width, 0), new Vector2(Screen.width, Screen.height), new Vector2(0, Screen.height) };
            for (int i = 0; i < 4; i++)
            {
                if (!GroundUnder(screen[i], out corners[i]))
                    return false;
            }
            return true;
        }
    }
}
