using System.Collections.Generic;
using UnityEngine;

namespace FinishTheGame
{
    /// <summary>The ribbon (Ribbon.hlsl draws the world along it). Past the
    /// camera's focus by <see cref="flat"/> metres along the story's curve the
    /// world leaves its place: the rest of the way is pressed into a curve of
    /// length <see cref="arc"/>, ever smaller (a place <see cref="arc"/> metres
    /// on is drawn at 1/e of its size). The curve starts as the road does
    /// there, on the ground, turning as it turns; it rises (<see cref="rise"/>
    /// over <see cref="riseLength"/>), and once up it turns to the camera's
    /// heading (<see cref="straighten"/>), so the way ahead always rises in
    /// front of the camera, whichever way the road goes. (Turned while still
    /// on the ground, its wide edges would fold over: a turn of the risen
    /// ribbon is a twist about its length, not a bend.) Its tip rolls up
    /// (<see cref="curl"/>), it sways and twists slowly. Behind, past
    /// <see cref="pastFlat"/>, the world shrinks away into a point under the
    /// ground. Sampled each frame for the shaders.
    ///
    /// Or, <see cref="sky"/>: the ribbon over the world, not bound to the
    /// camera: from the waking world into the dream. A little ahead
    /// (<see cref="Start"/>) the road itself, with its verges, as it is
    /// (the world there, the same, its relief, a metre of the story a metre),
    /// goes on along its own turns (<see cref="follow"/>) and slowly lifts
    /// off the ground (<see cref="rampLength"/>, to <see cref="rampPitch"/>);
    /// its relief is gone soon after (<see cref="reliefTo"/>), and over the
    /// way up it narrows and the story on it runs ever faster
    /// (<see cref="settleTo"/>) to the dream's measure; a bridge carries it on
    /// into a funnel round the hero (<see cref="anchor"/>), always the same
    /// way (counter-clockwise seen from above): its first turn the smallest
    /// and lowest to the eye, each further one a little wider and higher
    /// (<see cref="turns"/>, <see cref="funnelRadius"/>, <see cref="funnelRise"/>),
    /// wavy (<see cref="wobble"/>, <see cref="sag"/>), streaming in the wind
    /// (<see cref="flutter"/>), out to the story's end. Stood on edge, its face
    /// to him. Up there it is a strip of film, each frame
    /// (<see cref="frameLength"/> of the story, as long as
    /// <see cref="frameAspect"/> widths) a picture of its stretch; between, a
    /// memory (StripLit: <see cref="memory"/>, <see cref="filmIn"/>). The
    /// shaders know the sky by <c>_RibbonWindow.w</c>; the curve's own metres
    /// and width along it come in its fourth row.</summary>
    [DefaultExecutionOrder(100)]
    public class RibbonBend : MonoBehaviour
    {
        const int Samples = 1024;   // RIBBON_SAMPLES in Ribbon.hlsl

        public StoryWorld world;
        public ForwardCamera view;
        /// <summary>The flat ground ahead of the focus, and the curve's length, m.</summary>
        public float flat = 16f, arc = 40f;
        /// <summary>The curve's stretch over which it turns to the camera's heading, m: from, to.</summary>
        public Vector2 straighten = new Vector2(10f, 26f);
        /// <summary>Degrees risen, over metres of the curve; degrees the tip rolls beyond.</summary>
        public float rise = 85f, riseLength = 14f, curl = 60f;
        /// <summary>The heights' share kept once risen: a relief, so that tall
        /// things do not fold over on the rise (h × curvature &lt; 1).</summary>
        public float relief = 0.3f;
        /// <summary>Degrees at the tip: the sway to the sides, the twist about itself.</summary>
        public float sway = 25f, twist = 35f;
        /// <summary>Its motion's rate, cycles a second, and how much the rise breathes.</summary>
        public float speed = 0.05f, breathe = 0.08f;
        /// <summary>Behind the focus: the ground kept, m; the shrinking's length, m;
        /// how deep under the ground it shrinks to, m; how far back the past reaches (the rest of the loop is ahead), m.</summary>
        public float pastFlat = 30f, pastShrink = 8f, pastSink = 20f, pastWindow = 80f;

