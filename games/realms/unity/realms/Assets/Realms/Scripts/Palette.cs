using Realms.Core;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// The game's colours as they should look on screen (sRGB). Materials convert their colour
    /// properties; vertex colours and shader globals are converted with <c>.linear</c> by whoever
    /// writes them. The land, trees and rocks are muted, natural colours a little calmer than the
    /// Studio models; full colour is kept for what the player has to find at a glance: the
    /// kingdoms, the selection and the paths.
    /// </summary>
    public static class Palette
    {
        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;

        /// <summary>
        /// Kingdom colours by the kingdom's index: the fields of their flags
        /// (<see cref="FlagArt.Refresh"/>), at first these.
        /// </summary>
        public static readonly Color[] Countries =
        {
            Hex("#D04141"), Hex("#3674D0"), Hex("#E3BD44"), Hex("#36B391"), Hex("#944ECF"), Hex("#E78537"),
        };

        /// <summary>Art keys of the kingdoms' shields, as in shield_red.</summary>
        public static readonly string[] CountryKeys = { "red", "blue", "yellow", "green", "violet", "orange" };

        public static readonly Color Meadow = Hex("#9ABC74");
        /// <summary>The steppe, muted (Oklab chroma ×0.7 of #CABA80, #B3B47A, #C5A673) so it reads as dry grass, not ochre.</summary>
        public static readonly Color Plains = Hex("#C5BA93");
        public static readonly Color PlainsGreen = Hex("#B2B38B");
        public static readonly Color PlainsDry = Hex("#BDA885");
        public static readonly Color MeadowGold = Hex("#ACBE77");
        public static readonly Color MeadowDeep = Hex("#83AC6E");
        /// <summary>Muted under the trees (Oklab chroma ×0.8, lightness ×0.95 of #6D975F), so the crowns stand out.</summary>
        public static readonly Color ForestFloor = Hex("#6A8B5F");
        public static readonly Color Hills = Hex("#AAB67C");
        public static readonly Color Rock = Hex("#A39E95");
        public static readonly Color RockDark = Hex("#7E7970");
        public static readonly Color Snow = Hex("#ECEEF0");
        public static readonly Color Sand = Hex("#D8CEAC");
        public static readonly Color Seabed = Hex("#547D9A");
        /// <summary>Multiplies the grass along a river: lusher and a little darker.</summary>
        public static readonly Color RiverGrass = new Color(0.84f, 0.97f, 0.8f, 1f);
        public static readonly Color Earth = Hex("#C0B298");
        public static readonly Color LairGround = Hex("#7C7167");
        public static readonly Color Field = Hex("#D1BE80");

        public static readonly Color Trunk = Hex("#654E3E");
        public static readonly Color Pine = Hex("#3D6B49");
        public static readonly Color PineLight = Hex("#508358");
        public static readonly Color Leaves = Hex("#5B8E4E");
        public static readonly Color LeavesLight = Hex("#75A05A");
        /// <summary>What some trees lean towards: an olive green.</summary>
        public static readonly Color LeavesOlive = Hex("#7D8650");
        public static readonly Color Boulder = Hex("#99938A");
        public static readonly Color Hay = Hex("#CDB379");
        public static readonly Color HayDark = Hex("#B69764");
        public static readonly Color Gold = Hex("#E7C86A");
        public static readonly Color Wood = Hex("#4D4138");
        /// <summary>Weathered wood for the banners' poles: dark wood read as a black stick.</summary>
        public static readonly Color Pole = Hex("#836C55");
        public static readonly Color Wall = Hex("#D9D0BE");
        public static readonly Color Roof = Hex("#A86453");

        /// <summary>The ground's colour in full sun.</summary>
        public static Color Ground(Biome biome)
        {
            switch (biome)
            {
                case Biome.Beach: return Sand;
                case Biome.Meadow: return Meadow;
                case Biome.Plains: return Plains;
                case Biome.Forest: return ForestFloor;
                case Biome.Hills: return Hills;
                case Biome.Mountains: return Rock;
                default: return Sand;
            }
        }

        /// <summary>
        /// A biome's ground shifted by <paramref name="v"/> in [-1, 1]: towards greener steppe or
        /// deeper meadow above 0, drier steppe or yellower meadow below.
        /// </summary>
        public static Color Ground(Biome biome, float v)
        {
            Color c = Ground(biome);
            if (biome == Biome.Plains)
                return v > 0 ? Color.Lerp(c, PlainsGreen, v) : Color.Lerp(c, PlainsDry, -v);
            if (biome == Biome.Meadow)
                return v > 0 ? Color.Lerp(c, MeadowDeep, v) : Color.Lerp(c, MeadowGold, -v);
            return c;
        }
    }
}
