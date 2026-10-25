using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

namespace FinishTheGame
{
    /// <summary>The world: one ordinary, Euclidean loop round the mountain,
    /// laid along the story's curve (<see cref="StoryPath"/>,
    /// <see cref="LoopLayout"/>). Each vertex keeps its place on the curve in
    /// its second UVs, (s, t, 1, the road's heading there), for the ribbon
    /// (Ribbon.hlsl); a mesh without them (the hero) is drawn where it is.</summary>
    [DefaultExecutionOrder(-100)]
    public class LoopWorld : StoryWorld
    {
        public RibbonBend ribbon;

        public List<Block> Blocks { get; private set; }
        /// <summary>Each block's place in the world.</summary>
        public List<Matrix4x4> Placed { get; private set; }

        public override float Ahead => ribbon.flat;
        public override float Behind => ribbon.pastFlat;
        public override float Horizon => ribbon.arc;
        public override Texture2D MapImage => map;
        public override Rect MapArea => new Rect(-extent, -extent, 2 * extent, 2 * extent);
        Texture2D map;
        float extent;

        void Awake()
        {
            Path = StoryPath.Ring();
            Blocks = LoopLayout.Make(Path.Length);
            Placed = new List<Matrix4x4>();

            var parts = new List<CombineInstance>();
            var ground = Strip(LoopLayout.Inner, LoopLayout.Outer, 0f, LoopLayout.Grass);
            parts.Add(new CombineInstance { mesh = ground, transform = Matrix4x4.identity });
            parts.Add(new CombineInstance { mesh = Strip(-2.5f, 2.5f, 0.04f, LoopLayout.Road), transform = Matrix4x4.identity });
            var solid = new GameObject("Colliders").transform;
            solid.SetParent(transform, false);
            solid.gameObject.AddComponent<MeshCollider>().sharedMesh = ground;

            foreach (var block in Blocks)
            {
                var place = Matrix4x4.TRS(
                    Path.At(block.s, block.t, block.bottom),
                    Quaternion.LookRotation(Path.Tangent(block.s)) * Quaternion.Euler(0, block.turn, 0),
                    Vector3.one);
                Placed.Add(place);
                var mesh = MeshKit.Box(block.size, block.colour);
                if (!block.fixedPlace) Story(mesh, place, block.s);
                else Fixed(mesh);
                parts.Add(new CombineInstance { mesh = mesh, transform = place });
                if (!block.solid) continue;
                var go = new GameObject(block.name);
                go.transform.SetParent(solid, false);
                go.transform.SetPositionAndRotation(place.GetPosition(), place.rotation);
                var box = go.AddComponent<BoxCollider>();
                box.size = block.size;
                box.center = new Vector3(0, block.size.y / 2, 0);
            }

            foreach (var (at, size, yaw) in LoopLayout.Mountain())
            {
                var mesh = MeshKit.Box(size, LoopLayout.MountainRock, 4f);
                Fixed(mesh);
                parts.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(at, Quaternion.Euler(0, yaw, 0), Vector3.one) });
            }

            var loop = new Mesh { name = "Loop", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            loop.CombineMeshes(parts.ToArray(), true, true);
            var look = new GameObject("Loop");
            look.transform.SetParent(transform, false);
            look.AddComponent<MeshFilter>().sharedMesh = loop;
            var renderer = look.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            Bendable(renderer);

            var surface = solid.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            map = Paint(512);
            hero.Place(LoopLayout.PointA - 8f);
        }

