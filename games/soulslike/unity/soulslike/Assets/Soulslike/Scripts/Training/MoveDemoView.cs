using UnityEngine;

namespace Soulslike.Training
{
    /// <summary>
    /// The editor's movement demo (Soulslike/Move Demo): drives one fighter as
    /// an agent would, by its speeds' inertia (Inertia: a third a decision, 6
    /// decisions a second), towards random orders: every 1–2 s new targets
    /// for ahead and aside, in thirds, never turning, so the ellipse's
    /// directions stay put (back to the arena's middle when near its walls). On the ground about it, the ellipse of its most
    /// pace each way, a white circle of <see cref="mark"/> m/s to measure it
    /// by, and an arrow of its pace now; over it a label of the
    /// orders and the pace.
    /// </summary>
    public class MoveDemoView : MonoBehaviour
    {
        public Fighter body;
        public Vector3 centre;
        public float roam = 7f;
        /// <summary>Drawing scale on the ground, m per m/s.</summary>
        public float scale = 0.4f;
        /// <summary>The pace the white circle marks, m/s.</summary>
        public float mark = 3f;
        public TextMesh label;
        public float back = 5.5f, up = 4.5f, follow = 3f;

        const int DecisionFrames = 10;
        Vector3 target;
        float nextOrder;
        int frames;
        Vector3 lastPosition, pace;
        LineRenderer ellipse, circle, arrow;

        void Start()
        {
            var glass = new Material(Shader.Find("Sprites/Default"));
            ellipse = Line("Pace ellipse", glass, new Color(0.3f, 0.8f, 1f, 0.8f), 65);
            ellipse.loop = true;
            circle = Line("Pace mark", glass, new Color(1f, 1f, 1f, 0.7f), 65);
            circle.loop = true;
            circle.widthMultiplier = 0.03f;
            arrow = Line("Pace arrow", glass, new Color(1f, 0.6f, 0.1f, 0.9f), 2);
            lastPosition = body.transform.position;
        }

        LineRenderer Line(string name, Material glass, Color color, int points)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.sharedMaterial = glass;
            line.positionCount = points;
            line.widthMultiplier = 0.05f;
            line.startColor = line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }

        static float Thirds() => Random.Range(-3, 4) / 3f;

        void Order()
        {
            var away = body.transform.position - centre;
            away.y = 0f;
            if (away.magnitude > roam)
            {
                // Near the walls: back to the middle, however it faces.
                var home = body.transform.InverseTransformDirection(-away.normalized);
                target = new Vector3(Mathf.Round(home.x * 3f) / 3f, Mathf.Round(home.z * 3f) / 3f, 0f);
            }
            else target = new Vector3(Thirds(), Thirds(), 0f);
            nextOrder = Time.time + Random.Range(1f, 2f);
        }

        /// <summary>Whether a speed must come down the same way (or to 0),
        /// which no order does.</summary>
        static bool Slower(float now, float want) =>
            Mathf.Abs(now) > 0.01f && now * want >= 0f && Mathf.Abs(want) < Mathf.Abs(now) - 0.01f;

        /// <summary>The order towards a speed: up or down while short of it
        /// (a speed of 0 is kept: it cannot be ordered, Inertia).</summary>
        static int Towards(float now, float want) =>
            Mathf.Abs(want) < 0.01f || Mathf.Abs(want - now) < 0.01f ? 0 : want > now ? 1 : 2;

        void Update()
        {
            if (Time.time >= nextOrder) Order();
            if (++frames % DecisionFrames == 0)
            {
                // Slowing comes only of an action (Inertia): here, stopped
                // outright, then ordered up again.
                if (Slower(body.motion.forward, target.y) || Slower(body.motion.side, target.x)
                    || Slower(body.motion.turn, target.z)) body.motion.Stop();
                else body.motion.Push(Towards(body.motion.forward, target.y), Towards(body.motion.side, target.x),
                    Towards(body.motion.turn, target.z));
            }
            if (Time.deltaTime > 0f)
            {
                var now = (body.transform.position - lastPosition) / Time.deltaTime;
                now.y = 0f;
                pace = Vector3.Lerp(pace, now, 1f - Mathf.Exp(-10f * Time.deltaTime));
            }
            lastPosition = body.transform.position;
        }

        void LateUpdate()
        {
            var at = body.transform.position + Vector3.up * 0.05f;
            var f = body.transform.forward;
            var r = body.transform.right;
            float most = body.speed * body.Size;
            for (int i = 0; i < ellipse.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / ellipse.positionCount;
                float ahead = Mathf.Cos(a), aside = Mathf.Sin(a);
                var p = f * ahead * (ahead < 0f ? body.backwards : 1f) + r * aside * body.sideways;
                ellipse.SetPosition(i, at + p * most * scale);
                circle.SetPosition(i, at + (f * ahead + r * aside) * mark * scale);
            }
            arrow.SetPosition(0, at);
            arrow.SetPosition(1, at + pace * scale);
            var view = Camera.main;
            if (view != null)
            {
                var place = body.transform.position + new Vector3(0f, up, -back);
                float share = 1f - Mathf.Exp(-follow * Time.deltaTime);
                view.transform.position = Vector3.Lerp(view.transform.position, place, share);
                view.transform.rotation = Quaternion.Slerp(view.transform.rotation,
                    Quaternion.LookRotation(body.transform.position + Vector3.up - view.transform.position), share);
                if (label != null)
                {
                    label.transform.position = body.transform.position + Vector3.up * 2.6f;
                    label.transform.rotation = view.transform.rotation;
                    label.text = $"order: ahead {target.y:+0.00;-0.00;0}, aside {target.x:+0.00;-0.00;0}, turn {target.z:+0.00;-0.00;0}\n"
                        + $"now: {body.motion.forward:+0.00;-0.00;0} / {body.motion.side:+0.00;-0.00;0} / {body.motion.turn:+0.00;-0.00;0}, {pace.magnitude:F1} m/s";
                }
            }
        }
    }
}
