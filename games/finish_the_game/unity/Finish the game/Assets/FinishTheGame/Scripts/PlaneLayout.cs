using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace FinishTheGame
{
    /// <summary>A thing on the plane: its footprint's centre (x, z), its bottom,
    /// size, yaw and colour; a solid one has a collider (and cuts the navmesh).
    /// With a <see cref="model"/> (<see cref="ModelSet"/>), that model, scaled to
    /// the height (or <see cref="byLength"/>, to the length along x); else a box.</summary>
    public struct Prop
    {
        public string name;
        public Vector3 at;   // x, bottom, z
        public Vector3 size;
        public float yaw;
        public Color colour;
        public bool solid;
        public string model;
        public bool byLength;

        public Prop(string name, float x, float z, Vector3 size, Color colour, float yaw = 0f, bool solid = true, float bottom = 0f, string model = null, bool byLength = false)
        {
            this.model = model;
            this.byLength = byLength;
            this.name = name;
            at = new Vector3(x, bottom, z);
            this.size = size;
            this.yaw = yaw;
            this.colour = colour;
            this.solid = solid;
        }

        public Matrix4x4 Place => Matrix4x4.TRS(at, Quaternion.Euler(0, yaw, 0), Vector3.one);
    }

    /// <summary>An ordinary, Euclidean level on a plane, and the story's
    /// route over it, which comes back to places: from the south into A, the
    /// village with the red tower; west through the wood to the cave and back
    /// to A; east to the walled city, north to the farm, back through the city
    /// and home to A; and, round all that, a wide land of woods and rocks, so
    /// wide that one never reaches its end. Placeholder boxes only.</summary>
    public static class PlaneLayout
    {
        /// <summary>The places are this much further apart than they were
        /// drawn (in metres, below), the places themselves as they are: a
        /// story long enough for the crystal's rings.</summary>
        public const float Spread = 2.5f;
        /// <summary>The plane's half side, m; and the half side of its middle,
        /// where the story is (the map, the view from above, the colliders: the
        /// land beyond is only seen).</summary>
        public const float Half = 1500f, Story = 280f * Spread;
        /// <summary>The woods (Forest), over all the land: as planted within
        /// this of a road, m (where one walks and looks closely); beyond, seen
        /// only from afar, their spacing <see cref="FarSpacing"/> times wider.
        /// Their noise's scale, m, and the noise's span sharpened into their
        /// edges; the spacing kept in deep wood, open woodland and meadow (a
        /// lone tree), m; and how far from a road's middle they stand, m.</summary>
        public const float WoodReach = 300f, FarSpacing = 1.7f, WoodScale = 260f, DenseSpacing = 8.5f, OpenSpacing = 30f, LoneSpacing = 60f, TreeRoom = 8f;
        public static readonly Vector2 WoodEdge = new Vector2(0.42f, 0.6f);
        /// <summary>The radius of a thing's foot, m: it stands on the lowest ground within it.</summary>
        public const float Foot = 1.5f;

        public static readonly Vector2 A = new Vector2(0, -120) * Spread, Cave = new Vector2(-110, 95) * Spread,
            City = new Vector2(112, 15) * Spread, Farm = new Vector2(95, 112) * Spread, Lake = new Vector2(10, 30) * Spread;

        public static readonly Color Grass = new Color(0.33f, 0.42f, 0.24f);
        public static readonly Color Road = new Color(0.55f, 0.48f, 0.36f);
        static readonly Color Stone = new Color(0.52f, 0.52f, 0.5f);
        static readonly Color Rock = new Color(0.38f, 0.36f, 0.35f);
        static readonly Color Walls = new Color(0.62f, 0.5f, 0.38f);
        static readonly Color Tower = new Color(0.75f, 0.18f, 0.15f);
        static readonly Color Tree = new Color(0.16f, 0.3f, 0.16f);
        static readonly Color Wall = new Color(0.58f, 0.56f, 0.5f);
        static readonly Color Banner = new Color(0.2f, 0.35f, 0.75f);
        static readonly Color Hay = new Color(0.85f, 0.75f, 0.3f);
        static readonly Color Fence = new Color(0.4f, 0.3f, 0.2f);
        static readonly Color Water = new Color(0.2f, 0.4f, 0.6f);

        /// <summary>A branch of the story: a way off the main road, taken or
        /// passed by (<see cref="skip"/>: the main road's name at its fork); an
        /// optional one comes back to the road, else it shuts the other way out.</summary>
        public struct Branch
        {
            public string name, skip;
            public bool optional;
        }

        /// <summary>The story's branches, in their order along the road.</summary>
        public static readonly Branch[] Branches =
        {
            new Branch { name = "The lake shrine", skip = "On along the road", optional = true },
            new Branch { name = "The old portal", skip = "North to the farm", optional = false },
        };

        /// <summary>The shrine by the lake, and the old portal on the hill east of the city.</summary>
        public static readonly Vector2 Shrine = new Vector2(-120, 80), Portal = new Vector2(450, 115);

        /// <summary>Every way through the story: one route a choice of each
        /// branch (taken or not), its waypoints (a smooth curve goes through
        /// them; on the open road, spread; in and round a place, from its middle
        /// as it is; the dead ends turn round in a small loop, for the camera to
        /// turn with). The first is the main road, no branch taken.</summary>
        public static List<(bool[] taken, Vector2[] waypoints)> Routes()
        {
            Vector2 Road(float x, float z) => new Vector2(x, z) * Spread;
            Vector2 In(Vector2 place, float x, float z) => place + new Vector2(x, z);
            // From A west through the wood to the cave, round before its mouth, back.
            var toCave = new[]
            {
                In(A, 0, -40), A,
                Road(-30, -95), Road(-70, -60), Road(-95, -15), Road(-105, 35), In(Cave, 6, -27),
                In(Cave, 22, -4), In(Cave, 0, 18), In(Cave, -22, -1), In(Cave, -8, -32),
                Road(-112, 38),
            };
            // Off to the shrine by the lake, round it, back to the road.
            var shrine = new[]
            {
                new Vector2(-215, 105), new Vector2(-160, 108), In(Shrine, 0, 22), In(Shrine, 22, 0),
                In(Shrine, 0, -22), new Vector2(-170, 38), new Vector2(-225, 5),
            };
            // Back to A, east to the city, through its gate and round its square, out north.
            var toCity = new[]
            {
                Road(-100, -12), Road(-74, -56), Road(-32, -90), In(A, 0, 8),
                // (The gates are the walls' gaps at x −4 to 4 from its middle.)
                Road(32, -100), Road(72, -74), In(City, -10, -55), In(City, 0, -29),
                In(City, 12, -3), In(City, 8, 19), In(City, 0, 35),
            };
            // North to the farm and round it.
            var farm = new[]
            {
                In(Farm, 7, -30), In(Farm, 22, -9), In(Farm, 0, 21), In(Farm, -22, -3), In(Farm, -4, -36),
            };
            // East up to the old portal, through its arch, round and back.
            var portal = new[]
            {
                new Vector2(320, 105), new Vector2(385, 122), In(Portal, -28, 0), In(Portal, 0, 0), In(Portal, 26, 0),
                In(Portal, 34, -24), new Vector2(395, 70), new Vector2(325, 70),
            };
            // Back through the city, by its west side, and home.
            var home = new[]
            {
                In(City, -2, 35), In(City, -12, 10), In(City, -12, -10), In(City, -2, -31), In(City, -8, -57), Road(66, -80),
                Road(28, -104), In(A, 0, -4), In(A, 0, -30),
            };
            var routes = new List<(bool[], Vector2[])>();
            foreach (bool toShrine in new[] { false, true })
                foreach (bool toPortal in new[] { false, true })
                {
                    var points = new List<Vector2>(toCave);
                    if (toShrine) points.AddRange(shrine);
                    points.AddRange(toCity);
                    points.AddRange(toPortal ? portal : farm);
                    points.AddRange(home);
                    routes.Add((new[] { toShrine, toPortal }, points.ToArray()));
                }
            return routes;
        }

        /// <summary>The main road's waypoints.</summary>
        public static Vector2[] Route() => Routes()[0].waypoints;

        public static List<Prop> Make(StoryPath route, IEnumerable<Model> models = null, IList<StoryPath> others = null)
        {
            var routes = new List<StoryPath> { route };
            if (others != null) routes.AddRange(others);
            // The decor: the studio's older models, by kind.
            var decor = new Dictionary<string, List<Model>>();
            if (models != null)
                foreach (var m in models)
                    if (m != null && !string.IsNullOrEmpty(m.kind))
                    {
                        if (!decor.TryGetValue(m.kind, out var list)) decor[m.kind] = list = new List<Model>();
                        list.Add(m);
                    }
            Land.Build(routes, Half, (A, 40f), (City, 42f), (Farm, 70f), (Cave + new Vector2(-4, 30), 45f), (Lake, 110f), (Shrine, 40f), (Portal, 45f));
            var props = new List<Prop>();
            var random = new System.Random(11);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            // Clear of every way's road.
            bool Clear(float x, float z, float room)
            {
                foreach (var r in routes) if (Mathf.Abs(r.Project(new Vector3(x, 0, z)).y) <= room) return false;
                return true;
            }

            // The studio's older models by name (art/decor.json): a heights table
            // and what each is; a model not converted is left out.
            var have = new HashSet<string>();
            foreach (var list in decor.Values) foreach (var m in list) have.Add(m.name);
            string[] Barrels = { "old14", "old15", "old53" }, Chests = { "old16", "old17" }, Crates = { "old80" };
            string[] Oaks = { "oak", "old98", "old99", "old95" }, Pines = { "pine", "old94", "old96", "old97" }, Autumn = { "old56", "old58", "old57" };
            string[] Dead = { "old32", "old72", "old101", "old107", "old108" }, Stumps = { "old34", "old67", "old102", "old103", "old104", "old105", "old106" };
            string[] Bushes = { "old73", "old74" }, Grasses = { "old60", "old71" }, Rocks = { "old35", "old75", "old76", "boulder" };
            string[] Chapels = { "old86", "old87", "old83" }, RuinWalls = { "old62", "old63", "old64", "old81", "old85", "old90", "old93" };
            string One(string[] names)
            {
                var ok = System.Array.FindAll(names, n => have.Contains(n) || n == "oak" || n == "pine" || n == "boulder");
                return ok.Length == 0 ? null : ok[random.Next(ok.Length)];
            }
            float HeightOf(string name, float fallback)
            {
                foreach (var list in decor.Values) foreach (var m in list) if (m.name == name) return m.height;
                return fallback;
            }
            // A thing by name, at its own height (times a little play), turned.
            void Thing(string name, float x, float z, float yaw, float height = 0, bool solid = true, float bottom = 0, float play = 0.12f)
            {
                if (name == null) return;
                float h = (height > 0 ? height : HeightOf(name, 2f)) * Range(1 - play, 1 + play);
                props.Add(new Prop(name, x, z, new Vector3(1, h, 1), Rock, yaw: yaw, solid: solid, bottom: bottom, model: name));
            }
            void Tree(string[] kinds, float x, float z, float height = 0, float scale = 1)
            {
                var name = One(kinds);
                float h = height > 0 ? height : name == "oak" ? Range(10, 14) : name == "pine" ? Range(13, 18) : HeightOf(name, 10f);
                Thing(name, x, z, Range(0, 360), h * scale);
            }

            // A: the village. Its well on the grass (no paved square), the
            // watchtower, two lanes of houses with gardens fenced behind them,
            // and the life round them.
            props.Add(new Prop("A Tower", A.x - 9, A.y + 5, new Vector3(5, 15, 5), Tower, yaw: 20, model: "watchtower"));
            props.Add(new Prop("A Well", A.x + 4, A.y - 2, new Vector3(2.5f, 3.2f, 2.5f), Stone, model: "well"));
            Thing("old45", A.x + 7, A.y - 5, 160);
            Thing("old45", A.x + 1, A.y + 5, 20);
            foreach (var (x, z) in new[] { (-6f, -8f), (7f, 7f), (-7f, 9f), (8f, -9f) }) Thing("old79", A.x + x, A.y + z, 0, 3.4f, play: 0.03f);
            // The houses round the square, not in rows: each at its own distance,
            // a little off its neighbours, turned to face the square or the road
            // (whichever is nearer) give or take.
            float angleAt = 0;
            for (int k = 0; k < 11; k++)
            {
                angleAt += Range(24f, 40f) * Mathf.Deg2Rad;
                float r = Range(17f, 30f);
                float hx = A.x + Mathf.Cos(angleAt) * r, hz = A.y + Mathf.Sin(angleAt) * r;
                if (Mathf.Abs(route.Project(new Vector3(hx, 0, hz), PointS(A), 60f).y) < 8f) continue;
                var toSquare = new Vector2(A.x - hx, A.y - hz);
                float yaw = Mathf.Atan2(toSquare.x, toSquare.y) * Mathf.Rad2Deg + Range(-25f, 25f);
                House(hx, hz, yaw);
                var back = -toSquare.normalized;
                float gx = hx + back.x * 9f, gz = hz + back.y * 9f;
                if (random.NextDouble() < 0.6)
                {
                    // A garden behind it, fenced on its far side.
                    float fenceYaw = yaw + 90f;
                    for (int e = -1; e <= 1; e++)
                    {
                        var along = Quaternion.Euler(0, fenceYaw, 0) * Vector3.forward;
                        props.Add(new Prop("Fence", gx + back.x * 3f + along.x * e * 4f, gz + back.y * 3f + along.z * e * 4f, new Vector3(4, 1.2f, 0.4f), Fence, yaw: fenceYaw + Range(-6, 6), model: "fence", byLength: true));
                    }
                }
                if (random.NextDouble() < 0.5) Thing(One(Barrels), hx - back.x * 5f + Range(-2, 2), hz - back.y * 5f + Range(-2, 2), Range(0, 360));
                if (random.NextDouble() < 0.35) Thing("old113", gx + Range(-3, 3), gz + Range(-3, 3), yaw + Range(-20, 20));
                if (random.NextDouble() < 0.3) Thing(One(Crates), hx - back.x * 5f + Range(-3, 3), hz - back.y * 5f + Range(-3, 3), Range(0, 360));
                if (random.NextDouble() < 0.45) Tree(Autumn, gx + Range(-4, 4), gz + Range(-4, 4), Range(7, 10));
            }
            Thing("old66", A.x + 11, A.y + 31, 40);
            Thing(One(Chests), A.x - 11, A.y - 32, 0);
            // The way-sign where the road leaves the village, both ways.
            foreach (float s in new[] { 60f, route.Length - 60f })
            {
                var at = route.At(s, 3.8f);
                Thing("old112", at.x, at.z, Mathf.Atan2(route.Tangent(s).x, route.Tangent(s).z) * Mathf.Rad2Deg);
            }

            // The cave: a great rock with its mouth facing the road's loop, boulders
            // round it, and the wood about it dead and dark.
            props.Add(new Prop("Cave", Cave.x - 4, Cave.y + 34, new Vector3(34, 20, 26), Rock, yaw: 180, model: "cave"));
            for (int i = 0; i < 9; i++)
            {
                float angle = Mathf.Lerp(10f, 175f, i / 8f) * Mathf.Deg2Rad;
                Thing(One(Rocks), Cave.x + Mathf.Cos(angle) * Range(36, 46), Cave.y + Mathf.Sin(angle) * Range(32, 42), Range(0, 360), Range(4, 9));
            }

            // The city: walls, a gatehouse south and north, houses round the ring
            // street, the keep in the middle (the road goes round it).
            const float side = 56f;
            for (int i = 0; i < 4; i++)
            {
                float yaw = i * 90f;
                var normal = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                var along = Quaternion.Euler(0, yaw, 0) * Vector3.right;
                bool gated = i == 0 || i == 2;
                for (int k = -3; k <= 3; k++)
                {
                    if (gated && Mathf.Abs(k) <= 1) continue;
                    var at = new Vector3(City.x, 0, City.y) + normal * side / 2 + along * (k * 8f);
                    props.Add(new Prop("City Wall", at.x, at.z, new Vector3(8.4f, 6, 2), Wall, yaw: yaw, model: "wall", byLength: true));
                }
                if (gated)
                {
                    var at = new Vector3(City.x, 0, City.y) + normal * side / 2;
                    props.Add(new Prop("City Gate", at.x, at.z, new Vector3(24, 12, 8), Wall, yaw: yaw, model: "gate", byLength: true));
                }
            }
            props.Add(new Prop("City Keep", City.x, City.y, new Vector3(9, 20, 9), Banner, model: "keep"));
            foreach (var (x, z, yaw) in new[] { (-22f, -16f, 90f), (-22f, 0f, 90f), (-22f, 16f, 90f), (21f, -16f, -90f), (21f, 2f, -90f), (21f, 18f, -90f), (-8f, 22f, 180f), (8f, -22f, 0f) })
                props.Add(new Prop("Town House", City.x + x, City.y + z, new Vector3(6, 10, 6), Walls, yaw: yaw, model: "townhouse"));
            foreach (var (x, z) in new[] { (6f, 6f), (-6f, 7f), (7f, -6f), (-5f, -7f) }) Thing(One(Barrels.Concat(Crates).ToArray()), City.x + x, City.y + z, Range(0, 360));
            Thing("old66", City.x - 7, City.y - 8, 70);
            foreach (var (x, z) in new[] { (-14f, -24f), (14f, -24f), (-14f, 24f), (14f, 24f) }) Thing("old79", City.x + x, City.y + z, 0, 3.4f, play: 0.03f);

            // The farm: the barn, fenced paddocks with haystacks, a cart, logs, a woodshed.
            props.Add(new Prop("Barn", Farm.x - 30, Farm.y + 10, new Vector3(12, 10, 16), Walls, yaw: 110, model: "barn"));
            Thing("old66", Farm.x - 20, Farm.y + 2, 200);
            Thing("old68", Farm.x - 22, Farm.y + 18, 30, 0.9f);
            Thing("old68", Farm.x - 23, Farm.y + 19.5f, 35, 0.9f);
            Thing("old113", Farm.x - 38, Farm.y + 2, 110);
            for (int f = 0; f < 4; f++)
            {
                float fx = Farm.x + 30 + (f % 2) * 22, fz = Farm.y - 12 + (f / 2) * 24;
                for (int e = 0; e < 4; e++)
                {
                    float yaw = e * 90f;
                    var normal = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                    for (int k = -1; k <= 1; k++)
                    {
                        var at = new Vector3(fx, 0, fz) + normal * 9f + Quaternion.Euler(0, yaw, 0) * Vector3.right * (k * 6f);
                        props.Add(new Prop("Fence", at.x, at.z, new Vector3(6, 1.3f, 0.4f), Fence, yaw: yaw, model: "fence", byLength: true));
                    }
                }
                for (int k = 0; k < 3; k++)
                    props.Add(new Prop("Haystack", fx + Range(-5, 5), fz + Range(-5, 5), new Vector3(3, 3, 3), Hay, yaw: Range(0, 360), model: "haystack"));
            }

            // The lake, willows on its shore, an old chapel in ruins by it.
            props.Add(new Prop("Lake", Lake.x, Lake.y, new Vector3(60, 0.05f, 40) * Spread, Water, yaw: 20, solid: false));
            for (int i = 0; i < 14; i++)
            {
                float a = Range(0, 2 * Mathf.PI);
                float x = Lake.x + Mathf.Cos(a) * 82f, z = Lake.y + Mathf.Sin(a) * 58f;
                if (Clear(x, z, 8f)) Thing("old100", x, z, Range(0, 360), Range(8, 11));
            }
            // The shrine: an old chapel in ruins by the lake's west shore.
            var chapel = Shrine + new Vector2(-10, 4);
            Thing(One(Chapels), chapel.x, chapel.y, 200, 9);
            for (int i = 0; i < 6; i++) Thing(One(RuinWalls), chapel.x + Range(-14, 14), chapel.y + Range(-12, 12), Range(0, 360));
            Thing("old88", chapel.x - 3, chapel.y - 9, 20, 2);
            Thing("old65", chapel.x + 12, chapel.y - 6, 30, 6);

            // Along the road: bushes and grasses at its verges, travellers' camps.
            for (float s = 10; s < route.Length; s += Range(5, 11))
            {
                var at = route.At(s, (random.Next(2) == 0 ? -1 : 1) * Range(4.2f, 7.5f));
                if (Near(at.x, at.z) || !Clear(at.x, at.z, 3.8f)) continue;
                Thing(random.NextDouble() < 0.55 ? One(Grasses) : One(Bushes), at.x, at.z, Range(0, 360), solid: false);
            }
            foreach (float s in new[] { route.Length * 0.12f, route.Length * 0.47f, route.Length * 0.82f })
            {
                var fire = route.At(s, 10f);
                Thing("old51", fire.x, fire.z, Range(0, 360), 1f, solid: false);
                var tent = route.At(s + 3f, 15f);
                Thing("old52", tent.x, tent.z, Mathf.Atan2(route.Tangent(s).x, route.Tangent(s).z) * Mathf.Rad2Deg + 90, 2.6f);
                var log = route.At(s - 2f, 12f);
                Thing("old68", log.x, log.z, Range(0, 360), 0.8f);
                Thing(One(Barrels), tent.x + 2.5f, tent.z, Range(0, 360));
            }

            // The old portal on its hill: the runed arch, the road through it, old stones round it.
            props.Add(new Prop("Portal", Portal.x, Portal.y, new Vector3(6, 7, 4), Rock, yaw: 90, model: "portal"));
            for (int i = 0; i < 7; i++)
            {
                float angle = i * Mathf.PI * 2f / 7f + 0.3f;
                float x = Portal.x + Mathf.Cos(angle) * Range(16, 24), z = Portal.y + Mathf.Sin(angle) * Range(16, 24);
                if (Clear(x, z, 5f)) Thing(random.NextDouble() < 0.5 ? One(RuinWalls) : One(Rocks), x, z, Range(0, 360), Range(2, 4));
            }

            // Lone ruins and rocks, so the land is never empty.
            for (int i = 0; i < 30; i++)
            {
                float x = Range(-Half, Half), z = Range(-Half, Half);
                if (Near(x, z) || !Clear(x, z, 40)) continue;
                Thing(random.NextDouble() < 0.4 ? One(Chapels) : One(RuinWalls), x, z, Range(0, 360));
            }
            for (int i = 0; i < 120; i++)
            {
                float x = Range(-Half, Half), z = Range(-Half, Half);
                if (!Clear(x, z, 12f) || Near(x, z)) continue;
                Thing(One(Rocks), x, z, Range(0, 360), Range(2, 7));
            }
            // The farmyard round the old cart (the demo quest's: Hilda searches
            // the grass before it), on its far side from the road: hay, barrels
            // and crates by it, a well, a fenced pen with the cows and sheep, a
            // dog, an oak and bushes behind: a lived-in place.
            {
                // (Its own random numbers: the rest of the land is laid as before.)
                var kept = random;
                random = new System.Random(2118);
                var cart = new Vector2(Farm.x - 20, Farm.y + 2);
                // From the road where it comes nearest the farm (where Hilda
                // stands, between it and the cart: SpiritQuest), all behind her.
                Vector3 near = route.Position(0);
                for (float ds = 0; ds <= route.Length; ds += 2f)
                {
                    var on = route.Position(ds);
                    if (Vector2.Distance(new Vector2(on.x, on.z), Farm) < Vector2.Distance(new Vector2(near.x, near.z), Farm)) near = on;
                }
                var away = (cart - new Vector2(near.x, near.z)).normalized;
                if (away == Vector2.zero) away = Vector2.up;
                var aside = new Vector2(away.y, -away.x);
                Vector2 At(float a, float b) => cart + away * a + aside * b;
                float Yaw(Vector2 v) => Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg;
                var hay = At(3.2f, 4.2f);
                props.Add(new Prop("Haystack", hay.x, hay.y, new Vector3(3, 3, 3), Hay, yaw: Range(0, 360), model: "haystack"));
                foreach (var (a, b) in new[] { (1.2f, -3f), (2f, -3.8f), (0.6f, -4.2f) })
                {
                    var at = At(a, b);
                    Thing(One(Barrels), at.x, at.y, Range(0, 360));
                }
                foreach (var (a, b) in new[] { (3f, -5.5f), (3.3f, -6.6f) })
                {
                    var at = At(a, b);
                    Thing(One(Crates), at.x, at.y, Yaw(away) + Range(-15, 15));
                }
                var well = At(2.5f, 9f);
                props.Add(new Prop("Farm Well", well.x, well.y, new Vector3(2.5f, 3.2f, 2.5f), Stone, model: "well"));
                // The pen: fences round a square behind, its middle 12 m off.
                var pen = At(14f, 0f);
                for (int e = 0; e < 4; e++)
                {
                    float yaw = Yaw(away) + e * 90f;
                    var normal = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                    var along = Quaternion.Euler(0, yaw, 0) * Vector3.right;
                    for (int k = -1; k <= 1; k++)
                    {
                        // A gap in the side toward the cart.
                        if (e == 2 && k == 0) continue;
                        var at = new Vector3(pen.x, 0, pen.y) + normal * 6f + along * (k * 4f);
                        props.Add(new Prop("Fence", at.x, at.z, new Vector3(4, 1.3f, 0.4f), Fence, yaw: yaw, model: "fence", byLength: true));
                    }
                }
                foreach (var (a, b, kind, h) in new[] { (12f, -3f, "animal_cow", 1.5f), (15.5f, 2f, "animal_cow", 1.5f), (17f, -2.5f, "animal_cow", 1.45f),
                                                         (13f, 3.5f, "animal_sheep", 1f), (14.5f, 4f, "animal_sheep", 0.95f), (2.5f, 6.5f, "animal_dog", 0.6f) })
                {
                    var at = At(a, b);
                    Thing(kind, at.x, at.y, Range(0, 360), h, play: 0.05f);
                }
                foreach (var (a, b) in new[] { (23f, -8f), (21f, 9f) })
                {
                    var at = At(a, b);
                    Tree(Oaks, at.x, at.y);
                }
                foreach (var (a, b) in new[] { (5.5f, -8.5f), (6f, 11.5f), (22f, 2f) })
                {
                    var at = At(a, b);
                    Thing(One(Bushes), at.x, at.y, Range(0, 360));
                }
                random = kept;
            }
            // The woods, planted last, clear of all the rest (Forest).
            Woods();

            // Everything stands on the ground where it is: on a slope, on its
            // lowest under its foot, so that none stands out over the ground.
            for (int i = 0; i < props.Count; i++)
            {
                var p = props[i];
                float ground = Land.Height(p.at.x, p.at.z);
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    ground = Mathf.Min(ground, Land.Height(p.at.x + Foot * Mathf.Cos(a), p.at.z + Foot * Mathf.Sin(a)));
                }
                p.at.y += ground;
                props[i] = p;
            }
            return props;

            // The woods over all the land (sparser far from the roads):
            // great masses with soft edges, open woodland round them, meadows
            // between; dark woods round the cave, fewer trees by the fields and
            // the towns. Their kinds by slow noise of their own, a few mixed in;
            // bushes at the woods' edges, stumps and fallen wood in the deep.
            void Woods()
            {
                var (cells, away) = Forest.Corridor(routes, Half, 20f, float.MaxValue);
                var taken = new List<(Vector2, float)>();
                foreach (var p in props) taken.Add((new Vector2(p.at.x, p.at.z), Mathf.Max(3.5f, Mathf.Max(p.size.x, p.size.z) / 2f + 3f)));
                float Fade(Vector2 p, Vector2 place, float from, float to) => 1f - Mathf.InverseLerp(from, to, Vector2.Distance(p, place));
                (float, float) Spacing(Vector2 p)
                {
                    if (Near(p.x, p.y)) return (0, 0);
                    if (away(p) < 30f && !Clear(p.x, p.y, TreeRoom)) return (0, 0);
                    float bias = 0.22f * Fade(p, Cave, 60, 220) - 0.3f * Fade(p, Farm, 60, 170) - 0.3f * Fade(p, City, 50, 150) - 0.2f * Fade(p, A, 35, 110);
                    float wood = Forest.Woods(p.x, p.y, WoodScale, WoodEdge, bias);
                    float far = Mathf.Lerp(1f, FarSpacing, Mathf.InverseLerp(WoodReach, WoodReach + 200f, away(p)));
                    return (far * (wood < 0.03f ? LoneSpacing : Mathf.Lerp(OpenSpacing, DenseSpacing, Mathf.Pow(wood, 0.7f))), wood);
                }
                foreach (var tree in Forest.Plant(cells, 20f, 14, Spacing, taken, random))
                {
                    float x = tree.at.x, z = tree.at.y;
                    float roll = (float)random.NextDouble();
                    string[] kinds;
                    if (Vector2.Distance(tree.at, Cave) < 180f) kinds = roll < 0.4f ? Dead : Pines;
                    else
                    {
                        float k = Mathf.PerlinNoise(x / 240f + 77f, z / 240f + 33f);
                        kinds = k < 0.4f ? Pines : k > 0.6f ? Autumn : Oaks;
                        if (roll < 0.12f) kinds = roll < 0.04f ? Pines : roll < 0.08f ? Oaks : Autumn;
                    }
                    if (tree.wood > 0.7f && roll > 0.95f) Thing(One(roll > 0.975f ? Dead : Stumps), x, z, Range(0, 360));
                    else Tree(kinds, x, z, scale: Mathf.Lerp(0.8f, 1.1f, tree.wood));
                    // Under the trees at the woods' edge: bushes, a rock now and then.
                    float edge = 4f * tree.wood * (1f - tree.wood);
                    if (random.NextDouble() < 0.6f * edge)
                    {
                        float a = Range(0, 2 * Mathf.PI), r = Range(2.5f, 4.5f);
                        float bx = x + Mathf.Cos(a) * r, bz = z + Mathf.Sin(a) * r;
                        if (Clear(bx, bz, 4f)) Thing(One(Bushes), bx, bz, Range(0, 360), solid: false);
                    }
                    else if (random.NextDouble() < 0.04f)
                    {
                        float a = Range(0, 2 * Mathf.PI);
                        float bx = x + Mathf.Cos(a) * 4f, bz = z + Mathf.Sin(a) * 4f;
                        if (Clear(bx, bz, 4f)) Thing(One(Rocks), bx, bz, Range(0, 360), Range(1.5f, 3.5f));
                    }
                }
            }

            // The route's s nearest a place.
            float PointS(Vector2 place) => route.Project(new Vector3(place.x, 0, place.y)).x;

            void House(float x, float z, float yaw)
            {
                props.Add(new Prop("House", x, z, new Vector3(7, 7.5f, 8), Walls, yaw: yaw, model: random.Next(2) == 0 ? "house" : "house2"));
            }

            bool Near(float x, float z)
            {
                var p = new Vector2(x, z);
                return Vector2.Distance(p, A) < 30 || Vector2.Distance(p, City) < 40 || Vector2.Distance(p, Farm) < 40
                    || Vector2.Distance(p, Shrine) < 25 || Vector2.Distance(p, Portal) < 25
                    || Vector2.Distance(p, Cave) < 34 || Vector2.Distance(p, Lake) < 34 * Spread;
            }
        }
    }
}