        /// <summary>The ribbon over the world, round its anchor (see the summary).</summary>
        public bool sky;
        public Transform anchor;
        /// <summary>The sky's ribbon's start: this far over the world (it lies
        /// on it there, the same, and must not fight it), m, and its width's
        /// share there (the road and its verges).</summary>
        public float above = 0.15f, startWidth = 0.3f;
        /// <summary>The way up: along the road's own turns for this far, m;
        /// level for this far, m; up to this far, m, rising to this steepest,
        /// degrees.</summary>
        public float follow = 60f, level = 30f, rampLength = 220f, rampPitch = 22f;
        /// <summary>Along the ribbon, m: where its relief (the world's heights
        /// on it) is gone; where its width and the story's pace on it start
        /// changing, and where they are the dream's.</summary>
        public float reliefTo = 70f, settleFrom = 60f, settleTo = 330f;
        /// <summary>Along the ribbon, m: the world turning to a memory (its
        /// tones, its pixels, lights), from, to; the film coming in, from, to.</summary>
        public Vector2 memory = new Vector2(90f, 330f), filmIn = new Vector2(330f, 460f);
        /// <summary>How far round (radians) ahead of the ramp's end the funnel
        /// starts; how long its start takes to follow the ramp's, s.</summary>
        public float lead = 0.6f, settle = 1.5f;
        /// <summary>The funnel: its turns; its radius at the start (at least:
        /// else where the ramp ends) and after the turns, m, and how it widens
        /// (the share of the turns gone, to this power); how high it is seen
        /// from the anchor, degrees over the horizon, at the start and after
        /// the turns (so that the camera, looking about, has it in sight), and
        /// never lower than this, m (the bridge climbs to it).</summary>
        public float turns = 2.3f;
        public Vector2 funnelRadius = new Vector2(250f, 1100f);
        public float widen = 1.2f;
        public Vector2 funnelRise = new Vector2(9f, 18f);
        public float funnelBase = 40f;
        /// <summary>Its waves: in and out, a share of its radius, and up and
        /// down, m; their length along it, m.</summary>
        public float wobble = 0.1f, sag = 20f, windLength = 400f;
        /// <summary>It streams in the wind: waves running along it, up and
        /// down and to the side, m, this long, m, this fast, m a second; and it
        /// turns about itself, degrees. Calm where it comes out of the ground,
        /// full once up (over the descent).</summary>
        public float flutter = 10f, flutterLength = 280f, flutterSpeed = 30f, flutterTwist = 25f;
        /// <summary>Along the ribbon, m: the wind starts taking it, and has it whole.</summary>
        public Vector2 calm = new Vector2(220f, 520f);
        /// <summary>The ribbon's width, as a share of the strip's.</summary>
        public float skyWidth = 0.09f;
        /// <summary>The strip's half width, m (its width's share is of twice this).</summary>
        public float stripHalfWidth = 50f;
        /// <summary>A frame of the film: its length of the story, m, and its
        /// length on the ribbon as a share of the ribbon's width there.</summary>
        public float frameLength = 20f, frameAspect = 3f;
        /// <summary>The ribbon's metres a metre of the story up in the dream: a frame as long as frameAspect widths.</summary>
        public float Pace => frameAspect * 2f * stripHalfWidth * skyWidth / frameLength;

        /// <summary>Where the flat part ends, s (unwrapped).</summary>
        public float Start => view.Focus + flat;

        /// <summary>Which way through the story it carries (-1: the active one).</summary>
        public int route = -1;
        public StoryPath Route => route < 0 || route >= world.Routes.Count ? world.Path : world.Routes[route];
        /// <summary>The strips it lays (their own ribbon, per renderer); and
        /// whether it also sets the shaders' globals (the active way's).</summary>
        public List<Renderer> renderers = new List<Renderer>();
        public bool global = true;
        /// <summary>A branch off the active way: from where it parts, s, it goes
        /// off to the side (metres, signed: right), over <see cref="branchSpread"/> metres of the curve.</summary>
        public float branchFrom = float.MaxValue, branchSide, branchSpread = 220f;
        /// <summary>Gone (0–1): it rises away and thins to nothing; its colour (tint).</summary>
        public float vanish;
        public Color tint = Color.white;
        /// <summary>Its strips are drawn from this s on (the shared road before it is the active way's).</summary>
        public float storyFrom;
        MaterialPropertyBlock block;
        static readonly int From = Shader.PropertyToID("_StoryFrom");
        static readonly int Tint = Shader.PropertyToID("_Tint");

