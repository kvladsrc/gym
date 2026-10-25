using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FinishTheGame
{
    /// <summary>The plane: an ordinary level (<see cref="PlaneLayout"/>), never
    /// bent, and the story's route over it, which comes back to places. It is
    /// seen only through the band along the route round the camera's focus
    /// (Band.hlsl: from <see cref="behind"/> metres back to the ribbon's
    /// start ahead, <see cref="halfWidth"/> to either side); the way on is
    /// the horizon's ribbon (<see cref="HorizonRibbon"/>), showing the route
    /// unrolled: a picture made once, from above, of the band along the whole
    /// route, a place in it as often as the route passes it.</summary>
    [DefaultExecutionOrder(-100)]
    public class PlaneWorld : StoryWorld
    {
        const int BandPoints = 64;   // BAND_POINTS in Band.hlsl

        public RibbonBend ribbon;
        public HorizonRibbon horizon;
        /// <summary>The band: how far back it reaches along the route, m; its
        /// half width and that width's fade, m; the fades past its ends, m.</summary>
        public float behind = 30f, halfWidth = 30f, sideFade = 8f, behindFade = 10f, aheadFade = 8f;
        /// <summary>How far past <see cref="Ahead"/> the band still reaches, m (the strip laid on the ribbon takes over).</summary>
        public float bandBeyond;
        /// <summary>The pictures' metres a pixel: the unrolled route's; and the
        /// view from above's pixels a side.</summary>
        public float texel = 0.25f;
        public int abovePixels = 2048;

        public override float Ahead => ribbon.flat;
        public override float Behind => behind;
        public override float Horizon => ribbon.arc;
        public override Texture2D MapImage => map;
        public override Rect MapArea => new Rect(-PlaneLayout.Story, -PlaneLayout.Story, 2 * PlaneLayout.Story, 2 * PlaneLayout.Story);

        /// <summary>The models (the studio's), the ground's and the road's tiled textures, and their tiles, m.</summary>
        public Model[] models;
        public Texture2D grassTexture, roadTexture;
        public float grassTile = 8f, roadTile = 5f;
        /// <summary>The layer of the world's things, and how far the camera draws them, m (past it, the haze).</summary>
        public const int WorldLayer = 6;
        public float drawDistance = 450f;

        /// <summary>With a navmesh (for walking to a click round things), over the story's middle.</summary>
        public bool navigation = true;
        /// <summary>The navmesh (if any), laid where the level is when it is moved; the colliders' root.</summary>
        protected NavMeshSurface Surface { get; private set; }
        protected Transform Colliders { get; private set; }
        /// <summary>The thing of a model nearest a place of the level (x, z), if any.</summary>
        public bool Nearest(string model, Vector2 near, out Prop found)
        {
            found = default;
            float best = float.MaxValue;
            foreach (var prop in Props)
            {
                if (prop.model != model) continue;
                float d = Vector2.Distance(new Vector2(prop.at.x, prop.at.z), near);
                if (d < best)
                {
                    best = d;
                    found = prop;
                }
            }
            return best < float.MaxValue;
        }

        /// <summary>The level's boxes.</summary>
        protected List<Prop> Props { get; private set; }

        Texture2D map;
        readonly Vector4[] band = new Vector4[BandPoints];

        static readonly int Points = Shader.PropertyToID("_BandPoints");
        static readonly int Count = Shader.PropertyToID("_BandCount");
        static readonly int Width = Shader.PropertyToID("_BandWidth");
        static readonly int Ends = Shader.PropertyToID("_BandEnds");
        static readonly int Disk = Shader.PropertyToID("_BandDisk");

        protected virtual void Awake()
        {
            foreach (var (taken, waypoints) in PlaneLayout.Routes())
            {
                Routes.Add(StoryPath.Through(waypoints));
                Taken.Add(taken);
                Open.Add(true);
            }
            Path = Routes[0];
            var props = Props = PlaneLayout.Make(Path, models, Routes.GetRange(1, Routes.Count - 1));

            float half = PlaneLayout.Half;
            var index = ModelSet.Index(models);
            var groundMesh = MeshKit.Grid(2 * half, Mathf.CeilToInt(2 * half / 6f), grassTexture != null ? Color.white : PlaneLayout.Grass, grassTile);
            // One mesh and one material for each look: the ground, the road,
            // each model (its texture), and the boxes (their vertex colours).
            var looks = new Dictionary<string, List<CombineInstance>>
            {
                ["ground"] = new List<CombineInstance> { new CombineInstance { mesh = groundMesh, transform = Matrix4x4.identity } },
                ["road"] = new List<CombineInstance> { new CombineInstance { mesh = Roads(), transform = Matrix4x4.identity } },
                ["boxes"] = new List<CombineInstance>(),
            };
            var things = new GameObject("Things").transform;
            things.SetParent(transform, false);
            var solid = Colliders = new GameObject("Colliders").transform;
            solid.SetParent(transform, false);
            // The ground's own shape (its relief).
            solid.gameObject.AddComponent<MeshCollider>().sharedMesh = groundMesh;

            // Colliders only where the story is: the land beyond is only seen.
            float reach = PlaneLayout.Story + 60f;
            var white = new Dictionary<string, Mesh>();
            foreach (var prop in props)
            {
                bool modelled = prop.model != null && index.ContainsKey(prop.model);
                Mesh mesh;
                Matrix4x4 place;
                GameObject drawn = null;
                if (modelled)
                {
                    // Each model its own object: culled by the frustum and its
                    // distance, drawn instanced, simplified far off (its mesh LODs).
                    var model = index[prop.model];
                    if (!white.TryGetValue(prop.model, out mesh)) white[prop.model] = mesh = MeshKit.White(model.mesh);
                    place = ModelSet.Place(prop, model);
                    drawn = new GameObject(prop.name) { layer = WorldLayer };
                    drawn.transform.SetParent(things, false);
                    drawn.transform.SetPositionAndRotation(place.GetPosition(), place.rotation);
                    drawn.transform.localScale = place.lossyScale;
                    drawn.AddComponent<MeshFilter>().sharedMesh = mesh;
                    drawn.AddComponent<MeshRenderer>().sharedMaterial = Look(material, prop.model, index);
                }
                else
                {
                    mesh = MeshKit.Box(prop.size, prop.colour, 1000f);
                    place = prop.Place;
                    looks["boxes"].Add(new CombineInstance { mesh = mesh, transform = place });
                }
                if (!prop.solid || Mathf.Abs(prop.at.x) > reach || Mathf.Abs(prop.at.z) > reach) continue;
                if (modelled)
                {
                    // Its collider on a child of the colliders (the level's moving body).
                    var shape = new GameObject(prop.name);
                    shape.transform.SetParent(solid, false);
                    shape.transform.SetPositionAndRotation(place.GetPosition(), place.rotation);
                    shape.transform.localScale = place.lossyScale;
                    shape.AddComponent<MeshCollider>().sharedMesh = mesh;
                    continue;
                }
                var go = new GameObject(prop.name);
                go.transform.SetParent(solid, false);
                go.transform.SetPositionAndRotation(place.GetPosition(), place.rotation);
                var box = go.AddComponent<BoxCollider>();
                box.size = prop.size;
                box.center = new Vector3(0, prop.size.y / 2, 0);
            }

            foreach (var (look, parts) in looks)
            {
                if (parts.Count == 0) continue;
                var combined = new Mesh { name = look, indexFormat = IndexFormat.UInt32 };
                combined.CombineMeshes(parts.ToArray(), true, true);
                var go = new GameObject(look);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = combined;
                go.AddComponent<MeshRenderer>().sharedMaterial = Look(material, look, index);
            }

            var eye = view.GetComponent<Camera>();
            var far = new float[32];
            far[WorldLayer] = drawDistance;
            eye.layerCullDistances = far;
            eye.layerCullSpherical = true;
            if (navigation) Navigate(solid, reach);
            hero.Place(20f);
        }

        /// <summary>The material of a look: the base one, with the look's texture
        /// (the ground's, the road's, a model's), made once.</summary>
        protected Material Look(Material from, string look, Dictionary<string, Model> index)
        {
            var key = (from, look);
            if (madeLooks.TryGetValue(key, out var made)) return made;
            Texture2D texture = look switch
            {
                "ground" => grassTexture,
                "road" => roadTexture,
                _ => index.TryGetValue(look, out var model) ? model.texture : null,
            };
            made = texture == null ? from : new Material(from) { name = $"{from.name} {look}", mainTexture = texture };
            // The road's earth, drawn too bright and orange, toned down to sit in the grass.
            if (look == "road" && made != from)
            {
                made.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.84f));
                // Its ragged edge blended into the ground: drawn after it, over it.
                made.SetVector("_RoadEdge", new Vector4(2.6f, 1.1f, 0.8f, 1f));
                made.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                made.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                made.SetFloat("_ZWrite", 0f);
                made.renderQueue = (int)RenderQueue.GeometryLast;
            }
            if (made != from) made.enableInstancing = true;
            madeLooks[key] = made;
            return made;
        }

        readonly Dictionary<(Material, string), Material> madeLooks = new Dictionary<(Material, string), Material>();

        void Navigate(Transform solid, float reach)
        {
            var surface = Surface = solid.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = Vector3.zero;
            surface.size = new Vector3(2 * reach, 60f, 2 * reach);
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.4f;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
        }

        protected virtual void Start()
        {
            var above = Above();
            map = Mapped(above);
            if (horizon == null) return;
            horizon.picture = Unrolled(above);
            horizon.halfWidth = halfWidth;
        }

        /// <summary>Only a band along the route is drawn (Band.hlsl); else all the world.</summary>
        protected virtual bool Banded => true;

        protected virtual void LateUpdate()
        {
            // The route from behind the focus to the ribbon's start, evenly.
            float from = Path.Wrap(view.Focus - behind), to = Path.Wrap(view.Focus + Ahead + bandBeyond);
            int n = Mathf.Clamp(Mathf.CeilToInt((to - from) / 2f) + 1, 2, BandPoints);
            for (int i = 0; i < n; i++)
                band[i] = FromLevel(Path.Position(Mathf.Lerp(from, to, (float)i / (n - 1))));
            Shader.SetGlobalVectorArray(Points, band);
            Shader.SetGlobalFloat(Count, Banded ? n : 0);
            Shader.SetGlobalVector(Width, new Vector4(halfWidth, sideFade, 0, 0));
            Shader.SetGlobalVector(Ends, new Vector4(behindFade, aheadFade, 0, 0));
            Shader.SetGlobalVector(Disk, Vector4.zero);
        }

        /// <summary>How far the road's mesh reaches either side of its middle, m (its ragged edge within).</summary>
        const float RoadSpan = 4.4f;

        /// <summary>The roads along the routes, a row of quads a metre; where they
        /// pass again or run together, laid over each other (blended, they do
        /// not fight; skipped, a branch would start with a straight cut).</summary>
        Mesh Roads()
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var across = new List<Vector2>();
            foreach (var Path in Routes)
            for (float s = 0; s + 1f <= Path.Length; s += 1f)
            {
                int first = vertices.Count;
                // Wider than the road: its ragged edge is drawn within (BandLit's RoadCover).
                foreach (var ds in new[] { 0f, 1f })
                    foreach (var dt in new[] { -RoadSpan, -RoadSpan / 3f, RoadSpan / 3f, RoadSpan })
                    {
                        var v = Path.At(s + ds, dt, 0.04f);
                        v.y += Land.Height(v.x, v.z);
                        vertices.Add(v);
                        uvs.Add(new Vector2(0.5f + dt / 5f, (s + ds) / roadTile));
                        across.Add(new Vector2(dt, 0));
                    }
                for (int c = 0; c < 3; c++)
                    triangles.AddRange(new[] { first + c, first + 4 + c, first + c + 1, first + c + 1, first + 4 + c, first + 5 + c });
            }
            var mesh = new Mesh { name = "Roads", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            var colours = new Color[vertices.Count];
            for (int i = 0; i < colours.Length; i++) colours[i] = roadTexture != null ? Color.white : PlaneLayout.Road;
            mesh.colors = colours;
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, across);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>The story's middle of the plane from straight above, lit, without the band, the fog or the hero.</summary>
        Texture2D Above()
        {
            float half = PlaneLayout.Story;
            var eye = new GameObject("Above").AddComponent<Camera>();
            eye.enabled = false;
            eye.orthographic = true;
            eye.orthographicSize = half;
            eye.aspect = 1f;
            eye.transform.SetPositionAndRotation(new Vector3(0, 100, 0), Quaternion.Euler(90, 0, 0));
            eye.nearClipPlane = 1f;
            eye.farClipPlane = 200f;
            eye.clearFlags = CameraClearFlags.SolidColor;
            eye.backgroundColor = new Color(0.12f, 0.14f, 0.12f);
            var target = new RenderTexture(abovePixels, abovePixels, 24);

            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            Shader.SetGlobalFloat(Count, 0);
            hero.gameObject.SetActive(false);
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(eye, request))
                RenderPipeline.SubmitRenderRequest(eye, request);
            hero.gameObject.SetActive(true);
            RenderSettings.fog = fog;

            var image = new Texture2D(abovePixels, abovePixels, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var active = RenderTexture.active;
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, abovePixels, abovePixels), 0, 0);
            image.Apply();
            RenderTexture.active = active;
            target.Release();
            Destroy(eye.gameObject);
            return image;
        }

        /// <summary>The map: the view from above with the route drawn on it.</summary>
        Texture2D Mapped(Texture2D above)
        {
            var image = new Texture2D(above.width, above.height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = above.GetPixels();
            float metre = above.width / (2f * PlaneLayout.Story);
            var line = new Color(0.95f, 0.9f, 0.7f, 1f);
            for (float s = 0; s <= Path.Length; s += 0.5f)
            {
                var p = Path.Position(s);
                int cx = (int)((p.x + PlaneLayout.Story) * metre), cy = (int)((p.z + PlaneLayout.Story) * metre);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (x >= 0 && y >= 0 && x < image.width && y < image.height) pixels[y * image.width + x] = line;
                    }
            }
            image.SetPixels(pixels);
            image.Apply();
            return image;
        }

        /// <summary>The band along the whole route, from above, unrolled: u along the route, v across it (left to right).</summary>
        Texture2D Unrolled(Texture2D above)
        {
            int length = Mathf.Min(16384, Mathf.CeilToInt(Path.Length / texel));
            int across = Mathf.CeilToInt(2 * halfWidth / texel);
            // With its mips: far off on the sky's ribbon it is seen blurred.
            var image = new Texture2D(length, across, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[length * across];
            float size = 2f * PlaneLayout.Story;
            for (int i = 0; i < length; i++)
            {
                float s = (i + 0.5f) / length * Path.Length;
                var at = Path.Position(s);
                var right = Path.Right(s);
                for (int j = 0; j < across; j++)
                {
                    float t = -halfWidth + (j + 0.5f) / across * 2 * halfWidth;
                    var p = at + right * t;
                    var colour = above.GetPixelBilinear((p.x + PlaneLayout.Story) / size, (p.z + PlaneLayout.Story) / size);
                    // Its sides fade, as the band's.
                    colour.a = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(halfWidth - sideFade, halfWidth, Mathf.Abs(t)));
                    pixels[j * length + i] = colour;
                }
            }
            image.SetPixels(pixels);
            image.Apply();
            return image;
        }
    }
}
