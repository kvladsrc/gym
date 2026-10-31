using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Procedural.Editor
{
    /// <summary>
    /// Builds seamless 512x512 albedo, normal and URP mask maps for the terrain layers.
    /// Everything is derived from a tileable height field, so the three maps agree.
    /// </summary>
    public static class ProceduralTerrainTextures
    {
        const int Size = 512;
        static readonly string Folder = ProceduralPaths.Placeholders + "/Textures";

        enum Kind
        {
            Sand,
            Grass,
            Rock,
            Gravel
        }

        enum Map
        {
            Albedo,
            Normal,
            Mask
        }

        readonly struct LayerProfile
        {
            public readonly int Seed;
            public readonly float NormalStrength;
            public readonly float Smoothness;
            public readonly float OcclusionFloor;

            public LayerProfile(int seed, float normalStrength, float smoothness, float occlusionFloor)
            {
                Seed = seed;
                NormalStrength = normalStrength;
                Smoothness = smoothness;
                OcclusionFloor = occlusionFloor;
            }
        }

        /// <summary>Loads the layer maps, generating them only when one of the PNGs is missing.</summary>
        public static (Texture2D albedo, Texture2D normal, Texture2D mask) Ensure(string name, Color baseColor)
        {
            return Ensure(name, baseColor, false);
        }

        /// <summary>Force regenerates the maps; useful while tuning the height fields.</summary>
        public static (Texture2D albedo, Texture2D normal, Texture2D mask) Ensure(string name, Color baseColor, bool force)
        {
            Kind kind = KindOf(name);
            Folders.Ensure(Folder);

            string albedoPath = $"{Folder}/{name}_Albedo.png";
            string normalPath = $"{Folder}/{name}_Normal.png";
            string maskPath = $"{Folder}/{name}_Mask.png";

            bool regenerated = force || !Exists(albedoPath) || !Exists(normalPath) || !Exists(maskPath);
            if (regenerated)
                Generate(kind, baseColor, albedoPath, normalPath, maskPath);

            Import(albedoPath, Map.Albedo, regenerated);
            Import(normalPath, Map.Normal, regenerated);
            Import(maskPath, Map.Mask, regenerated);

            return (Load(albedoPath), Load(normalPath), Load(maskPath));
        }

        static Kind KindOf(string name)
        {
            return name switch
            {
                "Sand" => Kind.Sand,
                "Grass" => Kind.Grass,
                "Rock" => Kind.Rock,
                "Gravel" => Kind.Gravel,
                _ => throw new ArgumentException($"No procedural terrain texture set for '{name}'.", nameof(name))
            };
        }

        static LayerProfile ProfileOf(Kind kind)
        {
            return kind switch
            {
                // Sand ripples are shallow and soft; rock cracks are the deepest and sharpest.
                Kind.Sand => new LayerProfile(1103, 40f, 0.07f, 0.72f),
                Kind.Grass => new LayerProfile(2207, 60f, 0.12f, 0.62f),
                Kind.Rock => new LayerProfile(3301, 100f, 0.20f, 0.50f),
                _ => new LayerProfile(4409, 80f, 0.09f, 0.55f)
            };
        }

        static void Generate(Kind kind, Color baseColor, string albedoPath, string normalPath, string maskPath)
        {
            LayerProfile profile = ProfileOf(kind);
            int count = Size * Size;
            var height = new float[count];
            var detail = new float[count];
            BuildHeight(kind, height, detail);

            var albedo = new Color[count];
            var normal = new Color32[count];
            var mask = new Color32[count];
            for (int y = 0; y < Size; y++)
            {
                float v = (y + 0.5f) / Size;
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    float u = (x + 0.5f) / Size;
                    float h = height[i];
                    albedo[i] = Albedo(kind, baseColor, u, v, h, detail[i], profile.Seed);

                    float occlusion = profile.OcclusionFloor +
                                      (1f - profile.OcclusionFloor) * Mathf.SmoothStep(0f, 1f, h);
                    float smoothness = Mathf.Clamp(
                        profile.Smoothness + 0.03f * (detail[i] - 0.5f) * 2f, 0.05f, 0.25f);
                    // URP Terrain Lit mask: R metallic, G occlusion, B height, A smoothness.
                    mask[i] = new Color32(0, ToByte(occlusion), ToByte(h), ToByte(smoothness));
                }
            }
            BuildNormal(height, profile.NormalStrength, normal);

            WriteColor(albedoPath, albedo);
            WriteColor32(normalPath, normal);
            WriteColor32(maskPath, mask);
        }

        static void BuildHeight(Kind kind, float[] height, float[] detail)
        {
            for (int y = 0; y < Size; y++)
            {
                float v = (y + 0.5f) / Size;
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size;
                    int i = y * Size + x;
                    switch (kind)
                    {
                        case Kind.Sand:
                            Sand(u, v, out height[i], out detail[i]);
                            break;
                        case Kind.Grass:
                            Grass(u, v, out height[i], out detail[i]);
                            break;
                        case Kind.Rock:
                            Rock(u, v, out height[i], out detail[i]);
                            break;
                        default:
                            Gravel(u, v, out height[i], out detail[i]);
                            break;
                    }
                    height[i] = Mathf.Clamp01(height[i]);
                }
            }
        }

        /// <summary>Low dunes with a few diagonally travelling sine ripples bent by noise, plus grain.</summary>
        static void Sand(float u, float v, out float height, out float detail)
        {
            float dunes = 0.5f + 0.5f * Noise.Fbm(u, v, 3, 4, 1117);
            float warp = Noise.Fbm(u, v, 3, 2, 1123);
            // Integer wave numbers keep the sine ripples seamless.
            float phase = Noise.TwoPi * (5f * u + 2f * v) + warp * 1.15f;
            float ripple = 0.5f + 0.5f * Mathf.Sin(phase);
            float grain = Noise.Value(u, v, 128, 1129) - 0.5f;

            height = 0.52f + 0.14f * (dunes - 0.5f) + 0.055f * (ripple - 0.5f) + 0.02f * grain;
            detail = ripple;
        }

        /// <summary>Clumpy fBm soil with rounded tufts gated by another noise octave.</summary>
        static void Grass(float u, float v, out float height, out float detail)
        {
            float clumps = 0.5f + 0.5f * Noise.Fbm(u, v, 4, 5, 2213);
            float soil = 0.5f + 0.5f * Noise.Fbm(u, v, 16, 3, 2221);
            Noise.WorleyCell tuftCell = Noise.Worley(u, v, 20, 2237);
            float tuft = 1f - Mathf.SmoothStep(0f, 0.55f, tuftCell.F1);
            float gate = Mathf.SmoothStep(0.35f, 0.75f, 0.5f + 0.5f * Noise.Fbm(u, v, 6, 2, 2239));
            float tufts = tuft * gate;
            float grain = Noise.Value(u, v, 96, 2243) - 0.5f;

            height = 0.47f + 0.18f * (clumps - 0.5f) + 0.10f * (soil - 0.5f) +
                     0.16f * tufts + 0.018f * grain;
            detail = tufts;
        }

        /// <summary>Ridged fBm stone with Worley cell borders carved out as cracks.</summary>
        static void Rock(float u, float v, out float height, out float detail)
        {
            float ridges = Noise.Ridged(u, v, 4, 5, 3307);
            float body = 0.5f + 0.5f * Noise.Fbm(u, v, 3, 4, 3313);
            Noise.WorleyCell crackCell = Noise.Worley(u, v, 6, 3319);
            float crack = 1f - Mathf.SmoothStep(0.015f, 0.10f, crackCell.F2 - crackCell.F1);
            float chips = Noise.Worley(u, v, 18, 3323).F1;

            height = 0.32f + 0.52f * ridges + 0.10f * (body - 0.5f) - 0.30f * crack;
            height -= 0.04f * Mathf.SmoothStep(0.2f, 0.8f, chips);
            detail = crack;
        }

        /// <summary>Packed Worley pebbles, each a rounded dome of its own height, with a gritty bed.</summary>
        static void Gravel(float u, float v, out float height, out float detail)
        {
            Noise.WorleyCell pebble = Noise.Worley(u, v, 11, 4409);
            float radius = 0.60f + 0.28f * pebble.Cell;
            float t = Mathf.Clamp01(1f - pebble.F1 / radius);
            float dome = t * t * (3f - 2f * t);
            float bed = 0.17f + 0.05f * (0.5f + 0.5f * Noise.Fbm(u, v, 24, 2, 4421));
            float grit = Noise.Value(u, v, 128, 4423) - 0.5f;

            height = bed + (0.55f + 0.16f * pebble.Cell) * dome + 0.015f * grit;
            detail = pebble.Cell;
        }

        /// <summary>Base colour with low-frequency brightness, a slight hue shift and hollow shading.</summary>
        static Color Albedo(Kind kind, Color baseColor, float u, float v, float height, float detail, int seed)
        {
            float low = 0.5f + 0.5f * Noise.Fbm(u, v, 3, 3, seed + 101);
            float hue = Noise.Fbm(u, v, 2, 2, seed + 211);
            float brightness = 0.75f + 0.40f * low;
            float shade = 0.60f + 0.40f * Mathf.Clamp01(height);

            float r = baseColor.r * brightness * shade * (1f + 0.05f * hue);
            float g = baseColor.g * brightness * shade * (1f - 0.01f * hue);
            float b = baseColor.b * brightness * shade * (1f - 0.05f * hue);

            switch (kind)
            {
                case Kind.Sand:
                    float crest = 0.94f + 0.12f * detail;
                    r *= crest;
                    g *= crest;
                    b *= crest;
                    break;
                case Kind.Grass:
                    // Tufts read a little paler and yellower than the soil around them.
                    r *= 0.96f + 0.08f * detail;
                    g *= 0.98f + 0.04f * detail;
                    b *= 0.98f - 0.04f * detail;
                    break;
                case Kind.Rock:
                    float crevice = 1f - 0.42f * detail;
                    r *= crevice;
                    g *= crevice;
                    b *= crevice;
                    break;
                default:
                    // Each pebble gets its own slightly warm or cool tint.
                    float tint = 0.80f + 0.36f * detail;
                    r *= tint * (1f + 0.10f * (detail - 0.5f));
                    g *= tint;
                    b *= tint * (1f - 0.12f * (detail - 0.5f));
                    break;
            }

            return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
        }

        /// <summary>Tangent-space normal from central differences; the height field wraps at the edges.</summary>
        static void BuildNormal(float[] height, float strength, Color32[] normal)
        {
            for (int y = 0; y < Size; y++)
            {
                int row = y * Size;
                int rowBelow = (y == 0 ? Size - 1 : y - 1) * Size;
                int rowAbove = (y == Size - 1 ? 0 : y + 1) * Size;
                for (int x = 0; x < Size; x++)
                {
                    int left = x == 0 ? Size - 1 : x - 1;
                    int right = x == Size - 1 ? 0 : x + 1;
                    float dx = (height[row + right] - height[row + left]) * 0.5f * strength;
                    float dy = (height[rowAbove + x] - height[rowBelow + x]) * 0.5f * strength;
                    float inv = 1f / Mathf.Sqrt(dx * dx + dy * dy + 1f);
                    normal[row + x] = new Color32(
                        ToByte(0.5f - 0.5f * dx * inv),
                        ToByte(0.5f - 0.5f * dy * inv),
                        ToByte(0.5f + 0.5f * inv),
                        255);
                }
            }
        }

        static void WriteColor(string path, Color[] pixels)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false)
            {
                name = Path.GetFileNameWithoutExtension(path)
            };
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(FullPath(path), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        static void WriteColor32(string path, Color32[] pixels)
        {
            // Linear keeps the normal and mask bytes exactly as computed.
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true)
            {
                name = Path.GetFileNameWithoutExtension(path)
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(FullPath(path), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        static void Import(string path, Map map, bool force)
        {
            var options = ImportAssetOptions.ForceSynchronousImport;
            if (force) options |= ImportAssetOptions.ForceUpdate;
            AssetDatabase.ImportAsset(path, options);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                throw new InvalidOperationException($"Could not import terrain texture '{path}'.");

            bool changed = false;
            TextureImporterType type = map == Map.Normal
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            if (importer.textureType != type)
            {
                importer.textureType = type;
                changed = true;
            }
            bool sRGB = map == Map.Albedo;
            if (importer.sRGBTexture != sRGB)
            {
                importer.sRGBTexture = sRGB;
                changed = true;
            }
            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                changed = true;
            }
            if (!importer.mipmapEnabled)
            {
                importer.mipmapEnabled = true;
                changed = true;
            }
            if (importer.maxTextureSize != Size)
            {
                importer.maxTextureSize = Size;
                changed = true;
            }
            if (importer.textureShape != TextureImporterShape.Texture2D)
            {
                importer.textureShape = TextureImporterShape.Texture2D;
                changed = true;
            }
            if (importer.filterMode != FilterMode.Trilinear)
            {
                importer.filterMode = FilterMode.Trilinear;
                changed = true;
            }
            if (importer.anisoLevel != 4)
            {
                importer.anisoLevel = 4;
                changed = true;
            }
            if (importer.isReadable)
            {
                importer.isReadable = false;
                changed = true;
            }
            // Height and smoothness need the full byte range; normal maps must not be squashed either.
            TextureImporterCompression compression = map == Map.Albedo
                ? TextureImporterCompression.CompressedHQ
                : TextureImporterCompression.Uncompressed;
            if (importer.textureCompression != compression)
            {
                importer.textureCompression = compression;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }

        static Texture2D Load(string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
                throw new InvalidOperationException($"Terrain texture '{path}' did not import.");
            return texture;
        }

        static bool Exists(string assetPath) => File.Exists(FullPath(assetPath));

        static string FullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath);
        }

        static byte ToByte(float value) => (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);

        /// <summary>Deterministic, seamless noise on an integer lattice wrapped by a period.</summary>
        static class Noise
        {
            public const float TwoPi = 6.28318530718f;

            static uint Hash(int x, int y, int seed)
            {
                unchecked
                {
                    uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 1442695041u;
                    h = (h ^ (h >> 13)) * 1274126177u;
                    return h ^ (h >> 16);
                }
            }

            static float Hash01(int x, int y, int seed) => (Hash(x, y, seed) & 0x00FFFFFFu) * (1f / 16777215f);

            static int Wrap(int value, int period)
            {
                int wrapped = value % period;
                return wrapped < 0 ? wrapped + period : wrapped;
            }

            static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

            static float Lerp(float a, float b, float t) => a + (b - a) * t;

            static float GradientDot(int ix, int iy, float dx, float dy, int period, int seed)
            {
                uint h = Hash(Wrap(ix, period), Wrap(iy, period), seed);
                float angle = (h & 0xFFFFu) * (TwoPi / 65536f);
                return Mathf.Cos(angle) * dx + Mathf.Sin(angle) * dy;
            }

            /// <summary>Tileable gradient noise in roughly -1..1; u and v are tile coordinates.</summary>
            public static float Gradient(float u, float v, int period, int seed)
            {
                float x = u * period;
                float y = v * period;
                int x0 = Mathf.FloorToInt(x);
                int y0 = Mathf.FloorToInt(y);
                float fx = x - x0;
                float fy = y - y0;
                float wx = Fade(fx);
                float wy = Fade(fy);

                float a = GradientDot(x0, y0, fx, fy, period, seed);
                float b = GradientDot(x0 + 1, y0, fx - 1f, fy, period, seed);
                float c = GradientDot(x0, y0 + 1, fx, fy - 1f, period, seed);
                float d = GradientDot(x0 + 1, y0 + 1, fx - 1f, fy - 1f, period, seed);
                return Lerp(Lerp(a, b, wx), Lerp(c, d, wx), wy) * 1.4f;
            }

            /// <summary>Tileable value noise in 0..1.</summary>
            public static float Value(float u, float v, int period, int seed)
            {
                float x = u * period;
                float y = v * period;
                int x0 = Mathf.FloorToInt(x);
                int y0 = Mathf.FloorToInt(y);
                float fx = x - x0;
                float fy = y - y0;
                float wx = Fade(fx);
                float wy = Fade(fy);

                float a = Hash01(Wrap(x0, period), Wrap(y0, period), seed);
                float b = Hash01(Wrap(x0 + 1, period), Wrap(y0, period), seed);
                float c = Hash01(Wrap(x0, period), Wrap(y0 + 1, period), seed);
                float d = Hash01(Wrap(x0 + 1, period), Wrap(y0 + 1, period), seed);
                return Lerp(Lerp(a, b, wx), Lerp(c, d, wx), wy);
            }

            /// <summary>Signed fBm in roughly -1..1; every octave doubles the lattice period.</summary>
            public static float Fbm(float u, float v, int period, int octaves, int seed)
            {
                float sum = 0f;
                float amplitude = 1f;
                float norm = 0f;
                int p = period;
                for (int o = 0; o < octaves; o++)
                {
                    sum += amplitude * Gradient(u, v, p, seed + o * 131);
                    norm += amplitude;
                    amplitude *= 0.5f;
                    p *= 2;
                }
                return sum / norm;
            }

            /// <summary>Ridged fBm in 0..1, used for angular stone.</summary>
            public static float Ridged(float u, float v, int period, int octaves, int seed)
            {
                float sum = 0f;
                float amplitude = 1f;
                float norm = 0f;
                int p = period;
                for (int o = 0; o < octaves; o++)
                {
                    float n = 1f - Mathf.Abs(Gradient(u, v, p, seed + o * 137));
                    n *= n;
                    sum += amplitude * n;
                    norm += amplitude;
                    amplitude *= 0.5f;
                    p *= 2;
                }
                return sum / norm;
            }

            public readonly struct WorleyCell
            {
                public readonly float F1;
                public readonly float F2;
                public readonly float Cell;

                public WorleyCell(float f1, float f2, float cell)
                {
                    F1 = f1;
                    F2 = f2;
                    Cell = cell;
                }
            }

            /// <summary>Tileable Worley noise: nearest and second-nearest feature distances.</summary>
            public static WorleyCell Worley(float u, float v, int period, int seed)
            {
                float x = u * period;
                float y = v * period;
                int cx = Mathf.FloorToInt(x);
                int cy = Mathf.FloorToInt(y);
                float fx = x - cx;
                float fy = y - cy;

                float f1 = 4f;
                float f2 = 4f;
                uint nearest = 0;
                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        uint h = Hash(Wrap(cx + ox, period), Wrap(cy + oy, period), seed);
                        float px = ox + (h & 0xFFFFu) * (1f / 65535f);
                        float py = oy + ((h >> 16) & 0xFFFFu) * (1f / 65535f);
                        float dx = px - fx;
                        float dy = py - fy;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d < f1)
                        {
                            f2 = f1;
                            f1 = d;
                            nearest = h;
                        }
                        else if (d < f2)
                        {
                            f2 = d;
                        }
                    }
                }
                return new WorleyCell(f1, f2, (nearest & 0x00FFFFFFu) * (1f / 16777215f));
            }
        }
    }
}
