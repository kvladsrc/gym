using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FinishTheGame
{
    /// <summary>The plane (<see cref="PlaneWorld"/>) as one ordinary world,
    /// all of it drawn (far things simplified by their mesh LODs), and over
    /// it, an illusion: the story's ribbon. A little ahead, in sight (where
    /// the road is first <see cref="ribbonAhead"/> metres off in a straight
    /// line), the road peels up off the ground: from there the story's strip
    /// (the world cut along the whole route, a place as often as the route
    /// passes it; StripLit.shader) is laid on the ribbon's curve
    /// (<see cref="RibbonBend"/>, sky), which starts as the road there, so
    /// that it lies on the world at first, the same; then it rises, turning
    /// to film, and goes over to a spiral fixed round the level, up into the
    /// sky, the whole story on it to its end. The world goes on under it, as it is.
    /// The ways' forks (<see cref="branches"/>) may be left out: the active way's ribbon alone.
    ///
    /// The hero walks and the camera is turned as in any game seen over the
    /// shoulder (<see cref="ForwardCamera"/>, <see cref="Hero"/>), within
    /// <see cref="roam"/> of the road.</summary>
    [DefaultExecutionOrder(-100)]
    public class CrystalWorld : PlaneWorld
    {
        public Material stripMaterial;
        /// <summary>The ribbon rises off the road where it is first this far off in a straight line, m.</summary>
        public float ribbonAhead = 90f;
        /// <summary>…where the road goes away from the hero, at most this far off the way from him, degrees.</summary>
        public float startAngle = 70f;
        /// <summary>How far off the route the hero may go, m.</summary>
        public float roam = 150f;
        /// <summary>The film's frames (each <see cref="RibbonBend.frameLength"/>
        /// of the story) are pictures of their stretch of it, taken from its
        /// left side as the game starts: this many pixels high (as wide as
        /// the frame's aspect), from these far off (the first whose view is
        /// clear) and this high, m, looking at this height over the road, m,
        /// with this field of view, degrees; this many taken an update.</summary>
        public int framePixels = 72;
        public float[] frameDistances = { 22f, 15f, 30f, 40f };
        public float frameHeight = 4f, frameLook = 3f, frameView = 34f;
        public int framesAnUpdate = 3;
        /// <summary>The forks offered, and the other ways' ribbons shown.</summary>
        public bool branches = true;
        public override bool Forks => branches;
        /// <summary>How far along the route the ribbon's start may be, m, and how fast it follows, m a second.</summary>
        public float maxLeaving = 900f, leavingSpeed = 25f;

        float leaving = -1f;
        readonly List<Renderer> strip = new List<Renderer>();
        /// <summary>Each way's strips, ribbon and marks.</summary>
        readonly List<List<Renderer>> routeStrips = new List<List<Renderer>>();
        readonly List<RibbonBend> ribbons = new List<RibbonBend>();
        readonly List<StoryMarks> routeMarks = new List<StoryMarks>();
        /// <summary>Ways shut, going: since when, and burning (a choice) or fading (a way passed by).</summary>
        readonly Dictionary<int, (float since, bool burn)> going = new Dictionary<int, (float, bool)>();
        /// <summary>How long a shut way takes to go, s; how far ahead a fork is offered, m; how far a branch's ribbon goes off to the side, m.</summary>
        public float goneIn = 3.5f, offerAhead = 450f, branchSide = 140f;
        GUIStyle choiceStyle, choiceActive;

        static readonly int FilmAtlas = Shader.PropertyToID("_FilmAtlas");
        static readonly int FilmGrid = Shader.PropertyToID("_FilmGrid");
        static readonly int Film = Shader.PropertyToID("_RibbonFilm");

        protected override void Awake()
        {
            base.Awake();
            // The level moves as one: its colliders, one kinematic body.
            Colliders.gameObject.AddComponent<Rigidbody>().isKinematic = true;
            var index = ModelSet.Index(models);
            // Each way's own strip, laid on its own ribbon.
            for (int r = 0; r < Routes.Count; r++)
            {
                var drawnOnes = new List<Renderer>();
                foreach (var (look, mesh) in Strip(Routes[r]))
                {
                    var part = new GameObject($"Strip {r} {look}");
                    part.transform.SetParent(transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var drawn = part.AddComponent<MeshRenderer>();
                    drawn.sharedMaterial = StripLook(index);
                    drawn.shadowCastingMode = ShadowCastingMode.Off;
                    Bendable(drawn);
                    strip.Add(drawn);
                    drawnOnes.Add(drawn);
                }
                routeStrips.Add(drawnOnes);
            }
            Far();
        }

        /// <summary>The strip's look: the ground's (its grass), with the road's
        /// earth drawn over it as the world draws it (its texture, tint and
        /// ragged edge: <see cref="PlaneWorld"/>'s road).</summary>
        Material StripLook(Dictionary<string, Model> index)
        {
            var look = Look(stripMaterial, "ground", index);
            var road = Look(material, "road", index);
            if (road == material) return look;
            look.SetTexture("_RoadTex", road.mainTexture);
            look.SetColor("_RoadTint", road.GetColor("_BaseColor"));
            look.SetVector("_RoadEdge", road.GetVector("_RoadEdge"));
            look.SetFloat("_RoadTile", roadTile);
            return look;
        }

        /// <summary>Beyond the level, the land runs on flat to the horizon, into the haze.</summary>
        void Far()
        {
            const float Side = 24000f;
            var far = new GameObject("Far Land");
            far.transform.SetParent(transform, false);
            far.transform.localPosition = Vector3.down * 0.6f;
            var mesh = MeshKit.Grid(Side, 8, grassTexture != null ? Color.white : PlaneLayout.Grass, grassTile);
            far.AddComponent<MeshFilter>().sharedMesh = mesh;
            var look = far.AddComponent<MeshRenderer>();
            look.sharedMaterial = Look(material, "ground", ModelSet.Index(models));
            look.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>The level from above, for the map, is of the world alone.</summary>
        protected override void Start()
        {
            foreach (var part in strip) part.enabled = false;
            base.Start();
            foreach (var part in strip) part.enabled = true;
            // A ribbon and marks for each way: the scene's for the first, copies for the others.
            var marks = FindAnyObjectByType<StoryMarks>();
            for (int r = 0; r < Routes.Count; r++)
            {
                var rb = r == 0 ? ribbon : gameObject.AddComponent<RibbonBend>();
                if (r > 0) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(ribbon), rb);
                rb.world = this;
                rb.view = view;
                rb.route = r;
                rb.renderers = routeStrips[r];
                ribbons.Add(rb);
                if (marks == null) continue;
                var m = r == 0 ? marks : marks.gameObject.AddComponent<StoryMarks>();
                if (r > 0) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(marks), m);
                m.world = this;
                m.view = marks.view;
                m.village = marks.village; m.cave = marks.cave; m.city = marks.city; m.farm = marks.farm; m.end = marks.end;
                m.route = r;
                m.ribbon = rb;
                routeMarks.Add(m);
            }
            StartCoroutine(Frames());
        }

        /// <summary>Takes the film's frames, the nearest first: each its
        /// stretch of the story seen from its left (so that, on the ribbon
        /// round the hero, the story runs on to the left), into an atlas; the
        /// world alone, without the strips or the hero.</summary>
        IEnumerator Frames()
        {
            float length = ribbon.frameLength;
            int count = Mathf.CeilToInt(Path.Length / length);
            int high = framePixels, wide = Mathf.RoundToInt(framePixels * ribbon.frameAspect);
            int columns = Mathf.Max(1, 4096 / wide), rows = Mathf.CeilToInt((float)count / columns);
            var atlas = new RenderTexture(columns * wide, rows * high, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            atlas.Create();
            var active = RenderTexture.active;
            RenderTexture.active = atlas;
            GL.Clear(false, true, new Color(0.11f, 0.07f, 0.12f));
            RenderTexture.active = active;
            Shader.SetGlobalTexture(FilmAtlas, atlas);
            Shader.SetGlobalVector(FilmGrid, new Vector4(columns, rows, count, 0));

            var shot = new RenderTexture(wide, high, 24, RenderTextureFormat.ARGB32);
            var eye = new GameObject("Film").AddComponent<Camera>();
            eye.enabled = false;
            eye.fieldOfView = frameView;
            eye.aspect = (float)wide / high;
            eye.nearClipPlane = 0.3f;
            eye.farClipPlane = 600f;
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = shot };
            int first = Mathf.Clamp(Mathf.FloorToInt(view.Focus / length), 0, count - 1), taken = 0;
            for (int n = 0; n < count; n++)
            {
                // From where the hero is on, then back to the start.
                int k = first + n < count ? first + n : count - 1 - n;
                float s = (k + 0.5f) * length;
                var level = Path.Position(s);
                var way = Path.Tangent(s);
                way.y = 0;
                var right = new Vector3(way.z, 0, -way.x).normalized;
                float ground = Land.Height(level.x, level.z);
                var look = FromLevel(level + Vector3.up * (ground + frameLook));
                Vector3 Eye(float d)
                {
                    var from = level - right * d;
                    from.y = Mathf.Max(ground, Land.Height(from.x, from.z)) + frameHeight;
                    return FromLevel(from);
                }
                // Not from inside a tree, nor through one.
                var place = Eye(frameDistances[0]);
                foreach (float d in frameDistances)
                {
                    var at = Eye(d);
                    if (Physics.CheckSphere(at, 1.5f, ~0, QueryTriggerInteraction.Ignore) || Physics.Linecast(at, look, ~0, QueryTriggerInteraction.Ignore)) continue;
                    place = at;
                    break;
                }
                eye.transform.position = place;
                eye.transform.LookAt(look);
                foreach (var part in strip) part.enabled = false;
                bool walking = hero.gameObject.activeSelf;
                hero.gameObject.SetActive(false);
                if (RenderPipeline.SupportsRenderRequest(eye, request)) RenderPipeline.SubmitRenderRequest(eye, request);
                hero.gameObject.SetActive(walking);
                foreach (var part in strip) part.enabled = true;
                Graphics.CopyTexture(shot, 0, 0, 0, 0, wide, high, atlas, 0, 0, k % columns * wide, k / columns * high);
                if (++taken % framesAnUpdate == 0) yield return null;
            }
            Destroy(eye.gameObject);
            shot.Release();
        }

        /// <summary>The ribbon's start found anew (the hero put elsewhere).</summary>
        public void Restart() => leaving = -1f;

        /// <summary>All the world is drawn, not a band along the route.</summary>
        protected override bool Banded => false;

        protected override void LateUpdate()
        {
            var middle = hero.transform.position;
            middle.y = 0;
            var level = ToLevel(middle);
            // Where the road ahead is first ribbonAhead off in a straight line,
            // going away from the hero (coming back, the ribbon would rise
            // towards him and bend round over his head).
            bool Fits(float d, float slack)
            {
                var off = Path.Position(view.Focus + d) - level;
                off.y = 0;
                if (off.magnitude <= ribbonAhead * (1f - slack)) return false;
                var way = Path.Tangent(view.Focus + d);
                way.y = 0;
                return Vector3.Angle(way, off) <= startAngle * (1f + slack);
            }
            float target = maxLeaving;
            for (float d = 0; d < maxLeaving; d += 2f)
                if (Fits(d, 0f))
                {
                    target = d;
                    break;
                }
            // It jumps where the route loops back and forth (by a town, to a
            // farm and back): followed at a walking pace, so that nothing
            // leaps; but where it is stops fitting, it is moved at once.
            leaving = leaving < 0 || !Fits(leaving, 0.25f) ? target : Mathf.MoveTowards(leaving, target, leavingSpeed * Time.deltaTime);
            ribbon.flat = leaving;
            ribbon.pastFlat = behind;
            ribbon.anchor = hero.transform;
            ribbon.stripHalfWidth = halfWidth;
            Ways();
            base.LateUpdate();
            Shader.SetGlobalVector(Film, new Vector4(halfWidth, ribbon.frameLength, 0, 0));
        }

        /// <summary>The ways: each way's ribbon (the active one's in full; another
        /// from where it parts from the active, off to its side, dim: a tree);
        /// the fork ahead, if near, chosen with Q (its left way) and E (its
        /// right); a way the hero has gone past the parting of is shut: burning
        /// away if it was the other side of a choice, fading if a way passed by.</summary>
        void Ways()
        {
            if (ribbons.Count == 0) return;
            var fork = branches ? Fork() : (0, -1, -1, -1, -1);
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (fork.other >= 0 && keyboard != null)
            {
                if (keyboard.qKey.wasPressedThisFrame) Choose(fork.left);
                if (keyboard.eKey.wasPressedThisFrame) Choose(fork.right);
            }
            for (int r = 0; r < Routes.Count; r++)
            {
                if (!Open[r] || r == Active) continue;
                float parts = Divergence(r, Active);
                if (hero.S > parts + 10f)
                {
                    Open[r] = false;
                    int k = Deciding(r, Active);
                    going[r] = (Time.time, k >= 0 && !PlaneLayout.Branches[k].optional);
                }
            }
            for (int r = 0; r < ribbons.Count; r++)
            {
                var rb = ribbons[r];
                rb.flat = ribbon.flat;
                rb.pastFlat = ribbon.pastFlat;
                rb.anchor = ribbon.anchor;
                rb.stripHalfWidth = ribbon.stripHalfWidth;
                rb.global = r == Active;
                bool alive = branches ? Open[r] || going.ContainsKey(r) : r == Active;
                rb.enabled = alive;
                foreach (var part in routeStrips[r]) part.enabled = alive;
                if (r < routeMarks.Count) routeMarks[r].enabled = alive && Open[r];
                if (!alive) continue;
                if (r == Active)
                {
                    rb.branchFrom = float.MaxValue;
                    rb.storyFrom = 0f;
                    rb.tint = Color.white;
                    rb.vanish = 0f;
                    if (r < routeMarks.Count) { routeMarks[r].fromS = 0f; routeMarks[r].alpha = 1f; }
                    continue;
                }
                float at = Divergence(r, Active);
                rb.branchFrom = at;
                rb.storyFrom = at;
                rb.branchSide = Side(r, at) * branchSide;
                rb.tint = new Color(0.62f, 0.56f, 0.66f);
                rb.vanish = 0f;
                if (r < routeMarks.Count) { routeMarks[r].fromS = at + 1f; routeMarks[r].alpha = 0.6f; }
                if (going.TryGetValue(r, out var g))
                {
                    float t = Mathf.Clamp01((Time.time - g.since) / goneIn);
                    rb.vanish = Mathf.SmoothStep(0f, 1f, t);
                    rb.tint = g.burn
                        ? (t < 0.35f ? Color.Lerp(rb.tint, new Color(2.2f, 0.9f, 0.3f), t / 0.35f) : Color.Lerp(new Color(2.2f, 0.9f, 0.3f), new Color(0.05f, 0.02f, 0.02f), (t - 0.35f) / 0.65f))
                        : Color.Lerp(rb.tint, new Color(0.15f, 0.14f, 0.18f), t);
                    if (t >= 1f) going.Remove(r);
                }
            }
        }

        /// <summary>Which side of the active way another goes off to past their parting: -1 left, 1 right.</summary>
        float Side(int r, float at)
        {
            var away = Routes[r].Position(at + 60f) - Path.Position(at + 60f);
            return Vector3.Dot(away, Path.Right(at)) >= 0 ? 1f : -1f;
        }

        /// <summary>The fork ahead, if any within <see cref="offerAhead"/>: where,
        /// the branch it decides, the way on the other side of it (the active
        /// way with that branch turned), and which of the two goes left, which right.</summary>
        (float at, int branch, int other, int left, int right) Fork()
        {
            float nearest = float.MaxValue;
            int branch = -1;
            for (int r = 0; r < Routes.Count; r++)
            {
                if (!Open[r] || r == Active) continue;
                float d = Divergence(r, Active);
                if (d > hero.S + 2f && d < nearest) { nearest = d; branch = Deciding(r, Active); }
            }
            if (branch < 0 || nearest - hero.S > offerAhead) return (0, -1, -1, -1, -1);
            int other = -1;
            for (int r = 0; r < Routes.Count; r++)
            {
                if (!Open[r] || r == Active) continue;
                bool same = true;
                for (int k = 0; k < Taken[r].Length; k++) same &= (k == branch) != (Taken[r][k] == Taken[Active][k]);
                if (same) other = r;
            }
            if (other < 0) return (0, -1, -1, -1, -1);
            bool otherRight = Side(other, nearest) > 0;
            return (nearest, branch, other, otherRight ? Active : other, otherRight ? other : Active);
        }

        void OnGUI()
        {
            if (!branches) return;
            var fork = Fork();
            if (fork.other < 0) return;
            choiceStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.85f, 0.8f, 0.72f, 0.75f) } };
            choiceActive ??= new GUIStyle(choiceStyle) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.9f, 0.6f) } };
            string Name(int r) => Taken[r][fork.branch] ? PlaneLayout.Branches[fork.branch].name : PlaneLayout.Branches[fork.branch].skip;
            float w = 380f, y = Screen.height - 96f, x = Screen.width / 2f;
            GUI.Label(new Rect(x - w - 20, y, w, 32), "Q  ◂  " + Name(fork.left), fork.left == Active ? choiceActive : choiceStyle);
            GUI.Label(new Rect(x + 20, y, w, 32), Name(fork.right) + "  ▸  E", fork.right == Active ? choiceActive : choiceStyle);
            GUI.Label(new Rect(x - 200, y - 30, 400, 26), $"a fork in {Mathf.Max(0, fork.at - hero.S):0} m", choiceStyle);
        }

        public override float Outside(Vector3 at, float near)
        {
            var st = Path.Project(ToLevel(at), near, 20f);
            return Mathf.Max(0, view.Focus - behind + 2f - st.x) + Mathf.Max(0, Mathf.Abs(st.y) - (roam + 3f));
        }

        /// <summary>The world along the whole route: its ground, the road drawn
        /// on it by the strip's shader (the ribbon rises off the road ahead,
        /// and turns into film, the frames' pictures).</summary>
        IEnumerable<(string look, Mesh mesh)> Strip(StoryPath Path)
        {
            yield return ("ground", Ground(Path, -halfWidth, halfWidth, grassTexture != null ? Color.white : PlaneLayout.Grass, 24));
        }

        /// <summary>The ground along the route between two offsets across it, in
        /// columns, a row a metre, as the world's: its UVs the world's x, z over
        /// the grass's tile, its normals up; each vertex with its (s, t, 1, the
        /// road's heading) in its second UVs.</summary>
        Mesh Ground(StoryPath Path, float from, float to, Color colour, int columns)
        {
            int rows = Mathf.CeilToInt(Path.Length) + 1;
            float step = Path.Length / (rows - 1);
            int across = columns + 1;
            var vertices = new Vector3[rows * across];
            var uvs = new Vector2[vertices.Length];
            var story = new Vector4[vertices.Length];
            var triangles = new List<int>();
            for (int i = 0; i < rows; i++)
            {
                float s = i * step, heading = Path.Heading(s);
                for (int j = 0; j < across; j++)
                {
                    float t = Mathf.Lerp(from, to, (float)j / columns);
                    var p = Path.At(s, t, 0f);
                    p.y += Land.Height(p.x, p.z);
                    vertices[i * across + j] = p;
                    story[i * across + j] = new Vector4(s, t, 1, heading);
                    uvs[i * across + j] = new Vector2(p.x, p.z) / grassTile;
                }
                if (i == rows - 1) continue;
                for (int j = 0; j < columns; j++)
                {
                    int a = i * across + j, b = a + across;
                    triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            }
            var mesh = new Mesh { name = "Ground", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.SetUVs(1, story);
            var colours = new Color[vertices.Length];
            for (int i = 0; i < colours.Length; i++) colours[i] = colour;
            mesh.colors = colours;
            mesh.SetTriangles(triangles, 0);
            var normals = new Vector3[vertices.Length];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals;
            return mesh;
        }
    }
}
