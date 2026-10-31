using System.IO;
using Procedural.Generation;
using Procedural.World;
using UnityEngine;

namespace Procedural.Editor
{
    /// <summary>A top-down overview PNG: surface colors with hillshade, sea by depth, lakes, rivers.</summary>
    static class DebugMaps
    {
        static readonly Color Shallow = new Color(0.27f, 0.47f, 0.60f);
        static readonly Color Deep = new Color(0.05f, 0.14f, 0.28f);
        static readonly Color LakeColor = new Color(0.16f, 0.38f, 0.58f);
        static readonly Color RiverColor = new Color(0.10f, 0.33f, 0.72f);

        public static void WriteOverview(IslandResult result, IslandBuildAssets assets, string path)
        {
            var height = result.Height;
            int n = height.Width;
            float cell = height.CellSize;
            var layerColors = new Color[SurfaceLayer.Count];
            for (int l = 0; l < SurfaceLayer.Count; l++)
                layerColors[l] = LayerColor(assets, l);

            var light = new Vector3(-1f, 1.5f, -1f).normalized;
            var pixels = new Color[n * n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    float h = height[x, z];
                    Color c;
                    if (result.LakeId[z * n + x] >= 0)
                        c = LakeColor;
                    else if (h <= 0f)
                        c = Color.Lerp(Shallow, Deep, Mathf.Clamp01(-h / result.SeaDepth));
                    else
                    {
                        c = Color.black;
                        for (int l = 0; l < SurfaceLayer.Count; l++)
                            c += layerColors[l] * result.Surface[l][x, z];
                        float dx = height[Mathf.Min(x + 1, n - 1), z] - height[Mathf.Max(x - 1, 0), z];
                        float dz = height[x, Mathf.Min(z + 1, n - 1)] - height[x, Mathf.Max(z - 1, 0)];
                        var normal = new Vector3(-dx, 2f * cell, -dz).normalized;
                        c *= 0.35f + 0.75f * Mathf.Clamp01(Vector3.Dot(normal, light));
                    }
                    c.a = 1f;
                    pixels[z * n + x] = c;
                }

            foreach (var s in result.Rivers)
                for (int i = 0; i < s.X.Length; i++)
                {
                    int r = Mathf.Max(0, Mathf.RoundToInt(0.5f * s.Width[i] / cell));
                    int px = Mathf.RoundToInt(s.X[i] / cell), pz = Mathf.RoundToInt(s.Z[i] / cell);
                    for (int dz = -r; dz <= r; dz++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int x = px + dx, z = pz + dz;
                            if (x >= 0 && z >= 0 && x < n && z < n) pixels[z * n + x] = RiverColor;
                        }
                }

            Save(pixels, n, path);
        }

        /// <summary>A 0..1 field over land as a dry-to-wet ramp with hillshade; sea and lakes in blue.</summary>
        public static void WriteField(IslandResult result, Procedural.Generation.Grid field, string path)
        {
            var height = result.Height;
            int n = height.Width;
            float cell = height.CellSize;
            var dry = new Color(0.78f, 0.67f, 0.45f);
            var mid = new Color(0.45f, 0.62f, 0.33f);
            var wet = new Color(0.10f, 0.36f, 0.42f);
            var light = new Vector3(-1f, 1.5f, -1f).normalized;
            var pixels = new Color[n * n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    float h = height[x, z];
                    Color c;
                    if (result.LakeId[z * n + x] >= 0)
                        c = LakeColor;
                    else if (h <= 0f)
                        c = Deep;
                    else
                    {
                        float v = Mathf.Clamp01(field[x, z]);
                        c = v < 0.5f ? Color.Lerp(dry, mid, 2f * v) : Color.Lerp(mid, wet, 2f * v - 1f);
                        float dx = height[Mathf.Min(x + 1, n - 1), z] - height[Mathf.Max(x - 1, 0), z];
                        float dz = height[x, Mathf.Min(z + 1, n - 1)] - height[x, Mathf.Max(z - 1, 0)];
                        var normal = new Vector3(-dx, 2f * cell, -dz).normalized;
                        c *= 0.55f + 0.55f * Mathf.Clamp01(Vector3.Dot(normal, light));
                    }
                    c.a = 1f;
                    pixels[z * n + x] = c;
                }
            Save(pixels, n, path);
        }

        static void Save(Color[] pixels, int n, string path)
        {
            var texture = new Texture2D(n, n, TextureFormat.RGB24, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        static Color LayerColor(IslandBuildAssets assets, int layer)
        {
            var texture = assets.layers[layer] != null ? assets.layers[layer].diffuseTexture : null;
            return texture != null && texture.isReadable ? texture.GetPixel(0, 0) : Color.gray;
        }
    }
}