        /// <summary>The ground between two offsets across the road, a row a metre, with its (s, t).</summary>
        Mesh Strip(float from, float to, float height, Color colour)
        {
            int rows = Mathf.CeilToInt(Path.Length);
            float step = Path.Length / rows;
            var vertices = new Vector3[rows * 2];
            var story = new Vector4[rows * 2];
            var triangles = new int[rows * 6];
            for (int i = 0; i < rows; i++)
            {
                float s = i * step, heading = Path.Heading(s);
                vertices[2 * i] = Path.At(s, from, height);
                vertices[2 * i + 1] = Path.At(s, to, height);
                story[2 * i] = new Vector4(s, from, 1, heading);
                story[2 * i + 1] = new Vector4(s, to, 1, heading);
                int a = 2 * i, b = 2 * ((i + 1) % rows);
                // Seen from above, across the road to the right is +t.
                triangles[6 * i] = a; triangles[6 * i + 1] = b; triangles[6 * i + 2] = a + 1;
                triangles[6 * i + 3] = a + 1; triangles[6 * i + 4] = b; triangles[6 * i + 5] = b + 1;
            }
            var mesh = new Mesh { name = "Strip", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.SetUVs(1, story);
            var colours = new Color[vertices.Length];
            for (int i = 0; i < colours.Length; i++) colours[i] = colour;
            mesh.colors = colours;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Each vertex's (s, t) by where it stands, near its block's s.</summary>
        void Story(Mesh mesh, Matrix4x4 place, float near)
        {
            var vertices = mesh.vertices;
            var story = new Vector4[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var st = Path.Project(place.MultiplyPoint3x4(vertices[i]), near, 20f);
                float s = Path.Wrap(st.x);
                story[i] = new Vector4(s, st.y, 1, Path.Heading(s));
            }
            mesh.SetUVs(1, story);
        }

        /// <summary>Not of the story: second UVs all 0, drawn where it is.</summary>
        static void Fixed(Mesh mesh) => mesh.SetUVs(1, new Vector4[mesh.vertexCount]);

        /// <summary>The map's picture: the mountain, the ground along the loop, the boxes from above.</summary>
        Texture2D Paint(int pixels)
        {
            extent = 0;
            for (float s = 0; s < Path.Length; s += 5) extent = Mathf.Max(extent, Path.Position(s).magnitude);
            extent += LoopLayout.Outer + 8f;
            float metre = pixels / (2f * extent);
            var image = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var fill = new Color[pixels * pixels];
            var mountain = new Color(0.22f, 0.2f, 0.19f, 1f);
            var outside = new Color(0.12f, 0.14f, 0.12f, 1f);
            for (int py = 0; py < pixels; py++)
                for (int px = 0; px < pixels; px++)
                {
                    var m = new Vector2(px + 0.5f - pixels / 2f, py + 0.5f - pixels / 2f) / metre;
                    float inner = StoryPath.RingRadius(Mathf.Atan2(m.y, m.x)) + LoopLayout.Inner;
                    fill[py * pixels + px] = m.magnitude < inner ? mountain : outside;
                }

            float fine = 0.5f / metre;
            void Plot(Vector3 at, Color colour)
            {
                int px = (int)(at.x * metre + pixels / 2f), py = (int)(at.z * metre + pixels / 2f);
                if (px >= 0 && py >= 0 && px < pixels && py < pixels) fill[py * pixels + px] = colour;
            }
            for (float s = 0; s < Path.Length; s += fine)
                for (float t = LoopLayout.Inner; t <= LoopLayout.Outer; t += fine)
                    Plot(Path.At(s, t), Mathf.Abs(t) <= 2.5f ? LoopLayout.Road : LoopLayout.Grass);

            // The boxes, the lower first, each sampled finer than a pixel.
            var order = new int[Blocks.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            System.Array.Sort(order, (a, b) => Top(Blocks[a]).CompareTo(Top(Blocks[b])));
            foreach (int i in order)
            {
                var block = Blocks[i];
                var colour = block.colour;
                colour.a = 1f;
                for (float z = -block.size.z / 2; z <= block.size.z / 2; z += fine)
                    for (float x = -block.size.x / 2; x <= block.size.x / 2; x += fine)
                        Plot(Placed[i].MultiplyPoint3x4(new Vector3(x, 0, z)), colour);
            }
            image.SetPixels(fill);
            image.Apply();
            return image;

            static float Top(Block b) => b.bottom + b.size.y;
        }
    }
}
