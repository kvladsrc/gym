using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Soulslike.Training
{
    /// <summary>
    /// What a fighter's grid sees (the editor's "Soulslike/Grid Demo"): its
    /// cells as flat tiles on the floor, marked as the sensor marks them. Each
    /// cell's box is asked of the physics as ML-Agents' BoxOverlapChecker asks
    /// it (the same box, rotation and layers; the agent's own body left out;
    /// of the tagged colliders in it, the one nearest the grid's centre): the
    /// other fighter red, an obstacle blue, nothing a faint outline. For both
    /// fighters, where each stands as its controller (grey) and as its hips
    /// (yellow: the agents measure the other's bearing and distance between
    /// these), and the line they measure; on the screen, how far its hips
    /// are from its controller, now and at most in its current action. R
    /// makes it roll aside. The camera hangs behind and above the fighter.
    /// Both bodies' six points as the network saw them at its last decision,
    /// drawn back from its numbers alone (Fight.PlaceOf, in the frame it saw
    /// them from): the other orange, its own blue, each point's speed a white
    /// line (where it goes in 0.15 s); V shows or hides them. They stand on
    /// the bodies when the inputs are right. B hides the bodies (and their
    /// weapons), leaving the points alone. It asks both agents' decisions
    /// itself, every requester's period: in the editor's play mode without a
    /// domain reload, the requesters stayed subscribed to the last session's
    /// academy, and from ~2 s on no decision came.
    /// Training never uses it.
    /// </summary>
    public class GridView : MonoBehaviour
    {
        public Fighter fighter;
        public Shader shader;
        /// <summary>The colours of the sensor's tags, in its order, and of an empty cell.</summary>
        public Color[] tagColors = { new Color(1f, 0.25f, 0.2f, 0.85f), new Color(0.3f, 0.6f, 1f, 0.8f) };
        public Color emptyColor = new Color(1f, 1f, 1f, 0.07f);
        public float back = 4f, up = 7f;

        GridSensorComponent grid;
        Mesh mesh;
        Vector3[] corners;
        Color[] colors;
        readonly Collider[] found = new Collider[64];
        LineRenderer measured;
        Transform[] markers;
        float apart, mostApart;
        int lastDoing = -1;
        GUIStyle text;
        FighterAgent agent;
        /// <summary>Per body (the other, then its own): the six points, the
        /// five bones from the hips, the six speeds.</summary>
        Transform[] dots;
        LineRenderer[] bones, speeds;
        bool showSeen = true, hideBodies;
        FighterAgent[] deciders = { };
        int decisionPeriod = 10, steps;
        static readonly Color[] SeenColors = { new Color(1f, 0.55f, 0.1f, 1f), new Color(0.2f, 0.75f, 1f, 1f) };

        void Start()
        {
            grid = fighter.eyes.GetComponent<GridSensorComponent>();
            int cells = grid.GridSize.x * grid.GridSize.z;
            corners = new Vector3[cells * 4];
            colors = new Color[cells * 4];
            var quads = new int[cells * 6];
            for (int i = 0; i < cells; i++)
            {
                int v = i * 4;
                quads[i * 6] = v; quads[i * 6 + 1] = v + 1; quads[i * 6 + 2] = v + 2;
                quads[i * 6 + 3] = v; quads[i * 6 + 4] = v + 2; quads[i * 6 + 5] = v + 3;
            }
            mesh = new Mesh { name = "Grid view" };
            mesh.SetVertices(corners);
            mesh.SetColors(colors);
            mesh.SetTriangles(quads, 0);
            var show = new GameObject("Grid view");
            show.AddComponent<MeshFilter>().sharedMesh = mesh;
            var paint = show.AddComponent<MeshRenderer>();
            paint.sharedMaterial = new Material(shader);
            paint.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var glass = new Material(shader);
            measured = new GameObject("Measured line").AddComponent<LineRenderer>();
            measured.sharedMaterial = glass;
            measured.widthMultiplier = 0.04f;
            measured.positionCount = 2;
            measured.startColor = measured.endColor = new Color(1f, 0.9f, 0.1f, 0.9f);
            // Controller, hips; for this fighter, then the other.
            markers = new Transform[4];
            for (int i = 0; i < markers.Length; i++)
            {
                var mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(mark.GetComponent<Collider>());
                mark.name = i % 2 == 0 ? "Controller marker" : "Hips marker";
                mark.transform.localScale = i % 2 == 0 ? new Vector3(0.22f, 0.02f, 0.22f) : new Vector3(0.14f, 0.04f, 0.14f);
                var look = mark.GetComponent<MeshRenderer>();
                look.sharedMaterial = new Material(shader) { color = i % 2 == 0 ? new Color(0.7f, 0.7f, 0.7f, 0.9f) : new Color(1f, 0.9f, 0.1f, 1f) };
                look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                markers[i] = mark.transform;
            }

            agent = fighter.GetComponent<FighterAgent>();
            var pair = new[] { agent, fighter.opponent != null ? fighter.opponent.GetComponent<FighterAgent>() : null };
            deciders = System.Array.FindAll(pair, a => a != null);
            foreach (var a in deciders)
                if (a.GetComponent<Unity.MLAgents.DecisionRequester>() is { } requester)
                {
                    decisionPeriod = requester.DecisionPeriod;
                    requester.enabled = false;
                }
            int n = Fight.PointCount;
            dots = new Transform[2 * n];
            bones = new LineRenderer[2 * (n - 1)];
            speeds = new LineRenderer[2 * n];
            for (int b = 0; b < 2; b++)
            {
                var color = SeenColors[b];
                for (int i = 0; i < n; i++)
                {
                    var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(dot.GetComponent<Collider>());
                    dot.name = "Seen point";
                    dot.transform.localScale = Vector3.one * (i == 1 ? 0.2f : 0.11f);
                    var look = dot.GetComponent<MeshRenderer>();
                    look.sharedMaterial = new Material(shader) { color = color };
                    look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    dots[b * n + i] = dot.transform;
                    speeds[b * n + i] = Line("Seen speed", new Color(1f, 1f, 1f, 0.9f), 0.02f);
                    if (i > 0) bones[b * (n - 1) + i - 1] = Line("Seen bone", color, 0.035f);
                }
            }
        }

        LineRenderer Line(string name, Color color, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.sharedMaterial = new Material(shader);
            line.widthMultiplier = width;
            line.positionCount = 2;
            line.startColor = line.endColor = color;
            return line;
        }

        void ShowBodies(bool shown)
        {
            foreach (var body in new[] { fighter, fighter.opponent })
                if (body != null)
                    foreach (var look in body.GetComponentsInChildren<Renderer>()) look.enabled = shown;
        }

        /// <summary>The points as the network saw them (FighterAgent.SeenPoints), back in the world.</summary>
        void DrawSeen()
        {
            int n = Fight.PointCount;
            bool on = showSeen && agent != null && agent.SeenTall > 0f;
            foreach (var d in dots) d.gameObject.SetActive(on);
            foreach (var l in bones) l.enabled = on;
            foreach (var l in speeds) l.enabled = on;
            if (!on) return;
            for (int b = 0; b < 2; b++)
            {
                var at = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    int k = 2 * (b * n + i);
                    at[i] = agent.SeenFrom + agent.SeenTurn * Fight.PlaceOf(agent.SeenPoints[k], agent.SeenTall);
                    dots[b * n + i].position = at[i];
                    var speed = agent.SeenTurn * Fight.SpeedOf(agent.SeenPoints[k + 1]);
                    speeds[b * n + i].SetPositions(new[] { at[i], at[i] + speed * 0.15f });
                }
                for (int i = 1; i < n; i++) bones[b * (n - 1) + i - 1].SetPositions(new[] { at[0], at[i] });
            }
        }

        void FixedUpdate()
        {
            if (steps++ % decisionPeriod != 0) return;
            foreach (var a in deciders) a.RequestDecision();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.vKey.wasPressedThisFrame) showSeen = !showSeen;
            if (keyboard != null && keyboard.bKey.wasPressedThisFrame)
            {
                hideBodies = !hideBodies;
                if (!hideBodies) ShowBodies(true);
            }
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame && !fighter.Busy)
            {
                fighter.motion.Stop();
                fighter.motion.side = Random.value < 0.5f ? -1f : 1f;
                fighter.Act(fighter.RollAction);
            }
        }

        void OnGUI()
        {
            text ??= new GUIStyle(GUI.skin.label) { fontSize = 22 };
            string doing = fighter.Doing < 0 ? "—" : fighter.Doing == fighter.RollAction ? "roll" : fighter.attackNames[fighter.Doing];
            GUI.Label(new Rect(20, 20, 1100, 120),
                $"hips ↔ capsule: {apart * 100f:F0} cm, up to {mostApart * 100f:F0} cm this action\naction: {doing}   (R: roll aside)\nfigures: the points of the network's inputs at its last decision, the other's orange, its own blue (V; B hides the bodies)", text);
        }

        void LateUpdate()
        {
            if (grid == null) return;
            var offset = fighter.Ground - fighter.transform.position;
            offset.y = 0f;
            apart = offset.magnitude;
            if (fighter.Doing != lastDoing && fighter.Doing >= 0) mostApart = 0f;
            lastDoing = fighter.Doing;
            mostApart = Mathf.Max(mostApart, apart);
            var centre = fighter.eyes;
            var size = grid.GridSize;
            var cell = grid.CellScale;
            var half = new Vector3(cell.x / 2f, cell.y, cell.z / 2f);
            var rotation = grid.RotateWithAgent ? centre.rotation : Quaternion.identity;
            float floor = fighter.transform.position.y + 0.03f;
            // A tile a little smaller than its cell, so the cells stand apart.
            var across = rotation * Vector3.right * cell.x * 0.45f;
            var along = rotation * Vector3.forward * cell.z * 0.45f;
            for (int i = 0; i < size.x * size.z; i++)
            {
                // The cell's place as BoxOverlapChecker lays it out.
                var local = new Vector3((i / size.z - (size.x - 1f) / 2f) * cell.x, 0f, (i % size.z - (size.z - 1f) / 2f) * cell.z);
                var at = grid.RotateWithAgent ? centre.TransformPoint(local) : centre.position + local;
                var color = emptyColor;
                int tag = Seen(at, half, rotation, centre.position);
                if (tag >= 0 && tag < tagColors.Length) color = tagColors[tag];
                var flat = new Vector3(at.x, floor, at.z);
                int v = i * 4;
                corners[v] = flat - across - along;
                corners[v + 1] = flat - across + along;
                corners[v + 2] = flat + across + along;
                corners[v + 3] = flat + across - along;
                colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = color;
            }
            mesh.SetVertices(corners);
            mesh.SetColors(colors);
            mesh.RecalculateBounds();

            var pair = new[] { fighter, fighter.opponent };
            for (int k = 0; k < pair.Length; k++)
            {
                if (pair[k] == null) continue;
                markers[k * 2].position = pair[k].transform.position + Vector3.up * 0.05f;
                markers[k * 2 + 1].position = pair[k].Ground + Vector3.up * 0.08f;
            }
            if (fighter.opponent != null)
                measured.SetPositions(new[] { fighter.Ground + Vector3.up * 0.08f, fighter.opponent.Ground + Vector3.up * 0.08f });

            DrawSeen();
            // Each frame: a fight begun anew shows its weapon again.
            if (hideBodies) ShowBodies(false);

            var view = Camera.main;
            if (view != null)
            {
                var body = fighter.transform;
                var place = body.position - body.forward * back + Vector3.up * up;
                view.transform.SetPositionAndRotation(place, Quaternion.LookRotation(centre.position - place));
            }
        }

        /// <summary>The tag index the sensor gives the cell, -1 none.</summary>
        int Seen(Vector3 at, Vector3 half, Quaternion rotation, Vector3 centre)
        {
            int count = Physics.OverlapBoxNonAlloc(at, half, found, rotation, grid.ColliderMask);
            int best = -1;
            float nearest = float.MaxValue;
            var tags = grid.DetectableTags;
            for (int n = 0; n < count; n++)
            {
                var go = found[n].gameObject;
                if (ReferenceEquals(go, grid.AgentGameObject)) continue;
                float distance = (found[n].ClosestPointOnBounds(at) - centre).sqrMagnitude;
                if (distance >= nearest) continue;
                for (int t = 0; t < tags.Length; t++)
                    if (go.CompareTag(tags[t]))
                    {
                        best = t;
                        nearest = distance;
                        break;
                    }
            }
            return best;
        }
    }
}
