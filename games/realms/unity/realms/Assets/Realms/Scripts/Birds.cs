using System.Collections.Generic;
using Realms.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Realms
{
    /// <summary>
    /// Birds over the map (the visual plan's 6; the players allowed simple shader birds on
    /// 2026-11-12). Once in 40-90 s a wedge of five to nine dark birds crosses the view with the
    /// wind, above the trees and below the clouds; two or three light grey gulls circle slowly
    /// over each seen pier and fishing hut near the view. One mesh holds them all, a bird two wing
    /// triangles on four corners, and the Realms/Bird shader places, flaps and leans them, so a
    /// frame changes three values. Moving things draw a child's eye, so they stay rare and dim:
    /// none on a town's or hero's screen, in a battle (the map's objects are hidden then) or at
    /// the closest and the farthest looks, and one flock at a time. The players' crown sends a
    /// flock up from the capital (<see cref="Rise"/>).
    /// </summary>
    public sealed class Birds : MonoBehaviour
    {
        /// <summary>The material (MapView sets it); without it no birds.</summary>
        public static Material Material;

        /// <summary>Flocks come by themselves; the captures turn this off and send their own (<see cref="Fly"/>).</summary>
        public static bool Wild = true;

        /// <summary>False hides every bird (a capture's frames without them).</summary>
        public static bool Shown = true;

        /// <summary>Birds in a flock at most; piers and huts with gulls at once; gulls at one.</summary>
        const int FlockSize = 9, Sites = 14, SiteGulls = 3;

        /// <summary>Seconds from one flock to the next, at least and at most.</summary>
        const float Soonest = 40f, Latest = 90f;

        /// <summary>A flock's speed, world units a second (a cell is 1.7 wide).</summary>
        const float Speed = 1.4f;

        static readonly int FlockId = Shader.PropertyToID("_RealmsBirdFlock");
        static readonly int HeadingId = Shader.PropertyToID("_RealmsBirdHeading");
        static readonly int ShowId = Shader.PropertyToID("_RealmsBirdShow");

        /// <summary>Dark, muted birds; gulls light grey, not white (the visual plan).</summary>
        static readonly Color FlockColour = Palette.Hex("#3A3631"), GullColour = Palette.Hex("#BDBFBB");

        MapView map;
        World world;
        TerrainField field;
        Mesh mesh;
        Vector4[] places, turns;
        Color[] colours;

        /// <summary>The seen piers and huts: x, z and the cell (as a float).</summary>
        readonly List<Vector3> sites = new List<Vector3>();

        /// <summary>The cell whose gulls each site's slot holds, −1 none.</summary>
        readonly int[] slots = new int[Sites];

        float nextSites, nextChoice;
        Vector2 chosenAround = new Vector2(float.MaxValue, float.MaxValue);

        // The flock in the air.
        bool flying, forced;
        Vector2 from, direction;
        float started, length, low, cruise, climb, fadeIn, curve, phase, spread;
        int count;
        float nextFlock, flockShow, gullShow;
        readonly Vector3[] corners = new Vector3[4];

        /// <summary>The birds of <paramref name="map"/>'s world, under <paramref name="parent"/>; null without the material.</summary>
        public static Birds Make(MapView map, Transform parent)
        {
            if (Material == null || map.World == null || map.Field == null)
                return null;
            var birds = new GameObject("Birds").AddComponent<Birds>();
            birds.transform.SetParent(parent, false);
            birds.map = map;
            birds.world = map.World;
            birds.field = map.Field;
            birds.Build();
            // The first flock comes sooner than the rest.
            birds.nextFlock = Time.time + Random.Range(Soonest * 0.5f, Latest * 0.5f);
            return birds;
        }

        void Build()
        {
            int birds = FlockSize + Sites * SiteGulls;
            var positions = new Vector3[birds * 4];
            var own = new Vector4[birds * 4];
            places = new Vector4[birds * 4];
            turns = new Vector4[birds * 4];
            colours = new Color[birds * 4];
            var indices = new int[birds * 6];
            var random = new System.Random(world.Seed);
            for (int b = 0; b < birds; b++)
            {
                bool gull = b >= FlockSize;
                float seed = (float)random.NextDouble();
                var place = Vector4.zero;
                Color colour = new Color(0f, 0f, 0f, 0f);
                if (!gull)
                {
                    // The wedge: the leader, then left and right by turns, each row further back
                    // and out; with an odd count one arm is a bird longer, as in a real flock.
                    int row = (b + 1) / 2;
                    float side = b == 0 ? 0f : b % 2 == 1 ? 1f : -1f;
                    float back = row * 0.3f + (float)random.NextDouble() * 0.06f;
                    float across = side * (row * 0.24f + (float)random.NextDouble() * 0.05f);
                    place = new Vector4(back, across, 0f, 0f);
                    float tint = 0.92f + 0.16f * (float)random.NextDouble();
                    colour = new Color(FlockColour.r * tint, FlockColour.g * tint, FlockColour.b * tint, 1f);
                }
                for (int k = 0; k < 4; k++)
                {
                    int v = b * 4 + k;
                    own[v] = new Vector4(k, gull ? 1f : 0f, seed, gull ? 0f : b);
                    places[v] = place;
                    colours[v] = colour;
                }
                // Each wing a triangle on the breast-tail line.
                indices[b * 6] = b * 4;
                indices[b * 6 + 1] = b * 4 + 1;
                indices[b * 6 + 2] = b * 4 + 2;
                indices[b * 6 + 3] = b * 4;
                indices[b * 6 + 4] = b * 4 + 3;
                indices[b * 6 + 5] = b * 4 + 1;
            }
            for (int s = 0; s < Sites; s++)
                slots[s] = -1;
            mesh = new Mesh { name = "Birds" };
            mesh.SetVertices(positions);
            mesh.SetUVs(0, own);
            mesh.SetUVs(1, places);
            mesh.SetUVs(2, turns);
            mesh.SetColors(colours);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            // The shader moves every corner, so the bounds hold the whole map's sky.
            mesh.bounds = new Bounds(new Vector3((world.MinX + world.MaxX) * 0.5f, 2f, (world.MinZ + world.MaxZ) * 0.5f),
                new Vector3(world.MaxX - world.MinX + 80f, 8f, world.MaxZ - world.MinZ + 80f));
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = Material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        void OnDestroy()
        {
            Shader.SetGlobalVector(FlockId, Vector4.zero);
            Shader.SetGlobalVector(ShowId, Vector4.zero);
            if (mesh != null)
                Destroy(mesh);
        }

        /// <summary>A value that eases from 0 to 1 between near0 and near1 and back to 0 between far0 and far1.</summary>
        static float Window(float d, float near0, float near1, float far0, float far1) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(near0, near1, d)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(far0, far1, d)));

        void Update()
        {
            Camera view = Camera.main;
            MapCamera camera = view != null ? view.GetComponent<MapCamera>() : null;
            if (camera == null || mesh == null)
                return;
            float now = Time.time, distance = camera.Distance;
            bool hidden = !Shown || camera.Holding;
            // The flock between a close look and the farthest; the gulls only close.
            float flockRoom = hidden ? 0f : Window(distance, 8f, 11f, 48f, 62f);
            float gullRoom = hidden ? 0f : Window(distance, 7f, 9f, 24f, 32f);
            gullShow = hidden ? 0f : Mathf.MoveTowards(gullShow, gullRoom, Time.deltaTime * 1.5f);
            if (gullShow > 0f)
                KeepGulls(camera, now);

            float alpha = 0f, up = 1f, height = 0f;
            Vector2 at = Vector2.zero, heading = Vector2.right;
            if (flying)
            {
                float age = now - started, travelled = age * Speed;
                if (travelled >= length)
                    flying = forced = false;
                else
                {
                    // A long, gentle bend along the way.
                    var side = new Vector2(-direction.y, direction.x);
                    at = from + direction * travelled + side * (curve * Mathf.Sin(travelled / 7f + phase));
                    heading = (direction + side * (curve / 7f * Mathf.Cos(travelled / 7f + phase))).normalized;
                    up = climb > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / climb)) : 1f;
                    height = Mathf.Lerp(low, cruise, up);
                    float ends = Mathf.Clamp01(age / fadeIn) * Mathf.Clamp01((length - travelled) / (Speed * 1.5f));
                    float room = forced ? (hidden ? 0f : 1f) : flockRoom;
                    flockShow = hidden ? 0f : Mathf.MoveTowards(flockShow, room, Time.deltaTime * 1.5f);
                    alpha = ends * flockShow;
                }
            }
            Shader.SetGlobalVector(FlockId, new Vector4(at.x, height, at.y, alpha));
            Shader.SetGlobalVector(HeadingId, new Vector4(heading.x, heading.y, count, spread));
            Shader.SetGlobalVector(ShowId, new Vector4(gullShow, up, 0f, 0f));

            if (!flying && Wild && now >= nextFlock)
                nextFlock = now + (flockRoom > 0.9f && Fly() ? Random.Range(Soonest, Latest) : 8f);
        }

        /// <summary>
        /// A flock crosses the view now with the wind, over a seen point near the view's middle;
        /// or over <paramref name="through"/>, <paramref name="lead"/> seconds from now, already
        /// in sight (a capture's). False when no seen land lies near the view's middle.
        /// </summary>
        public bool Fly(Vector2? through = null, float lead = 0f)
        {
            Camera view = Camera.main;
            MapCamera camera = view != null ? view.GetComponent<MapCamera>() : null;
            if (camera == null || !camera.ViewCorners(corners))
                return false;
            Vector2 middle = Vector2.zero;
            foreach (Vector3 c in corners)
                middle += new Vector2(c.x, c.z) * 0.25f;
            float reach = 0f;
            foreach (Vector3 c in corners)
                reach = Mathf.Max(reach, Vector2.Distance(new Vector2(c.x, c.z), middle));
            reach = Mathf.Min(reach, 60f);
            Vector2 over = middle;
            if (through.HasValue)
                over = through.Value;
            else
            {
                bool found = false;
                for (int attempt = 0; attempt < 8 && !found; attempt++)
                {
                    over = middle + Random.insideUnitCircle * reach * 0.35f;
                    int cell = CellAt(over);
                    found = cell >= 0 && map.Visible(cell);
                }
                if (!found)
                    return false;
            }
            direction = Turned(MapView.WindToward, Random.Range(-25f, 25f));
            float before = through.HasValue ? lead * Speed : reach + 3f;
            from = over - direction * before;
            length = before + reach + 3f;
            cruise = Cruise(from, direction, length);
            low = cruise;
            climb = 0f;
            fadeIn = through.HasValue ? 0.1f : 1.5f;
            count = Random.Range(5, FlockSize + 1);
            spread = Random.Range(0.85f, 1.15f);
            curve = Random.Range(0.3f, 0.9f) * (Random.value < 0.5f ? -1f : 1f);
            phase = Random.Range(0f, Mathf.PI * 2f);
            started = Time.time;
            flying = true;
            forced = false;
            flockShow = 1f;
            return true;
        }

        /// <summary>
        /// A whole flock rises from <paramref name="point"/> and flies off with the wind: the
        /// players' crown, from the capital (the visual plan's 12). It shows at any distance but
        /// on the screens.
        /// </summary>
        public void Rise(Vector2 point)
        {
            direction = Turned(MapView.WindToward, Random.Range(-20f, 20f));
            from = point;
            length = 36f;
            cruise = Cruise(from, direction, length);
            low = Mathf.Max(0f, field.Height(point.x, point.y)) + 0.35f;
            climb = 4f;
            fadeIn = 0.4f;
            count = FlockSize;
            spread = 1f;
            curve = 0.4f;
            phase = 0f;
            started = Time.time;
            flying = forced = true;
            flockShow = 1f;
        }

        /// <summary>The seen pier or fishing hut nearest <paramref name="near"/> (a capture's gulls).</summary>
        public bool NearestSite(Vector2 near, out Vector2 site)
        {
            GatherSites();
            site = near;
            float best = float.MaxValue;
            foreach (Vector3 s in sites)
            {
                float d = (new Vector2(s.x, s.y) - near).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    site = new Vector2(s.x, s.y);
                }
            }
            return best < float.MaxValue;
        }

        /// <summary>Above the trees and the peaks under the way, below the drifting clouds (4.5 high).</summary>
        float Cruise(Vector2 start, Vector2 toward, float way)
        {
            float top = 0f;
            for (float s = 0f; s <= way; s += 1f)
            {
                Vector2 p = start + toward * s;
                top = Mathf.Max(top, field.Height(p.x, p.y));
            }
            return Mathf.Clamp(Mathf.Max(Random.Range(2.2f, 2.6f), top + 1.4f), 2.2f, 3.4f);
        }

        static Vector2 Turned(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad, s = Mathf.Sin(a), c = Mathf.Cos(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c).normalized;
        }

        int CellAt(Vector2 p)
        {
            Hex hex = HexMetrics.FromWorld(p.x, p.y);
            if (hex.Col < 0 || hex.Row < 0 || hex.Col >= world.Width || hex.Row >= world.Height)
                return -1;
            return world.IndexOf(hex.Col, hex.Row);
        }

        /// <summary>
        /// The gulls stay over the seen piers and huts nearest the view's middle: the sites are
        /// gathered again every few seconds (the land seen and the huts built change), and chosen
        /// again when the view has moved. A site keeps its gulls' circles wherever it is listed.
        /// </summary>
        void KeepGulls(MapCamera camera, float now)
        {
            if (now >= nextSites)
            {
                nextSites = now + 3f;
                GatherSites();
                chosenAround = new Vector2(float.MaxValue, float.MaxValue);
            }
            if (now < nextChoice || !camera.GroundUnder(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out Vector3 ground))
                return;
            nextChoice = now + 0.5f;
            var middle = new Vector2(ground.x, ground.z);
            if ((middle - chosenAround).sqrMagnitude < 4f)
                return;
            chosenAround = middle;
            sites.Sort((a, b) => (new Vector2(a.x, a.y) - middle).sqrMagnitude.CompareTo((new Vector2(b.x, b.y) - middle).sqrMagnitude));
            bool changed = false;
            for (int s = 0; s < Sites; s++)
            {
                int cell = s < sites.Count ? (int)sites[s].z : -1;
                if (slots[s] == cell)
                    continue;
                slots[s] = cell;
                WriteSite(s, cell >= 0 ? new Vector2(sites[s].x, sites[s].y) : Vector2.zero, cell);
                changed = true;
            }
            if (!changed)
                return;
            mesh.SetUVs(1, places);
            mesh.SetUVs(2, turns);
            mesh.SetColors(colours);
        }

        /// <summary>A pier stands on a town's water side most towards the camera (PropBuilder.Pier); a hut on a fish cell.</summary>
        void GatherSites()
        {
            sites.Clear();
            foreach (City city in world.Cities)
            {
                int cell = city.Cell;
                if (!map.Visible(cell))
                    continue;
                int best = -1;
                float bestScore = float.MinValue;
                for (int d = 0; d < 6; d++)
                {
                    int n = world.Neighbor(cell, d);
                    if (n < 0 || !world.Cells[n].IsWater)
                        continue;
                    float score = -Mathf.Sin(d * Mathf.PI / 3f);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = d;
                    }
                }
                if (best < 0)
                    continue;
                int water = world.Neighbor(cell, best);
                sites.Add(new Vector3(Mathf.Lerp(world.CenterX(cell), world.CenterX(water), 0.5f),
                    Mathf.Lerp(world.CenterZ(cell), world.CenterZ(water), 0.5f), cell));
            }
            foreach (Cell cell in world.Cells)
            {
                if (cell.Building == FieldBuilding.Fishery && map.Visible(cell.Index))
                    sites.Add(new Vector3(world.CenterX(cell.Index), world.CenterZ(cell.Index), cell.Index));
            }
        }

        /// <summary>Two or three gulls of the site of <paramref name="cell"/> into the slot; none for −1.</summary>
        void WriteSite(int slot, Vector2 site, int cell)
        {
            var random = new System.Random(cell * 7919 + 17);
            int gulls = cell < 0 ? 0 : 2 + random.Next(2);
            float ground = cell < 0 ? 0f : Mathf.Max(0f, field.Height(site.x, site.y));
            for (int g = 0; g < SiteGulls; g++)
            {
                int bird = FlockSize + slot * SiteGulls + g;
                // Each its own circle round the site, slow: once in 9-14 s, either way.
                float aside = (float)random.NextDouble() * Mathf.PI * 2f, off = 0.2f * (float)random.NextDouble();
                var place = new Vector4(site.x + Mathf.Cos(aside) * off, site.y + Mathf.Sin(aside) * off,
                    0.45f + 0.35f * (float)random.NextDouble(), ground + 0.95f + 0.35f * (float)random.NextDouble());
                float speed = Mathf.PI * 2f / (9f + 5f * (float)random.NextDouble()) * (random.Next(2) == 0 ? 1f : -1f);
                var turn = new Vector4(speed, (float)random.NextDouble() * Mathf.PI * 2f, 0f, 0f);
                float tint = 0.95f + 0.08f * (float)random.NextDouble();
                var colour = new Color(GullColour.r * tint, GullColour.g * tint, GullColour.b * tint, g < gulls ? 1f : 0f);
                for (int k = 0; k < 4; k++)
                {
                    places[bird * 4 + k] = place;
                    turns[bird * 4 + k] = turn;
                    colours[bird * 4 + k] = colour;
                }
            }
        }
    }
}
