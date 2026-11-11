using System.Collections;
using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Realms
{
    /// <summary>
    /// The heroes on the map: a figure with the kingdom's banner for each (and the settlers' cart
    /// behind a hero who leads one; a waiting cart stands beside its town), a gold ring on the land
    /// under the chosen one, its way as a row of dots on dark discs (cream for this turn, orange for
    /// the turns after, a ring where this turn's move ends short of the goal, a post at the goal,
    /// red when a fight waits there), and the hop from cell to cell. On a town, a place or a
    /// resource a hero stands on a road at the cell's edge, not among what stands there.
    /// </summary>
    public sealed class HeroView : MonoBehaviour
    {
        /// <summary>A hero's figure on the map, a cell's radius being 1.</summary>
        public float size = 0.64f;

        /// <summary>Seconds for one step.</summary>
        public float stepTime = 0.24f;

        static readonly Color Now = Palette.Hex("#F4EBD3"), Later = Palette.Hex("#E3AE56"), Fight = Palette.Hex("#C84D4A"),
            Edge = Palette.Hex("#2B2620");

        /// <summary>How far from a busy cell's centre a hero stands on one of its roads.</summary>
        public const float RoadStand = 0.52f;

        /// <summary>How far in front of a town's middle a hero stands on its square (see <see cref="Stand"/>).</summary>
        public const float SquareStand = 0.6f;

        /// <summary>
        /// The roads a hero takes on a busy cell, best first: towards the viewer (south-west,
        /// south-east), then the sides, then the back, where the cell's buildings would hide the figure.
        /// </summary>
        static readonly int[] StandRoads = { 4, 5, 0, 3, 1, 2 };

        /// <summary>
        /// The chosen hero's ring and the flat marks of its way draw over whatever stands on the
        /// land (under tree crowns and behind rocks the way was lost, review of 2026-11-10); the
        /// heroes and their carts draw after them, so the ring's far arc or a dot behind a figure
        /// does not cross it. Both stay in the opaque queues, before water, fog and clouds.
        /// </summary>
        const int OverlayQueue = 2460, HeroQueue = 2470;

        GameState state;
        MapView view;
        ArtLibrary art;
        Material material, overlay;
        readonly Dictionary<Material, Material> late = new Dictionary<Material, Material>();
        Transform root;
        readonly Dictionary<int, Transform> figures = new Dictionary<int, Transform>();

        /// <summary>The carts behind heroes (by hero) and beside towns (by town).</summary>
        readonly Dictionary<int, GameObject> carts = new Dictionary<int, GameObject>(), townCarts = new Dictionary<int, GameObject>();
        GameObject ring, path;
        Hero chosen;

        /// <summary>Removes the figures and their meshes (before the map makes way for a new one).</summary>
        public void Clear()
        {
            if (root != null)
                Figures.Destroy(root.gameObject);
            root = null;
            path = ring = null;
            figures.Clear();
            carts.Clear();
            townCarts.Clear();
        }

        public void Show(GameState gameState, MapView mapView, ArtLibrary library, Material propMaterial)
        {
            Clear();
            state = gameState;
            view = mapView;
            art = library;
            if (material != propMaterial && overlay != null)
            {
                Destroy(overlay);
                overlay = null;
            }
            material = propMaterial;
            figures.Clear();
            chosen = null;
            // Under the map's objects: hidden with them in a battle, gone with them on a new map.
            root = new GameObject("Heroes").transform;
            root.SetParent(view.Content, false);
            Refresh();
        }

        /// <summary>A town's waiting settlers' cart stands no taller than a cottage's ground floor (review of 2026-11-11).</summary>
        const float TownCartLength = 0.2f;

        /// <summary>
        /// How tall the pole of the kingdom's small flag on a town's waiting cart is: without it the
        /// cart looked like the carts the towns have among their houses (2026-11-11).
        /// </summary>
        const float TownCartFlag = 0.3f;

        /// <summary>How long the hero's horse is on the map: the settlers' cart behind him is as long.</summary>
        float HorseLength()
        {
            GameObject prefab = art != null ? art.ModelOf("hero") : null;
            return (prefab != null ? Figures.GroundSide(prefab) : 0.8f) * size;
        }

        /// <summary>Makes figures for new heroes and puts every figure on its hero's cell.</summary>
        public void Refresh()
        {
            if (state == null)
                return;
            foreach (Hero hero in state.Heroes)
            {
                if (!figures.TryGetValue(hero.Index, out Transform figure))
                {
                    figure = Figures.Hero(art, material, root, size, hero.Country).transform;
                    figure.name = "Hero " + hero.Label;
                    figures[hero.Index] = figure;
                    DrawLate(figure.gameObject);
                }
                figure.localPosition = Spot(hero.Cell);
                carts.TryGetValue(hero.Index, out GameObject cart);
                if (hero.Settlers && cart == null)
                {
                    cart = Figures.Cart(art, material, figure, HorseLength());
                    cart.transform.localPosition = new Vector3(0.05f, 0f, 0.78f) * size;
                    carts[hero.Index] = cart;
                    DrawLate(cart);
                }
                else if (!hero.Settlers && cart != null)
                {
                    Figures.Destroy(cart);
                    carts.Remove(hero.Index);
                }
            }
            foreach (City city in state.World.Cities)
            {
                townCarts.TryGetValue(city.Index, out GameObject cart);
                if (!city.Settlers)
                {
                    if (cart != null)
                    {
                        Figures.Destroy(cart);
                        townCarts.Remove(city.Index);
                    }
                    continue;
                }
                if (cart == null)
                    townCarts[city.Index] = cart = TownCart(city);
                cart.transform.localPosition = TownCartAt(city);
            }
            Choose(chosen);
        }

        /// <summary>
        /// A town's settlers' cart waiting for a hero, three quarters to the viewer, with the
        /// kingdom's small flag at its back, its cloth flying east like the towns' banners.
        /// </summary>
        GameObject TownCart(City city)
        {
            var cart = new GameObject("Settlers' cart");
            cart.transform.SetParent(root, false);
            Quaternion turn = Quaternion.Euler(0f, 70f, 0f);
            Figures.Cart(art, material, cart.transform, TownCartLength).transform.localRotation = turn;
            Transform flag = new GameObject("Settlers' flag").transform;
            flag.SetParent(cart.transform, false);
            flag.localPosition = turn * new Vector3(0f, 0f, TownCartLength * 0.42f);
            flag.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Figures.Banner(material, flag, Vector3.zero, TownCartFlag / Figures.BannerHeight, city.Country);
            return cart;
        }

        /// <summary>
        /// Where a town's waiting cart stands: on its square, where the hero who takes it will stand
        /// (by the hall it stood among the houses, 2026-11-11), or beside a hero who stands there
        /// already, leading a cart of his own (on the side away from the town's banner).
        /// </summary>
        Vector3 TownCartAt(City city)
        {
            World world = state.World;
            Vector2 stand = Stand(world, view.Field, city.Cell);
            if (state.HeroAt(city.Cell) != null)
                stand += new Vector2(-BannerSide(stand) * 0.26f, 0.04f);
            float x = world.CenterX(city.Cell) + stand.x, z = world.CenterZ(city.Cell) + stand.y;
            return new Vector3(x, Ground(x, z), z);
        }

        /// <summary>
        /// The side of a town's square its banner takes (+1 east, −1 west; PropBuilder.City) for a
        /// hero standing at <paramref name="stand"/> from its middle: east of one at the front, as
        /// its cloth flies east, else away from him.
        /// </summary>
        public static float BannerSide(Vector2 stand) => stand.y < -0.3f ? 1f : stand.x > 0f ? -1f : 1f;

        /// <summary>Lays the gold ring on the land under the hero, or takes it away.</summary>
        public void Choose(Hero hero)
        {
            chosen = hero;
            if (root == null)
                return;
            if (ring != null)
                Figures.Destroy(ring);
            ring = null;
            if (hero == null || !figures.TryGetValue(hero.Index, out Transform figure))
                return;
            Vector3 at = figure.localPosition;
            ring = Figures.Built(Ring(at), "Figure ring", Overlay(), root);
            ring.transform.localPosition = at;
            ring.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>The prop material drawing over everything, after the land's objects (see <see cref="OverlayQueue"/>).</summary>
        Material Overlay()
        {
            if (overlay == null)
            {
                overlay = new Material(material) { name = "Hero marks", renderQueue = OverlayQueue };
                overlay.SetFloat("_ZTest", (float)CompareFunction.Always);
            }
            return overlay;
        }

        /// <summary>Gives the figure's renderers copies of their materials drawn after the marks, one copy per material.</summary>
        void DrawLate(GameObject figure)
        {
            foreach (Renderer renderer in figure.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = Late(materials[i]);
                renderer.sharedMaterials = materials;
            }
        }

        /// <summary>A copy of the material drawn after the marks, made once per material.</summary>
        Material Late(Material m)
        {
            if (m == null || m.renderQueue == HeroQueue)
                return m;
            if (!late.TryGetValue(m, out Material copy))
                late[m] = copy = new Material(m) { name = m.name + " (after the marks)", renderQueue = HeroQueue };
            return copy;
        }

        /// <summary>Where a hero stands on a cell (see <see cref="Stand"/>).</summary>
        public Vector3 Spot(int cell)
        {
            World world = state.World;
            Vector2 stand = Stand(world, view.Field, cell);
            float x = world.CenterX(cell) + stand.x, z = world.CenterZ(cell) + stand.y;
            return new Vector3(x, Ground(x, z), z);
        }

        /// <summary>
        /// Where a hero stands on a cell, from its centre: on a town's square in front of its
        /// houses, towards the viewer (unless the water or a river reaches there), and in the
        /// middle of a town's quarter, where its roads meet (on a road among the houses a hero
        /// hid them and blended with them, review of 2026-11-10); on a place or a resource out of
        /// the way of what stands there (see <see cref="StandAngle"/>); in the middle of open land.
        /// PropBuilder keeps the town's banner, stall and yards clear of the spot.
        /// </summary>
        public static Vector2 Stand(World world, TerrainField field, int cell)
        {
            Cell c = world.Cells[cell];
            if (c.City >= 0)
            {
                var front = new Vector2(0f, -SquareStand);
                float x = world.CenterX(cell) + front.x, z = world.CenterZ(cell) + front.y;
                if (field == null || (field.Height(x, z) > 0.04f && field.RiverEdge(x, z) > RiverCourse.Bank + 0.08f))
                    return front;
            }
            else if (c.District >= 0)
                return Vector2.zero;
            if (c.Town >= 0 || c.Place != Core.Place.None || c.Resource != Resource.None)
            {
                float angle = StandAngle(c);
                return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * RoadStand;
            }
            return Vector2.zero;
        }

        /// <summary>
        /// Turns the hero's figure three quarters towards a camera facing <paramref name="yaw"/>,
        /// looking to the screen's right, into the view: the hero screen holds the hero left of
        /// its panel, and from behind the figure read as a back (review of 2026-11-10). The next
        /// hop turns it along the way again.
        /// </summary>
        public void Face(Hero hero, float yaw)
        {
            if (hero == null || !figures.TryGetValue(hero.Index, out Transform figure))
                return;
            float a = yaw * Mathf.Deg2Rad, turn = 40f * Mathf.Deg2Rad;
            var forward = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            var right = new Vector2(forward.y, -forward.x);
            Vector2 way = -forward * Mathf.Cos(turn) + right * Mathf.Sin(turn);
            figure.localRotation = Quaternion.Euler(0f, Mathf.Atan2(-way.x, -way.y) * Mathf.Rad2Deg + Figures.HeroFacing(art), 0f);
        }

        /// <summary>Where the hero's figure stands now, or null while it has none.</summary>
        public Vector3? FigureAt(Hero hero)
        {
            if (hero == null || !figures.TryGetValue(hero.Index, out Transform figure) || figure == null)
                return null;
            return figure.position;
        }

        /// <summary>
        /// The way from a busy cell's centre to where a hero stands on it (<see cref="RoadStand"/>
        /// away), in radians: along the first of the cell's roads in <see cref="StandRoads"/>, else
        /// towards the viewer and to the left. A town's banner and stall stand on the other side
        /// of its square (PropBuilder.City); straight in front, a hero on a new town's square stood
        /// in the flag.
        /// </summary>
        public static float StandAngle(Cell c)
        {
            foreach (int d in StandRoads)
                if ((c.Roads & (1 << d)) != 0)
                    return d * Mathf.PI / 3f;
            return 4 * Mathf.PI / 3f;
        }

        /// <summary>The hero's figure hops to a neighbouring cell, turning to face the way.</summary>
        public IEnumerator Hop(Hero hero, int cell)
        {
            if (!figures.TryGetValue(hero.Index, out Transform figure))
                yield break;
            Vector3 from = figure.localPosition, to = Spot(cell);
            Vector3 way = to - from;
            // Head first along the way.
            if (way.sqrMagnitude > 1e-4f)
                figure.localRotation = Quaternion.Euler(0f, Mathf.Atan2(-way.x, -way.z) * Mathf.Rad2Deg + Figures.HeroFacing(art), 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / stepTime)
            {
                Vector3 p = Vector3.Lerp(from, to, t);
                p.y = Mathf.Max(view.Field.Height(p.x, p.z), 0f) + 0.07f * Mathf.Sin(Mathf.PI * t);
                figure.localPosition = p;
                if (hero == chosen && ring != null)
                    ring.transform.localPosition = new Vector3(p.x, p.y - 0.07f * Mathf.Sin(Mathf.PI * t), p.z);
                yield return null;
            }
            figure.localPosition = to;
            Choose(chosen);
        }

        /// <summary>
        /// Shows the hero's way from the step <paramref name="from"/> on, or nothing: from where the
        /// hero stands to where the hero will stand, a dot between cells and one on each cell.
        /// </summary>
        public void ShowPath(Hero hero, HeroPath way, int from = 0)
        {
            if (path != null)
                Figures.Destroy(path);
            path = null;
            if (way == null || from >= way.Cells.Count)
                return;
            World world = state.World;
            var kit = new MeshKit();
            var rng = new Rng(5);
            int last = way.Cells.Count - 1;
            bool fight = world.Cells[way.Target].Monster != Monster.None;
            // Where this turn's move ends, when the way goes on after it.
            int stop = -1;
            if (way.Turns[last] > 0)
                for (int k = last; k >= from && stop < 0; k--)
                    if (way.Turns[k] == 0)
                        stop = k;
            // The goal's cell outlined, red when a fight waits there; first, so the dots lie over it.
            Color mark = fight ? Fight : way.Turns[last] == 0 ? Now : Later;
            Outline(kit, way.Target, mark);
            Vector2 previous = Flat(Spot(from == 0 ? hero.Cell : way.Cells[from - 1]));
            for (int k = from; k <= last; k++)
            {
                Vector2 at = k == last ? Flat(Spot(way.Cells[k])) : Centre(way.Cells[k]);
                Color colour = way.Turns[k] == 0 ? Now : Later;
                // Three dots a cell: two on the way to it and one in its middle (a third larger
                // since they draw over crowns and roofs, review of 2026-11-10).
                for (int d = 1; d <= 2; d++)
                    Dot(kit, Foot(Vector2.Lerp(previous, at, d / 3f), 0.12f), 0.09f, colour, rng);
                if (k == stop)
                {
                    // This turn's move ends here: a light ring twice a dot's size, with a dark middle.
                    Vector3 top = Dot(kit, Foot(at, 0.34f), 0.26f, Now, rng);
                    Disc(kit, top, 0.14f, 0.008f, Edge, rng);
                }
                else if (k < last)
                    Dot(kit, Foot(at, 0.17f), 0.13f, colour, rng);
                previous = at;
            }
            // At the goal a dot and a post with a knob. The post keeps to the land's depth like
            // the figures (drawn after the marks), so it stands on its dot and a monster group in
            // front of it still hides it.
            Vector3 foot = Dot(kit, Foot(previous, 0.2f), 0.14f, mark, rng);
            var post = new MeshKit();
            post.Cylinder(foot, foot + new Vector3(0, 0.36f, 0), 0.022f, 6, Edge, Edge);
            post.Blob(foot + new Vector3(0, 0.38f, 0), new Vector3(0.075f, 0.075f, 0.075f), 7, 3, mark * 0.8f, mark, rng, 0f);
            path = Figures.Built(kit, "Figure path", Overlay(), root);
            path.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Figures.Built(post, "Figure path post", Late(material), path.transform)
                .GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        Vector2 Centre(int cell) => new Vector2(state.World.CenterX(cell), state.World.CenterZ(cell));

        /// <summary>A cell outlined on its land a little inside its edge: a light band on a wider dark one.</summary>
        void Outline(MeshKit kit, int cell, Color colour)
        {
            Vector2 c = Centre(cell);
            Band(kit, c, 0.76f, 0.93f, 0.03f, Edge);
            Band(kit, c, 0.79f, 0.9f, 0.045f, colour);
        }

        /// <summary>A hexagonal band round the centre between two shares of a cell's radius, following the land.</summary>
        void Band(MeshKit kit, Vector2 c, float inner, float outer, float lift, Color colour)
        {
            const int steps = 4;
            Vector3 On(Vector2 at) => new Vector3(at.x, Ground(at.x, at.y) + lift, at.y);
            for (int k = 0; k < 6; k++)
            {
                var a = new Vector2(HexMetrics.CornerX[k], HexMetrics.CornerZ[k]);
                var b = new Vector2(HexMetrics.CornerX[(k + 1) % 6], HexMetrics.CornerZ[(k + 1) % 6]);
                for (int s = 0; s < steps; s++)
                {
                    Vector2 p = Vector2.Lerp(a, b, s / (float)steps), q = Vector2.Lerp(a, b, (s + 1) / (float)steps);
                    kit.Quad(On(c + p * inner), On(c + p * outer), On(c + q * outer), On(c + q * inner), colour, Vector3.up);
                }
            }
        }

        static Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);

        float Ground(float x, float z) => Mathf.Max(view.Field.Height(x, z), 0f);

        /// <summary>The highest ground under a disc of the radius round the point, so no slope swallows half of it.</summary>
        Vector3 Foot(Vector2 at, float radius)
        {
            float y = Ground(at.x, at.y);
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f;
                y = Mathf.Max(y, Ground(at.x + Mathf.Cos(a) * radius, at.y + Mathf.Sin(a) * radius));
            }
            return new Vector3(at.x, y, at.y);
        }

        /// <summary>
        /// A light disc on a wider dark one, so it reads on grass, sand, snow and crowns alike;
        /// where its top is.
        /// </summary>
        static Vector3 Dot(MeshKit kit, Vector3 foot, float radius, Color colour, Rng rng)
        {
            Disc(kit, foot, radius * 1.45f, 0.03f, Edge, rng);
            Disc(kit, foot, radius, 0.038f, colour, rng);
            return foot + new Vector3(0, 0.038f, 0);
        }

        /// <summary>A flat disc standing <paramref name="height"/> over the foot, sunk a little below it.</summary>
        static void Disc(MeshKit kit, Vector3 foot, float radius, float height, Color colour, Rng rng) =>
            kit.Lathe(foot - new Vector3(0, 0.015f, 0), new[] { 0f, height + 0.015f }, new[] { radius, radius }, new[] { colour },
                14, rng, 0f, 0.02f);

        /// <summary>A flat gold ring for the chosen hero standing at <paramref name="at"/>, laid on the land round it.</summary>
        MeshKit Ring(Vector3 at)
        {
            var kit = new MeshKit();
            const int sides = 32;
            const float inner = 0.22f, outer = 0.29f;
            Color gold = Palette.Gold;
            Vector3 Point(float angle, float radius)
            {
                float x = Mathf.Cos(angle) * radius, z = Mathf.Sin(angle) * radius;
                return new Vector3(x, Ground(at.x + x, at.z + z) - at.y + 0.025f, z);
            }
            for (int s = 0; s < sides; s++)
            {
                float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
                kit.Quad(Point(a0, inner), Point(a0, outer), Point(a1, outer), Point(a1, inner), gold, Vector3.up);
            }
            return kit;
        }

        void OnDestroy()
        {
            Clear();
            foreach (Material copy in late.Values)
                Destroy(copy);
            late.Clear();
            if (overlay != null)
                Destroy(overlay);
        }
    }
}
