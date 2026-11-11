using System;
using System.Collections.Generic;
using Realms.Core;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// What a town always has round the places the players build on; the terrain draws its
    /// trodden square and the lanes out to the places. An empty place is a kitchen garden. Where
    /// two places meet there is either a yard (a fruit tree, a woodpile, barrels or a haystack,
    /// often a fruit tree behind it) or a road with barrels and a cart beside it. With the level
    /// come lamps where the roads enter the square (2; 3 in the quarters), and in the centre a
    /// fountain instead of the well and flower beds round the paved square (3).
    /// </summary>
    public sealed partial class PropBuilder
    {
        static readonly Color Soil = Palette.Hex("#6A4F37"), SoilDark = Palette.Hex("#5A432F");
        static readonly Color Crop = Palette.Hex("#6B984A"), CropLight = Palette.Hex("#97BD64"), Pumpkin = Palette.Hex("#D98A3A");
        static readonly Color WattleTan = Palette.Hex("#94704A"), Cask = Palette.Hex("#8C6440"), Crate = Palette.Hex("#A9875C");
        static readonly Color Iron = Palette.Hex("#3A3530"), LampGlass = Palette.Hex("#F6DA8C"), Pool = Palette.Hex("#6497BE");
        static readonly Color[] Blooms = { Palette.Hex("#E0574A"), Palette.Hex("#F2C64C"), Palette.Hex("#F3EEE2"), Palette.Hex("#B77ACF") };

        /// <summary>The trodden square's radius, as the terrain shader draws it.</summary>
        static float SquareRadius(int level, bool centre) => centre ? 0.22f + 0.035f * level : 0.17f;

        /// <summary>A rectangle on the ground: its centre, half its sides, and its turn in degrees as <see cref="Place"/> turns a model.</summary>
        readonly struct Footprint
        {
            public readonly Vector2 Centre, Half;
            readonly Vector2 x, z;

            public Footprint(Vector2 centre, Vector2 half, float yaw)
            {
                Centre = centre;
                Half = half;
                // Turned by yaw about +y, the model's x side runs along (cos, -sin) and its z side along (sin, cos).
                float a = yaw * Mathf.Deg2Rad;
                x = new Vector2(Mathf.Cos(a), -Mathf.Sin(a));
                z = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            }

            /// <summary>How far the rectangle reaches from its centre along a unit direction.</summary>
            public float Reach(Vector2 dir) => Mathf.Abs(Vector2.Dot(dir, x)) * Half.x + Mathf.Abs(Vector2.Dot(dir, z)) * Half.y;

            /// <summary>
            /// The gap between two rectangles across the side that parts them most, negative where
            /// they overlap. It is never more than the true distance, so a gap above 0 keeps them apart.
            /// </summary>
            public float Gap(Footprint other) =>
                Mathf.Max(Mathf.Max(Apart(other, x), Apart(other, z)), Mathf.Max(Apart(other, other.x), Apart(other, other.z)));

            float Apart(Footprint other, Vector2 axis) => Mathf.Abs(Vector2.Dot(other.Centre - Centre, axis)) - Reach(axis) - other.Reach(axis);
        }

        readonly Dictionary<GameObject, Vector2> modelSides = new Dictionary<GameObject, Vector2>();

        /// <summary>
        /// A town model's two sides on the ground in its prefab (the largest side 1 long, centred on
        /// its root: Setup.ModelPrefab), read once from its meshes.
        /// </summary>
        Vector2 SidesOf(GameObject prefab)
        {
            if (modelSides.TryGetValue(prefab, out Vector2 sides))
                return sides;
            var box = new Bounds();
            bool any = false;
            Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                Bounds b = filter.sharedMesh.bounds;
                Matrix4x4 m = toRoot * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (any)
                        box.Encapsulate(p);
                    else
                        box = new Bounds(p, Vector3.zero);
                    any = true;
                }
            }
            sides = any ? new Vector2(box.size.x, box.size.z) : Vector2.one;
            modelSides[prefab] = sides;
            return sides;
        }

        /// <summary>A kitchen garden's sides with its fence (see <see cref="Garden"/>).</summary>
        static readonly Vector2 GardenSides = new Vector2(0.22f, 0.17f);

        /// <summary>How far a centre place's model keeps from the castle and the walls where it can.</summary>
        const float LotGap = 0.03f;

        /// <summary>Down to how small a centre place's model may stand to keep <see cref="LotGap"/>: four steps of 5%.</summary>
        const int ShrinkSteps = 4;

        /// <summary>The turns a centre place's model may stand at: as drawn, half of it, straight; or only as drawn.</summary>
        static readonly float[] FitTurns = { 1f, 0.5f, 0f }, DrawnTurn = { 1f };

        /// <summary>
        /// Where on its ray from the <paramref name="middle"/> (at <paramref name="degrees"/>) a
        /// centre place's model with <paramref name="sides"/> stands clear of the castle and the
        /// walls (<paramref name="crowd"/>): the spot nearest to where it was drawn that keeps
        /// <see cref="LotGap"/>, on dry land off the rivers like <see cref="LotAt"/>. A model that
        /// may move (<paramref name="free"/>) also turns less and, where no spot keeps the gap,
        /// stands smaller (<see cref="ShrinkSteps"/>), and in the end at the clearest spot. With no
        /// dry spot on the ray it stays where it was drawn.
        /// </summary>
        Vector2 Fit(List<Footprint> crowd, Vector2 middle, float degrees, Vector2 sides, Vector2 drawn, ref float size, ref float yaw, bool free)
        {
            var dir = new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
            float want = (drawn - middle).magnitude;
            for (int step = 0; step <= (free ? ShrinkSteps : 0); step++)
            {
                float scale = size * (1f - 0.05f * step), clearest = float.NegativeInfinity, nearest = float.PositiveInfinity;
                Vector2 clearSpot = drawn, nearSpot = drawn;
                float clearTurn = 1f, nearTurn = 1f;
                for (float r = Mathf.Max(0.3f, want - 0.22f); r <= want + 0.08f; r += 0.01f)
                {
                    Vector2 at = middle + dir * r;
                    if (field.Height(at.x, at.y) < 0.05f || NearRiver(at.x, at.y, 0.1f))
                        continue;
                    foreach (float turn in free ? FitTurns : DrawnTurn)
                    {
                        var shape = new Footprint(at, sides * (scale * 0.5f), yaw * turn);
                        float gap = float.PositiveInfinity;
                        foreach (Footprint thing in crowd)
                            gap = Mathf.Min(gap, shape.Gap(thing));
                        if (gap > clearest)
                        {
                            clearest = gap;
                            clearSpot = at;
                            clearTurn = turn;
                        }
                        if (gap >= LotGap && Mathf.Abs(r - want) < nearest)
                        {
                            nearest = Mathf.Abs(r - want);
                            nearSpot = at;
                            nearTurn = turn;
                        }
                    }
                }
                if (float.IsNegativeInfinity(clearest))
                    return drawn;
                bool kept = !float.IsPositiveInfinity(nearest);
                if (kept || step == (free ? ShrinkSteps : 0))
                {
                    size = scale;
                    yaw *= kept ? nearTurn : clearTurn;
                    return kept ? nearSpot : clearSpot;
                }
            }
            return drawn;
        }

        /// <summary>
        /// The town's lasting things on one of its cells, the <paramref name="centre"/> or a quarter,
        /// once its places are taken; the places stand <paramref name="ring"/> from the middle.
        /// </summary>
        void TownCore(City city, float cx, float cz, bool centre, float ring)
        {
            int level = Towns.Level(city);
            float square = SquareRadius(level, centre);
            bool grand = city.Capital || city.Buildings.Contains(TownBuilding.Castle);
            var middle = new Vector2(cx, cz);
            bool cart = false;
            for (int d = 0; d < 6; d++)
            {
                float degrees = d * 60f;
                var dir = new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
                var side = new Vector2(-dir.y, dir.x);
                if ((here.Roads >> d & 1) != 0)
                {
                    // A road: a lamp where it enters the square, barrels or a cart before the houses.
                    float off = RoadClear + 0.03f;
                    float flip = rng.Chance(0.5f) ? 1f : -1f;
                    if (level >= (centre ? 2 : 3))
                        TryPut(middle + dir * (square + 0.03f) + side * off * flip, 0.05f, Lamp);
                    Vector2 by = middle + dir * (square + 0.09f) - side * (off + 0.01f) * flip;
                    if (!cart && rng.Chance(0.4f))
                        cart = TryPut(by, 0.1f, p => Cart(p, degrees));
                    else if (rng.Chance(0.55f))
                        TryPut(by, 0.07f, Barrels);
                    continue;
                }
                // No road: a yard between the two places, a fruit tree behind it, flowers by the square.
                if (centre && level >= 3 && !grand)
                    TryPut(middle + dir * (square + 0.045f), 0.07f, p => FlowerBed(p, degrees));
                Vector2 yard = middle + dir * (ring - 0.1f);
                float pick = rng.Range(0f, 1f);
                if (pick < 0.3f)
                    TryPut(yard, 0.13f, FruitTree);
                else if (pick < 0.58f)
                    TryPut(yard, 0.11f, p => Woodpile(p, degrees + 90f));
                else if (pick < 0.8f)
                    TryPut(yard, 0.1f, Barrels);
                else
                    TryPut(yard, 0.12f, SmallStack);
                if (rng.Chance(0.55f))
                    TryPut(middle + dir * (ring + 0.06f), 0.14f, FruitTree);
            }
        }

        /// <summary>Puts a thing at <paramref name="p"/> when the spot is dry land clear of what stands.</summary>
        bool TryPut(Vector2 p, float gap, Action<Vector2> put)
        {
            if (field.Height(p.x, p.y) < 0.04f || NearRiver(p.x, p.y, 0f) || !Clear(p, gap))
                return false;
            placed.Add(p);
            put(p);
            return true;
        }

        /// <summary>
        /// An empty place: a kitchen garden of three rows (cabbages, sometimes pumpkins) in a
        /// wattle fence, open towards the middle at <paramref name="degrees"/> from it.
        /// </summary>
        void Garden(Vector2 at, float degrees)
        {
            const float Width = 0.2f, Depth = 0.15f;
            float yaw = 90f - degrees;
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            Vector3 o = Ground(at.x, at.y);
            MeshKit kit = Kit();
            kit.Box(o - new Vector3(0, 0.008f, 0), new Vector3(Width, 0.014f, Depth), yaw, Soil);
            bool pumpkins = rng.Chance(0.4f);
            for (int row = 0; row < 3; row++)
            {
                float z = (row - 1) * 0.045f;
                kit.Box(o + q * new Vector3(0, 0.004f, z), new Vector3(Width * 0.86f, 0.008f, 0.024f), yaw, SoilDark);
                for (int k = 0; k < 5; k++)
                {
                    float x = (k - 2) * 0.034f + rng.Range(-0.004f, 0.004f);
                    bool pumpkin = pumpkins && row == 1 && k % 2 == 0;
                    float s = pumpkin ? 0.014f : rng.Range(0.009f, 0.013f);
                    Color top = pumpkin ? Pumpkin : Color.Lerp(Crop, CropLight, rng.Range(0f, 1f));
                    kit.Blob(o + q * new Vector3(x, 0.012f + s * 0.5f, z), new Vector3(s, s * 0.8f, s), 5, 2, top * 0.78f, top, rng, 0.15f);
                }
            }
            Vector3 Corner(float x, float z) => o + q * new Vector3(x * Width * 0.5f, 0, z * Depth * 0.5f);
            Wattle(kit, Corner(-1.05f, -1.05f), Corner(-1.05f, 1.1f));
            Wattle(kit, Corner(-1.05f, 1.1f), Corner(1.05f, 1.1f));
            Wattle(kit, Corner(1.05f, 1.1f), Corner(1.05f, -1.05f));
        }

        /// <summary>A low wattle fence from <paramref name="a"/> to <paramref name="b"/>: posts and two rails.</summary>
        void Wattle(MeshKit kit, Vector3 a, Vector3 b)
        {
            int posts = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.05f) + 1);
            Vector3 previous = default;
            for (int k = 0; k < posts; k++)
            {
                Vector3 p = Vector3.Lerp(a, b, k / (float)(posts - 1));
                p = Ground(p.x, p.z);
                kit.Cylinder(p - new Vector3(0, 0.01f, 0), p + new Vector3(0, 0.042f, 0), 0.0045f, 4, WattleTan, WattleTan * 1.1f);
                if (k > 0)
                {
                    foreach (float h in new[] { 0.017f, 0.033f })
                        kit.Cylinder(previous + new Vector3(0, h, 0), p + new Vector3(0, h, 0), 0.003f, 4, WattleTan * 0.92f, WattleTan);
                }
                previous = p;
            }
        }

        /// <summary>The trees of a town's gardens: the young round ones of the studio set.</summary>
        static readonly string[] GardenTrees = { "young_round_2", "young_round_3" };

        /// <summary>
        /// A garden tree: a young round studio tree, the earlier linden while the art has none, the
        /// low-poly broadleaf without studio trees. Half of them used to ask for an oak the art no
        /// longer has and stood as flat bright-green balls between the houses (review of 2026-11-10).
        /// </summary>
        void FruitTree(Vector2 p)
        {
            Vector3 at = Ground(p.x, p.y);
            float s = rng.Range(0.55f, 0.68f);
            if (!StudioTree(Any(GardenTrees) ?? Any(OldTrees), at, s))
                Broadleaf(at, s);
        }

        /// <summary>Split logs stacked four, three and two high, lying across <paramref name="degrees"/>.</summary>
        void Woodpile(Vector2 p, float degrees)
        {
            Vector3 at = Ground(p.x, p.y);
            Quaternion q = Quaternion.Euler(0, 90f - degrees, 0);
            MeshKit kit = Kit();
            const float R = 0.011f;
            int[] rows = { 4, 3, 2 };
            for (int row = 0; row < rows.Length; row++)
            {
                for (int k = 0; k < rows[row]; k++)
                {
                    float x = (k - (rows[row] - 1) * 0.5f) * R * 2.1f;
                    Vector3 c = at + q * new Vector3(x, R + row * R * 1.8f, 0);
                    Vector3 axis = q * new Vector3(0, 0, 0.045f + rng.Range(-0.006f, 0.006f));
                    kit.Cylinder(c - axis, c + axis, R, 6, Palette.Trunk, Palette.Hay);
                }
            }
        }

        /// <summary>How big the studio decor stands: the casks' group and a stall along their long side, a cart's length, a lamp's and a signpost's height.</summary>
        const float BarrelsLength = 0.09f, StallLength = 0.13f, CartLength = 0.17f, LampHeight = 0.15f, SignpostHeight = 0.18f;

        /// <summary>How wide the studio fountain stands, a little wider than the basin of shapes.</summary>
        const float FountainSize = 0.22f;

        /// <summary>Two or three casks and a crate: the studio group turned at most 60 degrees from as drawn, or casks of shapes.</summary>
        void Barrels(Vector2 p)
        {
            if (StudioThing("barrels", p, BarrelsLength, rng.Range(-60f, 60f), true))
                return;
            MeshKit kit = Kit();
            int count = rng.Range(2, 4);
            for (int k = 0; k < count; k++)
            {
                float a = k * 2.3f + rng.Range(0f, 0.6f);
                Vector3 at = Ground(p.x + Mathf.Cos(a) * 0.024f, p.y + Mathf.Sin(a) * 0.024f) - new Vector3(0, 0.004f, 0);
                kit.Lathe(at, new[] { 0f, 0.012f, 0.03f, 0.042f }, new[] { 0.014f, 0.017f, 0.017f, 0.014f },
                    new[] { Cask * 0.9f, Cask, Cask * 0.9f }, 7, rng);
            }
            float b = rng.Range(0f, Mathf.PI * 2f);
            kit.Box(Ground(p.x + Mathf.Cos(b) * 0.05f, p.y + Mathf.Sin(b) * 0.05f) - new Vector3(0, 0.004f, 0),
                new Vector3(0.03f, 0.028f, 0.03f), rng.Range(0f, 90f), Crate);
        }

        /// <summary>A small haystack.</summary>
        void SmallStack(Vector2 p)
        {
            float s = rng.Range(0.5f, 0.62f);
            Kit().Lathe(Ground(p.x, p.y) - new Vector3(0, 0.008f, 0),
                new[] { 0f, 0.07f * s, 0.13f * s, 0.17f * s }, new[] { 0.085f * s, 0.09f * s, 0.06f * s, 0f },
                new[] { Palette.HayDark, Palette.Hay, Palette.Hay }, 8, rng, 0.04f, 0.05f);
        }

        /// <summary>A two-wheeled cart along a road at <paramref name="degrees"/>, loaded with hay (or casks, of shapes).</summary>
        void Cart(Vector2 p, float degrees)
        {
            if (StudioAlong("cart", p, degrees + rng.Range(-15f, 15f), CartLength))
                return;
            float yaw = -degrees + rng.Range(-15f, 15f);
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            Vector3 o = Ground(p.x, p.y);
            MeshKit kit = Kit();
            kit.Box(o + q * new Vector3(0, 0.024f, 0), new Vector3(0.1f, 0.02f, 0.058f), yaw, Cask);
            foreach (float z in new[] { -0.036f, 0.036f })
            {
                kit.Cylinder(o + q * new Vector3(0, 0.024f, z), o + q * new Vector3(0, 0.024f, z * 1.17f), 0.024f, 8, Palette.Wood, Cask * 0.8f);
                kit.Cylinder(o + q * new Vector3(0.05f, 0.03f, z * 0.55f), o + q * new Vector3(0.12f, 0.012f, z * 0.55f), 0.003f, 4, Cask, Cask);
            }
            if (rng.Chance(0.5f))
                kit.Blob(o + q * new Vector3(-0.005f, 0.056f, 0), new Vector3(0.045f, 0.022f, 0.027f), 6, 3, Palette.HayDark, Palette.Hay, rng, 0.1f);
            else
            {
                foreach (float x in new[] { -0.025f, 0.02f })
                    kit.Lathe(o + q * new Vector3(x, 0.044f, 0), new[] { 0f, 0.012f, 0.03f }, new[] { 0.014f, 0.016f, 0.014f },
                        new[] { Cask, Cask * 0.9f }, 7, rng);
            }
        }

        /// <summary>A street lamp: the studio one on its wooden post, or an iron post and a lit lantern under a little cap.</summary>
        void Lamp(Vector2 p)
        {
            if (StudioThing("lamp", p, LampHeight, rng.Range(0f, 360f)))
                return;
            Vector3 o = Ground(p.x, p.y);
            MeshKit kit = Kit();
            kit.Cylinder(o - new Vector3(0, 0.01f, 0), o + new Vector3(0, 0.115f, 0), 0.0042f, 5, Iron, Iron);
            kit.Box(o + new Vector3(0, 0.112f, 0), new Vector3(0.017f, 0.024f, 0.017f), 45f, LampGlass);
            kit.Lathe(o + new Vector3(0, 0.136f, 0), new[] { 0f, 0.012f }, new[] { 0.015f, 0f }, new[] { Iron }, 6, rng);
        }

        /// <summary>A flower bed by the square, along its edge.</summary>
        void FlowerBed(Vector2 p, float degrees)
        {
            float yaw = 90f - degrees;
            Quaternion q = Quaternion.Euler(0, yaw, 0);
            Vector3 o = Ground(p.x, p.y);
            MeshKit kit = Kit();
            kit.Box(o - new Vector3(0, 0.004f, 0), new Vector3(0.075f, 0.014f, 0.034f), yaw, Palette.Boulder);
            Color first = Blooms[rng.Range(0, Blooms.Length)], second = Blooms[rng.Range(0, Blooms.Length)];
            for (int k = 0; k < 6; k++)
            {
                Vector3 at = o + q * new Vector3((k % 3 - 1) * 0.022f, 0.012f, (k / 3 - 0.5f) * 0.014f);
                kit.Blob(at, new Vector3(0.011f, 0.007f, 0.011f), 5, 2, Crop * 0.8f, Crop, rng, 0.1f);
                Color bloom = k % 2 == 0 ? first : second;
                kit.Blob(at + new Vector3(0, 0.007f, 0), new Vector3(0.006f, 0.004f, 0.006f), 4, 2, bloom * 0.9f, bloom, rng, 0.1f);
            }
        }

        /// <summary>A town's fountain: a stone basin of water and a bowl on a column in it, the studio one when the art has it.</summary>
        void Fountain(float cx, float cz)
        {
            GameObject model = art != null ? art.ModelOf("fountain") : null;
            if (model != null)
            {
                Place(model, cx, cz, FountainSize, rng.Range(-30f, 30f), false);
                return;
            }
            Vector3 o = Ground(cx, cz);
            MeshKit kit = Kit();
            Color stone = Palette.Boulder;
            kit.Lathe(o - new Vector3(0, 0.012f, 0), new[] { 0f, 0.055f }, new[] { 0.105f, 0.1f }, new[] { stone }, 12, rng);
            kit.Lathe(o + new Vector3(0, 0.042f, 0), new[] { 0f, 0.004f }, new[] { 0.086f, 0.086f }, new[] { Pool }, 12, rng);
            kit.Cylinder(o + new Vector3(0, 0.04f, 0), o + new Vector3(0, 0.1f, 0), 0.011f, 6, stone, stone);
            kit.Lathe(o + new Vector3(0, 0.088f, 0), new[] { 0f, 0.016f }, new[] { 0.012f, 0.036f }, new[] { stone }, 8, rng);
            kit.Lathe(o + new Vector3(0, 0.103f, 0), new[] { 0f, 0.002f }, new[] { 0.03f, 0.03f }, new[] { Pool }, 8, rng);
        }
    }
}
