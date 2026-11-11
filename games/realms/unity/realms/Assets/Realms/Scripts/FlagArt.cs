using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using static Realms.Lang;

namespace Realms
{
    /// <summary>
    /// The kingdoms' flags drawn: as flags (the editor and the cloth on the map's banners) and as
    /// shields with a gold rim (the badges of the interface). The pattern comes from
    /// <see cref="Flags.Part"/>, the emblem from its silhouette in UI/Art/Emblems (only its alpha,
    /// tinted). The map's cloth samples one texture array with a slice per kingdom, so a new flag
    /// shows on every banner at once without drawing the map again.
    /// </summary>
    public static class FlagArt
    {
        public enum Shape
        {
            Flag,
            Shield,
        }

        /// <summary>The tinctures' shades, in <see cref="Flags.Tinctures"/> order; a kingdom's field is its colour on the map.</summary>
        public static readonly Color[] Tinctures =
        {
            Palette.Hex("#EFE8D6"), Palette.Hex("#E3BD44"), Palette.Hex("#E78537"), Palette.Hex("#D04141"),
            Palette.Hex("#944ECF"), Palette.Hex("#3674D0"), Palette.Hex("#79B4E3"), Palette.Hex("#36B391"),
            Palette.Hex("#8A5A3B"), Palette.Hex("#2B2622"),
        };

        public static string[] TinctureNames => new[]
        {
            T("белый", "white"), T("жёлтый", "yellow"), T("оранжевый", "orange"), T("красный", "red"), T("фиолетовый", "violet"), T("синий", "blue"), T("голубой", "sky blue"), T("зелёный", "green"), T("коричневый", "brown"), T("чёрный", "black"),
        };

        public static string[] PatternNames => new[]
        {
            T("Одноцветный", "Plain"), T("Две половины", "Two halves"), T("Верх и низ", "Top and bottom"), T("Четыре четверти", "Quarters"), T("Наискосок", "Diagonal"), T("Крест", "Cross"), T("Косой крест", "Saltire"),
            T("Угол", "Corner"), T("Полоса", "Stripe"), T("Кайма", "Border"), T("Треугольник", "Triangle"),
        };

        public static string[] EmblemNames => new[]
        {
            T("Без знака", "No emblem"), T("Лев", "Lion"), T("Орёл", "Eagle"), T("Конь", "Horse"), T("Олень", "Stag"), T("Медведь", "Bear"), T("Дракон", "Dragon"), T("Рыба", "Fish"), T("Дерево", "Tree"), T("Башня", "Tower"), T("Корона", "Crown"),
            T("Звезда", "Star"), T("Солнце", "Sun"), T("Луна", "Moon"), T("Меч", "Sword"), T("Ключ", "Key"), T("Колосья", "Wheat"), T("Лилия", "Lily"), T("Якорь", "Anchor"),
        };

        /// <summary>The emblem's silhouette in the art library: emblem_lion and so on.</summary>
        public static string EmblemIcon(Emblem emblem) => "emblem_" + emblem.ToString().ToLowerInvariant();

        /// <summary>The cloth's material (Realms/Flag); without it the banners keep the kingdom's plain colour.</summary>
        public static Material ClothMaterial { get; private set; }

        /// <summary>
        /// The cloth's slice of undyed sailcloth, after the kingdoms' flags: the sail of a boat
        /// nobody owns.
        /// </summary>
        public static int Undyed => world != null ? world.Countries.Count : 0;

        /// <summary>Undyed sailcloth.</summary>
        static readonly Color32 Sailcloth = Palette.Hex("#D8CCB2");

        /// <summary>One flag of the map's cloth, 3:2.</summary>
        const int ClothWidth = 120, ClothHeight = 80;

        static readonly int ClothId = Shader.PropertyToID("_RealmsFlags");
        static readonly Color GoldLight = Palette.Hex("#F4DD8E"), GoldDark = Palette.Hex("#9E762A"), Ink = Palette.Hex("#241C15");