        readonly Vector4[] points = new Vector4[Samples], tangents = new Vector4[Samples], sides = new Vector4[Samples], measures = new Vector4[Samples];

        /// <summary>The curve as last sampled, evenly along its length (the
        /// last a little short of it, but in the sky): points (w, the heights' share kept),
        /// tangents (w, metres of curve a metre of a: 1 but in the sky), and
        /// right-hand sides (w, the width's share kept).</summary>
        public int Count => Samples;
        public Vector4 Point(int i) => points[i];
        public Vector3 SideAt(int i) => sides[i];
        /// <summary>The width's share kept, and metres of curve a metre of its measure, at sample i.</summary>
        public float WidthAt(int i) => sides[i].w;
        public float RunAt(int i) => tangents[i].w;
        /// <summary>The curve's length at sample i, m.</summary>
        public float ArcAt(int i) => arc * Mathf.Min((float)i / (Samples - 1), sky ? 1f : 0.995f);

        /// <summary>The story's place (s) at a metres along the curve: as the
        /// shaders lay the strip, u = −arc ln(1 − a/arc) past <see cref="Start"/>;
        /// or, in the sky, as it runs: a = u (<see cref="arc"/>, the story left).</summary>
        public float StoryAt(float a) => sky ? Start + a : Start + Pressed(a);

        /// <summary>Where along the curve the story's place s lies (the inverse of <see cref="StoryAt"/>).</summary>
        public float ArcOf(float s)
        {
            float u = s - Start;
            return sky ? Mathf.Clamp(u, 0, arc) : arc * (1f - Mathf.Exp(-Mathf.Max(0, u) / arc));
        }

        /// <summary>The sample index where the story's place s lies on the ribbon.</summary>
        public float AlongOf(float s) => ArcOf(s) / Mathf.Max(1e-3f, ArcAt(Samples - 1)) * (Samples - 1);

        /// <summary>The story's length past a metres of the curve, as the shaders press it.</summary>
        float Pressed(float a) => -arc * Mathf.Log(1f - Mathf.Min(a, 0.995f * arc) / arc);
        /// <summary>The story left past the ribbon's start (a loop never ends: far).</summary>
        float Rest => Route.Closed ? 1e6f : Mathf.Max(0, Route.Length - Start);

        static readonly int Params = Shader.PropertyToID("_RibbonParams");
        static readonly int Past = Shader.PropertyToID("_RibbonPast");
        static readonly int Window = Shader.PropertyToID("_RibbonWindow");
        static readonly int Curve = Shader.PropertyToID("_RibbonCurveMap");
        static readonly int Dream = Shader.PropertyToID("_RibbonDream");
        /// <summary>The curve for the shaders: a row of points, of tangents, of
        /// sides, of its measures (in the sky: its own metres, its width's share).</summary>
        Texture2D curve;
        readonly Vector4[] texels = new Vector4[Samples * 4];
        static readonly int Turn = Shader.PropertyToID("_StoryTurn");

