using System.Collections.Generic;
using Realms.Core;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// The shade on the ground round the feet of what stands on the map, so that houses, walls
    /// and trees stand in the land rather than on it (review of 2026-11-10): a soft ring, darkest
    /// at the foot and gone half the thing's height away, drawn into a texture over the whole map
    /// that the terrain shader reads (_RealmsContact, _RealmsContactRect). Red holds houses,
    /// walls and towers, green the trees' trunks, whose shade the town and hero screens fade
    /// with the plants they cut away. Each cell's shapes are kept, so when a town grows only the
    /// ground round it is drawn again.
    /// </summary>
    public sealed class ContactShade : System.IDisposable
    {
        /// <summary>Texels per world unit: a cottage's ring is four or five across.</summary>
        const int PerUnit = 24;

        /// <summary>How far past the map's outermost cell centres the texture reaches.</summary>
        const float Margin = 0.5f;

        static readonly int TextureId = Shader.PropertyToID("_RealmsContact");
        static readonly int RectId = Shader.PropertyToID("_RealmsContactRect");

        /// <summary>
        /// A rectangle with rounded corners on the ground: a circle when its corners are half its
        /// sides, a stretch of wall when they are half its thickness.
        /// </summary>
        struct Shape
        {
            public Vector2 centre, axis, inner;
            public float corner, ring, strength;
            public int channel;
            public RectInt texels;

            /// <summary>How dark the shade is at (x, z): its strength at and inside the foot, falling to nothing a ring away.</summary>
            public float At(float x, float z)
            {
                float dx = x - centre.x, dz = z - centre.y;
                float u = Mathf.Abs(dx * axis.x + dz * axis.y) - inner.x;
                float w = Mathf.Abs(dz * axis.x - dx * axis.y) - inner.y;
                float ou = Mathf.Max(u, 0f), ow = Mathf.Max(w, 0f);
                float d = Mathf.Sqrt(ou * ou + ow * ow) + Mathf.Min(Mathf.Max(u, w), 0f) - corner;
                float t = 1f - Mathf.Clamp01(d / ring);
                return strength * t * t;
            }
        }

        readonly float minX, minZ;
        readonly int width, height;
        readonly byte[] data;
        readonly Texture2D texture;
        readonly Dictionary<int, List<Shape>> cells = new Dictionary<int, List<Shape>>();
        readonly List<RectInt> stale = new List<RectInt>();
        List<Shape> current;
        bool drawn;

        public ContactShade(World world)
        {
            minX = world.MinX - Margin;
            minZ = world.MinZ - Margin;
            width = Mathf.CeilToInt((world.MaxX - world.MinX + 2f * Margin) * PerUnit);
            height = Mathf.CeilToInt((world.MaxZ - world.MinZ + 2f * Margin) * PerUnit);
            data = new byte[width * height * 2];
            texture = new Texture2D(width, height, TextureFormat.RG16, true, true)
            {
                name = "Realms contact shade",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        /// <summary>
        /// Starts the shapes of a cell being dressed, forgetting those it had: the ground they
        /// shaded is drawn again at the next <see cref="Flush"/>.
        /// </summary>
        public void Begin(int cell)
        {
            if (cells.TryGetValue(cell, out current))
            {
                if (drawn)
                {
                    foreach (Shape shape in current)
                        stale.Add(shape.texels);
                }
                current.Clear();
            }
            else
                cells[cell] = current = new List<Shape>();
        }

        /// <summary>
        /// A building's foot: a rectangle round <paramref name="centre"/>, half its sides
        /// <paramref name="half"/>, turned by <paramref name="yaw"/> degrees as a model is turned
        /// about +y; the building stands <paramref name="tall"/> high.
        /// </summary>
        public void Building(Vector2 centre, Vector2 half, float yaw, float tall)
        {
            float a = yaw * Mathf.Deg2Rad;
            Add(centre, new Vector2(Mathf.Cos(a), -Mathf.Sin(a)), half, 0.3f * Mathf.Min(half.x, half.y), Ring(tall), 1f, 0);
        }

        /// <summary>A stretch of wall from <paramref name="from"/> to <paramref name="to"/>, <paramref name="thick"/> across and <paramref name="tall"/> high.</summary>
        public void Wall(Vector2 from, Vector2 to, float thick, float tall)
        {
            Vector2 along = to - from;
            float length = along.magnitude;
            if (length < 1e-4f)
                return;
            float r = thick * 0.5f;
            Add((from + to) * 0.5f, along / length, new Vector2(length * 0.5f + r, r), r, Ring(tall), 1f, 0);
        }

        /// <summary>A round tower's foot, <paramref name="radius"/> wide and <paramref name="tall"/> high.</summary>
        public void Tower(Vector2 centre, float radius, float tall) =>
            Add(centre, Vector2.right, new Vector2(radius, radius), radius, Ring(tall), 1f, 0);

        /// <summary>
        /// A tree's trunk, <paramref name="radius"/> wide, the tree <paramref name="tall"/> high:
        /// a lighter and narrower shade than a house's, as its crown lets light through.
        /// </summary>
        public void Trunk(Vector2 centre, float radius, float tall) =>
            Add(centre, Vector2.right, new Vector2(radius, radius), radius, Mathf.Clamp(0.25f * tall, 0.03f, 0.1f), 0.7f, 1);

        /// <summary>The ring is half as wide as the thing is tall, within a hand and a cottage's width.</summary>
        static float Ring(float tall) => Mathf.Clamp(0.5f * tall, 0.04f, 0.22f);

        void Add(Vector2 centre, Vector2 axis, Vector2 half, float corner, float ring, float strength, int channel)
        {
            if (current == null)
                return;
            // How far the shape and its ring reach along the map's x and z.
            float ex = Mathf.Abs(axis.x) * half.x + Mathf.Abs(axis.y) * half.y + ring;
            float ez = Mathf.Abs(axis.y) * half.x + Mathf.Abs(axis.x) * half.y + ring;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((centre.x - ex - minX) * PerUnit));
            int x1 = Mathf.Min(width, Mathf.CeilToInt((centre.x + ex - minX) * PerUnit));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((centre.y - ez - minZ) * PerUnit));
            int z1 = Mathf.Min(height, Mathf.CeilToInt((centre.y + ez - minZ) * PerUnit));
            if (x1 <= x0 || z1 <= z0)
                return;
            var shape = new Shape
            {
                centre = centre,
                axis = axis,
                inner = new Vector2(Mathf.Max(half.x - corner, 0f), Mathf.Max(half.y - corner, 0f)),
                corner = corner,
                ring = Mathf.Max(ring, 1e-3f),
                strength = strength,
                channel = channel,
                texels = new RectInt(x0, z0, x1 - x0, z1 - z0),
            };
            current.Add(shape);
            if (drawn)
                stale.Add(shape.texels);
        }

        /// <summary>Draws the whole map's shade once it is dressed and hands it to the shaders.</summary>
        public void Draw()
        {
            System.Array.Clear(data, 0, data.Length);
            foreach (List<Shape> shapes in cells.Values)
            {
                foreach (Shape shape in shapes)
                    Raster(shape, shape.texels);
            }
            stale.Clear();
            drawn = true;
            current = null;
            Upload();
            Shader.SetGlobalTexture(TextureId, texture);
            Shader.SetGlobalVector(RectId, new Vector4(minX, minZ, (float)PerUnit / width, (float)PerUnit / height));
        }

        /// <summary>Draws again the ground round the cells dressed again since the last time.</summary>
        public void Flush()
        {
            current = null;
            if (!drawn || stale.Count == 0)
                return;
            // A block of cells dressed again leaves a thousand small rectangles, one per tree;
            // merged, every shape on the map is tested against a few.
            List<RectInt> cleared = Merged(stale);
            foreach (RectInt r in cleared)
            {
                for (int z = r.yMin; z < r.yMax; z++)
                    System.Array.Clear(data, (z * width + r.xMin) * 2, r.width * 2);
            }
            // Every shape over a cleared rectangle, the cell's new ones and its neighbours' alike,
            // goes back in; drawing one twice changes nothing, as the darkest shade wins.
            foreach (List<Shape> shapes in cells.Values)
            {
                foreach (Shape shape in shapes)
                {
                    foreach (RectInt r in cleared)
                    {
                        int x0 = Mathf.Max(shape.texels.xMin, r.xMin), x1 = Mathf.Min(shape.texels.xMax, r.xMax);
                        int z0 = Mathf.Max(shape.texels.yMin, r.yMin), z1 = Mathf.Min(shape.texels.yMax, r.yMax);
                        if (x1 > x0 && z1 > z0)
                            Raster(shape, new RectInt(x0, z0, x1 - x0, z1 - z0));
                    }
                }
            }
            stale.Clear();
            Upload();
        }

        /// <summary>The rectangles, those that overlap or touch joined into the rectangle round both, until none do.</summary>
        static List<RectInt> Merged(List<RectInt> rects)
        {
            var merged = new List<RectInt>(rects);
            bool joined = true;
            while (joined)
            {
                joined = false;
                for (int a = 0; a < merged.Count; a++)
                {
                    for (int b = merged.Count - 1; b > a; b--)
                    {
                        RectInt p = merged[a], q = merged[b];
                        if (p.xMin > q.xMax || q.xMin > p.xMax || p.yMin > q.yMax || q.yMin > p.yMax)
                            continue;
                        int x0 = Mathf.Min(p.xMin, q.xMin), z0 = Mathf.Min(p.yMin, q.yMin);
                        merged[a] = new RectInt(x0, z0, Mathf.Max(p.xMax, q.xMax) - x0, Mathf.Max(p.yMax, q.yMax) - z0);
                        merged.RemoveAt(b);
                        joined = true;
                    }
                }
            }
            return merged;
        }

        void Raster(Shape shape, RectInt texels)
        {
            for (int j = texels.yMin; j < texels.yMax; j++)
            {
                float z = minZ + (j + 0.5f) / PerUnit;
                int row = j * width;
                for (int i = texels.xMin; i < texels.xMax; i++)
                {
                    float v = shape.At(minX + (i + 0.5f) / PerUnit, z);
                    if (v <= 0f)
                        continue;
                    int k = (row + i) * 2 + shape.channel;
                    var b = (byte)Mathf.Min(255, Mathf.RoundToInt(v * 255f));
                    if (b > data[k])
                        data[k] = b;
                }
            }
        }

        void Upload()
        {
            texture.SetPixelData(data, 0);
            texture.Apply(true, false);
        }

        public void Dispose()
        {
            Shader.SetGlobalVector(RectId, Vector4.zero);
            Object.Destroy(texture);
        }
    }
}