        static World world;
        static ArtLibrary art;
        static Texture2DArray cloth;
        static readonly Dictionary<Emblem, Mask> masks = new Dictionary<Emblem, Mask>();
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        /// <summary>The world whose kingdoms fly the flags, the art with the emblems and the cloth's material.</summary>
        public static void Bind(World playing, ArtLibrary library, Material material)
        {
            world = playing;
            if (art != library)
                masks.Clear();
            art = library;
            ClothMaterial = material;
            Refresh();
        }

        /// <summary>
        /// Draws every kingdom's flag into the cloth's texture array again (after the players drew
        /// theirs) and gives each kingdom its field's colour in <see cref="Palette.Countries"/>;
        /// whatever was built in the old colours has to be drawn again.
        /// </summary>
        public static void Refresh()
        {
            if (world == null)
                return;
            for (int k = 0; k < world.Countries.Count && k < Palette.Countries.Length; k++)
                Palette.Countries[k] = Tincture(world.Countries[k].Flag.Field);
            int depth = world.Countries.Count + 1;
            if (cloth == null || cloth.depth != depth)
            {
                if (cloth != null)
                    Object.Destroy(cloth);
                cloth = new Texture2DArray(ClothWidth, ClothHeight, depth, TextureFormat.RGBA32, true, false)
                {
                    name = "Realms flags",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 4,
                };
            }
            var pixels = new Color32[ClothWidth * ClothHeight];
            for (int k = 0; k < world.Countries.Count; k++)
            {
                Paint(pixels, ClothWidth, ClothHeight, world.Countries[k].Flag, Shape.Flag, 2, false);
                cloth.SetPixels32(pixels, k, 0);
            }
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Sailcloth;
            cloth.SetPixels32(pixels, Undyed, 0);
            cloth.Apply(true, false);
            Shader.SetGlobalTexture(ClothId, cloth);
        }

        /// <summary>A kingdom's shield for the interface's badges.</summary>
        public static Texture2D ShieldOf(int country) =>
            world != null && country >= 0 && country < world.Countries.Count ? Image(world.Countries[country].Flag, Shape.Shield, 92, 104) : null;

        /// <summary>The flag or shield as a texture, drawn once for each flag and size.</summary>
        public static Texture2D Image(Flag flag, Shape shape, int width, int height)
        {
            string key = $"{flag.Code}:{shape}:{width}x{height}";
            if (cache.TryGetValue(key, out Texture2D texture) && texture != null)
                return texture;
            texture = NewTexture(width, height, key);
            PaintInto(texture, flag, shape);
            cache[key] = texture;
            return texture;
        }

        /// <summary>An empty texture for <see cref="PaintInto"/>.</summary>
        public static Texture2D NewTexture(int width, int height, string name) =>
            new Texture2D(width, height, TextureFormat.RGBA32, true, false)
            {
                name = "Flag " + name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
            };

        /// <summary>Draws the flag into the texture, over what it showed (the editor's preview and tiles).</summary>
        public static void PaintInto(Texture2D texture, Flag flag, Shape shape)
        {
            int width = texture.width, height = texture.height;
            var pixels = new Color32[width * height];
            Paint(pixels, width, height, flag, shape, width * height > 40000 ? 2 : 3, shape == Shape.Flag);
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
        }