        void LateUpdate()
        {
            if (sky) Sky();
            else Bend();
            var path = Route;
            var sunk = path.Position(view.Focus - pastFlat) + Vector3.down * pastSink;
            // An open route has no loop to go round: a length no ds reaches.
            var parameters = new Vector4(view.Focus, arc, flat, path.Closed ? path.Length : 1e6f);
            var past = new Vector4(sunk.x, sunk.y, sunk.z, 0);
            var window = new Vector4(pastWindow, pastFlat, pastShrink, sky ? 1 : 0);
            if (global)
            {
                Shader.SetGlobalVector(Params, parameters);
                Shader.SetGlobalVector(Past, past);
                Shader.SetGlobalVector(Window, window);
            }
            if (curve == null) curve = new Texture2D(Samples * 4, 1, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < Samples; i++)
            {
                texels[i] = points[i];
                texels[Samples + i] = tangents[i];
                texels[2 * Samples + i] = sides[i];
                texels[3 * Samples + i] = measures[i];
            }
            // Raw floats: SetPixels would clamp them to 0–1.
            curve.SetPixelData(texels, 0);
            curve.Apply(false);
            if (global)
            {
                Shader.SetGlobalTexture(Curve, curve);
                Shader.SetGlobalVector(Dream, new Vector4(memory.x, memory.y, filmIn.x, filmIn.y));
                Shader.SetGlobalFloat(Turn, world.Turned);
            }
            block ??= new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetVector(Params, parameters);
                block.SetVector(Past, past);
                block.SetVector(Window, window);
                block.SetTexture(Curve, curve);
                block.SetFloat(From, storyFrom);
                block.SetColor(Tint, tint);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>The funnel's centre (the anchor, on the ground), where it
        /// starts round it (radians), and its radius there, followed smoothly.</summary>
        Vector3 middle;
        float startAngle, startRadius, turning;
        bool started;
        const int Funnel = 2048;
        readonly Vector3[] funnel = new Vector3[Funnel];
        /// <summary>Along the funnel: its length, m.</summary>
        readonly float[] funnelRun = new float[Funnel];

        /// <summary>The funnel from its start round the anchor: its points, and
        /// the length and the story along it.</summary>
        void Spiral()
        {
            float Noise(float v, float length, float kind) => 2f * Mathf.PerlinNoise(v / length, kind) - 1f;
            float Wind(float v, float kind) => 0.7f * Noise(v, windLength, kind) + 0.3f * Noise(v, windLength / 2.3f, kind + 5.1f);
            // Turns beyond the last, if the story runs on past them.
            float angle = 2f * Mathf.PI * turns * 2.5f, travelled = 0;
            for (int j = 0; j < Funnel; j++)
            {
                float phi = angle * j / (Funnel - 1), x = phi / (2f * Mathf.PI * turns);
                float plain = Mathf.LerpUnclamped(startRadius, funnelRadius.y, Mathf.Pow(x, widen));
                if (j > 0) travelled += plain * angle / (Funnel - 1);
                // The waves come in after the first quarter turn: it leaves the ramp smoothly.
                float waves = Mathf.SmoothStep(0f, 1f, x * turns * 4f);
                float radius = plain * (1f + waves * wobble * Wind(travelled, 3.3f));
                float seen = Mathf.Lerp(funnelRise.x, funnelRise.y, Mathf.Clamp01(x)) * Mathf.Deg2Rad;
                float height = Mathf.Max(funnelBase, plain * Mathf.Tan(seen)) + waves * sag * Wind(travelled, 7.7f);
                float theta = startAngle + phi;
                funnel[j] = middle + new Vector3(Mathf.Cos(theta) * radius, height, Mathf.Sin(theta) * radius);
                funnelRun[j] = j > 0 ? funnelRun[j - 1] + Vector3.Distance(funnel[j - 1], funnel[j]) : 0f;
            }
        }

        /// <summary>The ribbon over the world (see the summary): one curve, along
        /// the road and up off it, a Hermite bridge from the ramp's end into the
        /// funnel, the funnel; the story laid along it at a pace that runs from
        /// the world's (a metre a metre) to the dream's.</summary>
        void Sky()
        {
            var path = Route;
            arc = Mathf.Max(1f, Rest);
            middle = anchor.position;
            middle.y = 0;
            float pace = Pace;

            // The ramp: along the road's own turns at first, level, then
            // straight on, rising; walked a few metres a step.
            const int RampSteps = 110, BridgeSteps = 120;
            var line = new List<Vector3>(RampSteps + BridgeSteps + Funnel);
            var level0 = path.Position(Start);
            var at = world.FromLevel(level0);
            at.y = above;
            line.Add(at);
            float step = rampLength / RampSteps;
            Vector3 rampWay = Vector3.forward;
            for (int k = 1; k <= RampSteps; k++)
            {
                float a = (k - 0.5f) * step;
                var road = world.FromLevelWay(path.Tangent(Start + Mathf.Min(a, follow)));
                road.y = 0;
                road.Normalize();
                float pitch = Mathf.Deg2Rad * rampPitch * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(level, rampLength, a));
                rampWay = road * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch);
                at += rampWay * step;
                line.Add(at);
            }
            var rampRight = new Vector3(rampWay.z, 0, -rampWay.x).normalized;
            var from = at;
            int rampEnd = line.Count - 1;

