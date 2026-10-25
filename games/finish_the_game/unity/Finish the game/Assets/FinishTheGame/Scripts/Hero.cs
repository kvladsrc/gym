using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace FinishTheGame
{
    /// <summary>The hero: WASD by the camera's heading (W is the way the camera
    /// looks), or a click on the near ground to walk there by the navmesh.
    /// He is kept in the camera's frame, not by walls: no step takes him out
    /// past its sides (<see cref="margin"/>) or its bottom
    /// (<see cref="bottom"/>) (with <see cref="inFrame"/>; a camera turned by
    /// the player cannot keep him), nor out of what the world allows
    /// (<see cref="StoryWorld.Outside"/>). His place on the story's curve,
    /// <see cref="S"/> (unwrapped: it grows on each loop) and
    /// <see cref="T"/>, follows him.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class Hero : MonoBehaviour
    {
        public StoryWorld world;
        public ForwardCamera view;
        public StoryMap map;
        public float speed = 18f, turn = 720f;
        /// <summary>Only forward, the camera's way (the world turns instead: <see cref="CrystalWorld"/>).</summary>
        public bool forwardOnly;
        /// <summary>His look's material (else the world's): never masked by the band.</summary>
        public Material look;
        /// <summary>His rigged model (the studio's, Mixamo's skeleton) and its
        /// controller (Speed, m/s); without them, boxes.</summary>
        public GameObject rig;
        public RuntimeAnimatorController gaits;
        /// <summary>The rig's height, m.</summary>
        public float rigHeight = 1.8f;
        Animator animator;
        Vector3 lastPlace;
        /// <summary>Kept in the camera's frame; its parts kept clear: each side's share, the bottom's.</summary>
        public bool inFrame = true;
        public float margin = 0.06f, bottom = 0.1f;

        public float S { get; private set; }
        public float T { get; private set; }
        /// <summary>Held by a scene (a talk, a spirit): no steps of the
        /// player's; turned to face <see cref="facing"/>, if any.</summary>
        public bool held;
        public Vector3? facing;

        CharacterController body;
        Camera eye;
        NavMeshPath navPath;
        Vector3[] corners;
        int corner;
        float fall;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            eye = view.GetComponent<Camera>();
            navPath = new NavMeshPath();
            if (rig != null && gaits != null)
            {
                // A character controller holds the capsule a skin above the ground.
                animator = Rigs.Dress(rig, transform, rigHeight, gaits, look, 0.08f);
                return;
            }
            var box = new GameObject("Look").transform;
            box.SetParent(transform, false);
            Part(box, new Vector3(0.8f, 1.8f, 0.8f), Vector3.zero, new Color(0.2f, 0.45f, 0.85f));
            Part(box, new Vector3(0.3f, 0.3f, 0.5f), new Vector3(0, 1.3f, 0.5f), new Color(0.95f, 0.85f, 0.3f));
        }

        void Part(Transform parent, Vector3 size, Vector3 at, Color colour)
        {
            var go = new GameObject("Part");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.AddComponent<MeshFilter>().sharedMesh = MeshKit.Box(size, colour, 0.5f);
            go.AddComponent<MeshRenderer>().sharedMaterial = look != null ? look : world.material;
        }

        /// <summary>On the road at s, facing along it.</summary>
        public void Place(float s)
        {
            body = GetComponent<CharacterController>();
            body.enabled = false;
            transform.SetPositionAndRotation(world.FromLevel(world.Path.At(s, 0, 0.05f)), Quaternion.LookRotation(world.FromLevelWay(world.Path.Tangent(s))));
            body.enabled = true;
            S = s;
            T = 0;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var input = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1;
            }
            if (forwardOnly) input = new Vector2(0, Mathf.Max(0, input.y));
            // (Not with the camera turned by the mouse: there a click takes the mouse.)
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !view.orbit && !held)
                Click(mouse.position.ReadValue());
            if (held)
            {
                input = Vector2.zero;
                corners = null;
            }

            var heading = Quaternion.Euler(0, view.Heading, 0);
            var move = heading * new Vector3(input.x, 0, input.y);
            if (move != Vector3.zero) corners = null;
            else if (corners != null) move = Follow();
            var step = Vector3.ClampMagnitude(move, 1f) * (speed * Time.deltaTime);
            step = Kept(world.Steer(transform.position, step), heading);

            fall = body.isGrounded ? -1f : fall - 20f * Time.deltaTime;
            body.Move(step + Vector3.up * (fall * Time.deltaTime));
            if (step.sqrMagnitude > 1e-8f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(step), turn * Time.deltaTime);
            else if (held && facing is Vector3 at)
            {
                var to = at - transform.position;
                to.y = 0;
                if (to.sqrMagnitude > 1e-4f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 0.5f * turn * Time.deltaTime);
            }

            if (animator != null)
            {
                var moved = transform.position - lastPlace;
                moved.y = 0;
                animator.SetFloat("Speed", Time.deltaTime > 0 ? moved.magnitude / Time.deltaTime : 0f, 0.1f, Time.deltaTime);
            }
            lastPlace = transform.position;
            var st = world.Path.Project(world.ToLevel(transform.position), S, 10f);
            S = st.x;
            T = st.y;
        }

        /// <summary>The step, or its part along the camera's sides or heading,
        /// that leaves the hero no further out of bounds than he is.</summary>
        Vector3 Kept(Vector3 step, Quaternion heading)
        {
            if (step == Vector3.zero) return step;
            float now = Outside(transform.position);
            var right = heading * Vector3.right;
            var forward = heading * Vector3.forward;
            foreach (var tried in new[] { step, right * Vector3.Dot(step, right), forward * Vector3.Dot(step, forward) })
                if (tried.sqrMagnitude > 1e-10f && Outside(transform.position + tried) <= now + 1e-5f)
                    return tried;
            return Vector3.zero;
        }

        /// <summary>How far the hero would stand out of bounds: out of the
        /// world's, and his middle out of the kept part of the frame.</summary>
        float Outside(Vector3 at)
        {
            float beyond = world.Outside(at, S);
            if (!inFrame) return beyond;
            var p = eye.WorldToViewportPoint(at + Vector3.up * 0.9f);
            if (p.z <= 0) return 10f;
            return beyond + Mathf.Max(0, margin - p.x) + Mathf.Max(0, p.x - (1 - margin)) + Mathf.Max(0, bottom - p.y);
        }

        /// <summary>To a clicked point of the near ground (not of what lies
        /// ahead: what is drawn there is not where it is).</summary>
        void Click(Vector2 screen)
        {
            if (map != null && map.Covers(screen)) return;
            var ray = eye.ScreenPointToRay(screen);
            if (!Physics.Raycast(ray, out var hit, 300f)) return;
            float s = world.Path.Project(world.ToLevel(hit.point), S, 60f).x;
            if (s > view.Focus + world.Ahead || s < view.Focus - world.Behind) return;
            corner = 1;
            // By the navmesh round things; without one, straight there.
            if (NavMesh.SamplePosition(hit.point, out var near, 2f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(transform.position, near.position, NavMesh.AllAreas, navPath) && navPath.corners.Length >= 2)
                corners = navPath.corners;
            else
                corners = new[] { transform.position, hit.point };
        }

        Vector3 Follow()
        {
            while (corner < corners.Length)
            {
                var to = corners[corner] - transform.position;
                to.y = 0;
                if (to.magnitude > 0.3f) return to.magnitude > 1f ? to.normalized : to;
                corner++;
            }
            corners = null;
            return Vector3.zero;
        }
    }
}
