using Realms.Core;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// A new game's map as a small picture for the new game's window (2026-11-12), painted from
    /// the made world alone before anything of it is built. What the players' kingdom knows at
    /// the start is in its colours, as the minimap paints it: the land and the water, the
    /// kingdom tinted with its flag's field, the towns white and the capital ringed in gold. The
    /// rest of the land is a grey shape on a dark sea, so the players can choose by eye between
    /// one great land and islands, and the neighbours and the monsters stay in the fog.
    /// </summary>
    public static class MapPicture
    {
        public const int Width = 400;

        static readonly Color Deep = Palette.Hex("#3F6694"), Shallow = Palette.Hex("#679EA9");
        static readonly Color FogSea = Palette.Hex("#2F3B4A"), FogLand = Palette.Hex("#7D796F"), FogCoast = Palette.Hex("#65625A");
        static readonly Color Gold = Palette.Hex("#E7C86A"), Ink = Palette.Hex("#1C1A18"), Beast = Palette.Hex("#2A2522");

        /// <summary>
        /// Paints <paramref name="setup"/>'s map into <paramref name="texture"/>, or a new texture
        /// when it is missing or of another size; returns the one painted.
        /// </summary>
        public static Texture2D Paint(GameState setup, Texture2D texture)
        {
            World world = setup.World;
            float minX = world.MinX, maxX = world.MaxX, minZ = world.MinZ, maxZ = world.MaxZ;
            int width = Width;
            int height = Mathf.Max(1, Mathf.RoundToInt(width * (maxZ - minZ) / (maxX - minX)));
            if (texture == null || texture.width != width || texture.height != height)
            {
                if (texture != null)
                    Object.Destroy(texture);
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "New game's map", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                };
            }

            // Each pixel's cell; past the world's ragged edges the nearest edge cell, as on the minimap.
            var cellOf = new int[width * height];
            for (int j = 0; j < height; j++)
            for (int i = 0; i < width; i++)
            {
                float x = Mathf.Lerp(minX, maxX, (i + 0.5f) / width);
                float z = Mathf.Lerp(minZ, maxZ, (j + 0.5f) / height);
                Hex hex = HexMetrics.FromWorld(x, z);
                cellOf[j * width + i] = world.IndexOf(Mathf.Clamp(hex.Col, 0, world.Width - 1), Mathf.Clamp(hex.Row, 0, world.Height - 1));
            }

            bool[] seen = setup.Seen;
            var pixels = new Color32[width * height];
            for (int j = 0; j < height; j++)
            for (int i = 0; i < width; i++)
            {
                int k = j * width + i;
                Cell cell = world.Cells[cellOf[k]];
                Color c;
                if (!seen[cell.Index])
                {
                    if (cell.IsWater)
                        c = FogSea;
                    else
                    {
                        // The unseen land's shape, its coast a little darker so it reads.
                        bool coast = false;
                        if (i + 1 < width && world.Cells[cellOf[k + 1]].IsWater) coast = true;
                        if (i > 0 && world.Cells[cellOf[k - 1]].IsWater) coast = true;
                        if (j + 1 < height && world.Cells[cellOf[k + width]].IsWater) coast = true;
                        if (j > 0 && world.Cells[cellOf[k - width]].IsWater) coast = true;
                        c = coast ? FogCoast : FogLand;
                    }
                }
                else
                {
                    c = cell.Biome == Biome.Ocean ? Deep : cell.IsWater ? Shallow : Palette.Ground(cell.Biome);
                    int o = cell.Owner;
                    if (o >= 0)
                    {
                        Color tint = FlagArt.Tinctures[Mathf.Clamp(world.Countries[o].Flag.Field, 0, FlagArt.Tinctures.Length - 1)];
                        c = Color.Lerp(c, tint, cell.IsWater ? 0.3f : 0.5f);
                        bool edge = false;
                        if (i + 1 < width && world.Cells[cellOf[k + 1]].Owner != o) edge = true;
                        if (i > 0 && world.Cells[cellOf[k - 1]].Owner != o) edge = true;
                        if (j + 1 < height && world.Cells[cellOf[k + width]].Owner != o) edge = true;
                        if (j > 0 && world.Cells[cellOf[k - width]].Owner != o) edge = true;
                        if (edge)
                            c = Color.Lerp(tint, Color.white, 0.25f);
                    }
                }
                pixels[k] = c;
            }

            void Dot(int cellIndex, int r, Color color)
            {
                int ci = Mathf.RoundToInt((world.CenterX(cellIndex) - minX) / (maxX - minX) * width);
                int cj = Mathf.RoundToInt((world.CenterZ(cellIndex) - minZ) / (maxZ - minZ) * height);
                for (int dj = -r; dj <= r; dj++)
                for (int di = -r; di <= r; di++)
                {
                    int i = ci + di, j = cj + dj;
                    if (i >= 0 && j >= 0 && i < width && j < height && di * di + dj * dj <= r * r + 1)
                        pixels[j * width + i] = color;
                }
            }
            foreach (Cell cell in world.Cells)
            {
                if (cell.Monster != Monster.None && seen[cell.Index])
                    Dot(cell.Index, 2, Beast);
            }
            int capital = world.Countries[world.Player].Capital;
            foreach (City city in world.Cities)
            {
                if (!seen[city.Cell])
                    continue;
                if (city.Index == capital)
                {
                    Dot(city.Cell, 6, Ink);
                    Dot(city.Cell, 5, Gold);
                    Dot(city.Cell, 3, Color.white);
                }
                else
                    Dot(city.Cell, city.Capital ? 3 : 2, Color.white);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }
    }
}