            // The funnel starts a little round ahead of the ramp's end, as far
            // out; followed smoothly, so that a leap of the ramp (where the road
            // loops) is taken up by the bridge.
            var off = from - middle;
            off.y = 0;
            float targetAngle = Mathf.Atan2(off.z, off.x) + lead, targetRadius = Mathf.Max(funnelRadius.x, off.magnitude);
            if (!started)
            {
                startAngle = targetAngle;
                startRadius = targetRadius;
                started = true;
            }
            else
            {
                startAngle = Mathf.Deg2Rad * Mathf.SmoothDampAngle(startAngle * Mathf.Rad2Deg, targetAngle * Mathf.Rad2Deg, ref turning, settle);
                startRadius = Mathf.Lerp(startRadius, targetRadius, 1f - Mathf.Exp(-Time.deltaTime / settle));
            }
            Spiral();
            // The bridge, on from the ramp, long into the turn's own way: no corner.
            var joinAt = funnel[0];
            var joinWay = (funnel[1] - funnel[0]).normalized;
            float span = Vector3.Distance(from, joinAt);
            for (int k = 1; k < BridgeSteps; k++)
            {
                float x = (float)k / BridgeSteps, x2 = x * x, x3 = x2 * x;
                line.Add((2 * x3 - 3 * x2 + 1) * from + (x3 - 2 * x2 + x) * 0.8f * span * rampWay + (-2 * x3 + 3 * x2) * joinAt + (x3 - x2) * 1.4f * span * joinWay);
            }
            int funnelFrom = line.Count;
            line.AddRange(funnel);