        /// <summary>
        /// The flag's pixels, bottom row first, each the mean of samples × samples points: the
        /// pattern's parts, the emblem over them, and for the shield its rim and outline.
        /// <paramref name="framed"/> adds folds and a dark edge, for flags drawn on the interface.
        /// </summary>
        static void Paint(Color32[] pixels, int width, int height, Flag flag, Shape shape, int samples, bool framed)
        {
            Color field = Tincture(flag.Field), second = Tincture(flag.Second), ink = Tincture(flag.EmblemColour);
            Mask mask = flag.Emblem != Emblem.None ? MaskOf(flag.Emblem) : null;
            // The emblem's square: on the flag two thirds of its height in the middle, on the
            // shield three fifths of its width a little above the middle.
            float box = shape == Shape.Flag ? 0.66f * height : 0.6f * width;
            float boxX = 0.5f * width, boxY = shape == Shape.Flag ? 0.5f * height : 0.43f * height;
            // The shield's rim and outline as fractions of its half-width and height.
            const float Margin = 0.03f;
            float halfWidth = (0.5f - Margin) * width, tall = (1f - 2f * Margin) * height;
            float edge = Mathf.Max(1.2f, 0.018f * width), rim = edge + Mathf.Max(2f, 0.075f * width);
            float inv = 1f / (samples * samples);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            // px, py in pixels from the left and from the top.
                            float px = x + (sx + 0.5f) / samples, py = height - y - (sy + 0.5f) / samples;
                            Color c;
                            float u, v;
                            if (shape == Shape.Flag)
                            {
                                u = px / width;
                                v = py / height;
                            }
                            else
                            {
                                float X = (px - 0.5f * width) / halfWidth, Y = (py - Margin * height) / tall;
                                if (!InShield(X, Y, 0, 0))
                                    continue;
                                if (!InShield(X, Y, edge / halfWidth, edge / tall))
                                {
                                    Add(Ink, ref r, ref g, ref b, ref a);
                                    continue;
                                }
                                if (!InShield(X, Y, rim / halfWidth, rim / tall))
                                {
                                    Add(Color.Lerp(GoldLight, GoldDark, Mathf.Clamp01(0.75f * Y + 0.2f * (X + 1f) * 0.5f)), ref r, ref g, ref b, ref a);
                                    continue;
                                }
                                u = (X + 1f) * 0.5f;
                                v = Y;
                            }
                            c = Flags.Part(flag.Pattern, u, v) == 0 ? field : second;
                            if (mask != null)
                            {
                                float alpha = mask.Sample((px - boxX) / box + 0.5f, (py - boxY) / box + 0.5f);
                                if (alpha > 0)
                                    c = Color.Lerp(c, ink, alpha);
                            }
                            if (shape == Shape.Shield)
                                c *= 1.05f - 0.1f * v - 0.04f * u;
                            else if (framed)
                            {
                                c *= 1f + 0.06f * Mathf.Sin(u * 7.9f + 0.6f) - 0.05f * u;
                                float border = Mathf.Min(Mathf.Min(px, width - px), Mathf.Min(py, height - py));
                                if (border < 1.2f)
                                    c = Color.Lerp(c, Ink, 0.55f);
                            }
                            Add(c, ref r, ref g, ref b, ref a);
                        }
                    }
                    float alphaSum = a * inv;
                    pixels[y * width + x] = alphaSum <= 0
                        ? new Color32(0, 0, 0, 0)
                        : new Color32(Byte(r / a), Byte(g / a), Byte(b / a), Byte(alphaSum));
                }
            }
        }

        static void Add(Color c, ref float r, ref float g, ref float b, ref float a)
        {
            r += c.r;
            g += c.g;
            b += c.b;
            a += 1f;
        }

        static byte Byte(float value) => (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);

        static Color Tincture(int index) => Tinctures[Mathf.Clamp(index, 0, Tinctures.Length - 1)];

        /// <summary>
        /// Inside a heater shield, X across its width from −1 to 1 and Y down its height from 0 to
        /// 1, drawn in by insetX and insetY: straight sides down to the middle, then curving to
        /// the point.
        /// </summary>
        static bool InShield(float X, float Y, float insetX, float insetY)
        {
            if (Y < insetY || Y > 1f)
                return false;
            float t = Mathf.Min(1f, Y + insetY);
            float half = t < 0.48f ? 1f : Mathf.Pow(Mathf.Cos((t - 0.48f) / 0.52f * Mathf.PI * 0.5f), 0.8f);
            return Mathf.Abs(X) <= half - insetX;
        }

        static Mask MaskOf(Emblem emblem)
        {
            if (masks.TryGetValue(emblem, out Mask mask))
                return mask;
            Texture2D texture = art != null ? art.IconOf(EmblemIcon(emblem)) : null;
            mask = texture != null ? Mask.Of(texture) : null;
            masks[emblem] = mask;
            return mask;
        }

        /// <summary>An emblem's alpha, read once from its texture (Setup imports the emblems readable).</summary>
        sealed class Mask
        {
            byte[] alpha;
            int width, height;

            public static Mask Of(Texture2D texture)
            {
                if (!texture.isReadable)
                {
                    Debug.LogWarning($"Realms: emblem {texture.name} is not readable; run Realms/Setup.");
                    return null;
                }
                Color32[] pixels = texture.GetPixels32();
                var mask = new Mask { width = texture.width, height = texture.height, alpha = new byte[pixels.Length] };
                for (int k = 0; k < pixels.Length; k++)
                    mask.alpha[k] = pixels[k].a;
                return mask;
            }

            /// <summary>Bilinear alpha at (u from the left, v from the top), 0 outside.</summary>
            public float Sample(float u, float v)
            {
                if (u <= 0 || u >= 1 || v <= 0 || v >= 1)
                    return 0;
                float fx = u * width - 0.5f, fy = (1f - v) * height - 0.5f;
                int x0 = Mathf.Clamp((int)Mathf.Floor(fx), 0, width - 1), y0 = Mathf.Clamp((int)Mathf.Floor(fy), 0, height - 1);
                int x1 = Mathf.Min(x0 + 1, width - 1), y1 = Mathf.Min(y0 + 1, height - 1);
                float tx = Mathf.Clamp01(fx - x0), ty = Mathf.Clamp01(fy - y0);
                float top = Mathf.Lerp(alpha[y0 * width + x0], alpha[y0 * width + x1], tx);
                float bottom = Mathf.Lerp(alpha[y1 * width + x0], alpha[y1 * width + x1], tx);
                return Mathf.Lerp(top, bottom, ty) / 255f;
            }
        }
    }

    /// <summary>
    /// Flags of cloth on the map: grids hanging from their poles, each with its distance from the
    /// pole, its place on the flag and its kingdom's slice in the UVs, which the Realms/Flag
    /// shader waves and paints.
    /// </summary>
    public sealed class FlagCloth
    {
        const int Columns = 8, Rows = 3;

        readonly List<Vector3> positions = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector4> tangents = new List<Vector4>();
        readonly List<Vector4> uvs = new List<Vector4>();
        readonly List<int> indices = new List<int>();

        public int VertexCount => positions.Count;

        /// <summary>
        /// A flag hanging from <paramref name="top"/> on its pole and flying along
        /// <paramref name="along"/> (level), <paramref name="length"/> long and
        /// <paramref name="height"/> tall, its free end dropping a little. A sail waves less
        /// (<paramref name="wave"/> under 1).
        /// </summary>
        public void Add(Vector3 top, Vector3 along, float length, float height, int country, float wave = 1f)
        {
            along.y = 0;
            along.Normalize();
            // The normal of the side the triangles' winding makes the front (Unity's clockwise).
            Vector3 normal = Vector3.Cross(along, Vector3.down).normalized;
            int first = positions.Count;
            for (int r = 0; r <= Rows; r++)
            {
                for (int c = 0; c <= Columns; c++)
                {
                    float u = c / (float)Columns, v = r / (float)Rows;
                    positions.Add(top + along * (length * u) + Vector3.down * (height * v + 0.1f * height * u * u));
                    normals.Add(normal);
                    tangents.Add(new Vector4(along.x, along.y, along.z, length));
                    uvs.Add(new Vector4(u, 1f - v, country, 0.11f * length * wave));
                }
            }
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    int i = first + r * (Columns + 1) + c;
                    indices.Add(i);
                    indices.Add(i + 1);
                    indices.Add(i + Columns + 1);
                    indices.Add(i + 1);
                    indices.Add(i + Columns + 2);
                    indices.Add(i + Columns + 1);
                }
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);
            // The waving leaves the flat grid by a little.
            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;
            bounds.Expand(0.1f);
            mesh.bounds = bounds;
            return mesh;
        }
    }
}