            // Its own metres, and the story's along it: a metre a metre at
            // first, then ever faster, to the dream's pace.
            int n = line.Count;
            var run = new float[n];
            var story = new float[n];
            for (int k = 1; k < n; k++)
            {
                float d = Vector3.Distance(line[k - 1], line[k]);
                run[k] = run[k - 1] + d;
                float faster = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settleFrom, settleTo, run[k] - d / 2f));
                story[k] = story[k - 1] + d / Mathf.Lerp(1f, pace, faster);
            }
            float bridgeFrom = run[rampEnd], bridgeTo = run[funnelFrom];

            var placed = new Vector3[Samples];
            var across = new Vector3[Samples];
            var along = new float[Samples];
            int j = 0;
            for (int i = 0; i < Samples; i++)
            {
                float u = ArcAt(i);
                while (j < n - 2 && story[j + 1] < u) j++;
                float f = Mathf.Clamp01(Mathf.InverseLerp(story[j], story[j + 1], u));
                placed[i] = Vector3.Lerp(line[j], line[j + 1], f);
                float c = along[i] = Mathf.Lerp(run[j], run[j + 1], f);
                // Over the bridge it stands up on edge: its side up, its face (tangent × side) to the anchor.
                float roll = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(bridgeFrom, bridgeTo, c));
                var right = j < rampEnd ? new Vector3(line[j + 1].z - line[j].z, 0, line[j].x - line[j + 1].x).normalized : rampRight;
                across[i] = Vector3.Slerp(right, Vector3.up, roll);
            }
            // In the wind: waves running up along it, still near the ground.
            float time = Time.time * flutterSpeed;
            float Gust(float c, float kind) => 2f * Mathf.PerlinNoise((c - time) / flutterLength, kind) - 1f;
            var still = (Vector3[])placed.Clone();
            for (int i = 0; i < Samples; i++)
            {
                float c = along[i], wind = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(calm.x, calm.y, c));
                if (wind <= 0f) continue;
                var tangent = still[Mathf.Min(i + 1, Samples - 1)] - still[Mathf.Max(i - 1, 0)];
                var aside = Vector3.Cross(Vector3.up, tangent).normalized;
                placed[i] += wind * flutter * (Vector3.up * Gust(c, 11.3f) + aside * 0.7f * Gust(c, 23.9f));
                if (tangent.sqrMagnitude > 1e-6f)
                    across[i] = Quaternion.AngleAxis(wind * flutterTwist * Gust(c, 37.1f), tangent.normalized) * across[i];
            }
            // A branch: the same as the active way's ribbon up to where they
            // part, then off outward; gone, it rises away from there.
            float apart = branchFrom < float.MaxValue / 2 ? Mathf.Max(ArcOf(branchFrom), rampLength) : float.MaxValue;
            for (int i = 0; i < Samples; i++)
            {
                float a = ArcAt(i);
                if (a <= apart) continue;
                float k = Mathf.SmoothStep(0f, 1f, (a - apart) / branchSpread);
                var outward = placed[i] - middle;
                outward.y = 0;
                placed[i] += outward.normalized * (branchSide * k) + Vector3.up * (k * (0.15f * Mathf.Abs(branchSide) + vanish * 220f));
            }
            for (int i = 0; i < Samples; i++)
            {
                int i0 = Mathf.Max(0, i - 1), i1 = Mathf.Min(Samples - 1, i + 1);
                var way = placed[i1] - placed[i0];
                // The shaders scale the strip by its run: the widths and heights are kept as given.
                float runs = Mathf.Max(1e-3f, way.magnitude / Mathf.Max(1e-3f, ArcAt(i1) - ArcAt(i0)));
                float c = along[i];
                // The world's heights on it at first (it is the ground there), soon none.
                float heights = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f * reliefTo, reliefTo, c));
                float share = Mathf.Lerp(startWidth, skyWidth, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settleFrom, settleTo, c)));
                points[i] = new Vector4(placed[i].x, placed[i].y, placed[i].z, heights / runs);
                var tangent = way.normalized;
                tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, runs);
                sides[i] = new Vector4(across[i].x, across[i].y, across[i].z, (1f - vanish) * share / runs);
                measures[i] = new Vector4(c, share, 0, 0);
            }
        }

        /// <summary>The ribbon from the road's end of the flat part.</summary>
        void Bend()
        {
            var path = world.Path;
            float time = Time.time * speed * 2f * Mathf.PI;
            float start = Start;
            float cameraHeading = view.Heading * Mathf.Deg2Rad;
            var at = path.Position(start);
            float road = path.Heading(start);
            float lastRoad = road;
            cameraHeading = road + Mathf.DeltaAngle(road * Mathf.Rad2Deg, cameraHeading * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            Vector3 lastTangent = Vector3.zero;
            for (int i = 0; i < Samples; i++)
            {
                float t = (float)i / (Samples - 1);
                float a = arc * Mathf.Min(t, 0.995f);
                // The road's own heading where this point of the curve comes from, unwound.
                float u = -arc * Mathf.Log(1f - a / arc);
                float heading = path.Heading(start + u);
                road += Mathf.DeltaAngle(lastRoad * Mathf.Rad2Deg, heading * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                lastRoad = heading;
                float toCamera = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(straighten.x, straighten.y, a));
                float yaw = Mathf.Lerp(road, cameraHeading, toCamera) + Mathf.Deg2Rad * sway * t * t * Mathf.Sin(time + t * 2f);
                float lift = Mathf.SmoothStep(0f, 1f, a / riseLength);
                float pitch = Mathf.Deg2Rad * (rise * (1 + breathe * Mathf.Sin(time)) * lift + curl * t * t * t);
                float roll = Mathf.Deg2Rad * twist * t * t * Mathf.Sin(time * 0.6f + 1f);

                var tangent = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));
                var side = new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw));
                var up = Vector3.Cross(tangent, side);
                side = side * Mathf.Cos(roll) + up * Mathf.Sin(roll);
                if (i > 0) at += (lastTangent + tangent) * (0.5f * arc / (Samples - 1));
                lastTangent = tangent;
                points[i] = new Vector4(at.x, at.y, at.z, Mathf.Lerp(1f, relief, lift));
                tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, 1f);
                sides[i] = new Vector4(side.x, side.y, side.z, 1f);
            }
        }
    }
}
