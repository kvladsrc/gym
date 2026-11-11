using System;
using System.Collections.Generic;

namespace Realms.Core
{
    public sealed class WorldSettings
    {
        public int Width = 70;
        public int Height = 42;
        public int Countries = 6;

        /// <summary>Share of the cells that are land.</summary>
        public float LandShare = 0.4f;

        /// <summary>How far a capital's land reaches, in movement cost (a meadow step costs 1).</summary>
        public float MinReach = 7f, MaxReach = 9.5f;

        /// <summary>How many springs to look for, and how far apart they lie.</summary>
        public int Rivers = 10;
        public float RiverSpacing = 7f;
    }

    /// <summary>
    /// Makes a world from a seed: one large continent with a few islands, biomes, rivers, six
    /// kingdoms with capitals, towns and coastal waters, wild land with monster groups, lairs and
    /// places to visit, resources and the roads between the cities.
    /// </summary>
    public static partial class WorldGen
    {
        public static World Generate(int seed, WorldSettings settings = null)
        {
            settings ??= new WorldSettings();
            var world = new World(settings.Width, settings.Height, seed);
            var rng = new Rng(seed);
            int n = world.Cells.Length;
            var elevation = new float[n];
            var moisture = new float[n];
            var relief = new float[n];
            float seaLevel = Elevate(world, rng, settings, elevation, moisture, relief);
            int[] coastDistance = Classify(world, elevation, moisture, relief, seaLevel);
            Rivers(world, settings, moisture);
            int[] owners = Kingdoms(world, rng, settings, coastDistance);
            Monsters(world, rng);
            PlaceResources(world, rng);
            Places(world, rng);
            CapitalQuarters(world);
            Roads(world, owners);
            // The first quarters' streets come after the roads between the capitals, which so keep
            // their way and do not bend through a quarter.
            foreach (City city in world.Cities)
            {
                foreach (int quarter in city.Districts)
                    Towns.Join(world, city, quarter);
            }
            Flags.Assign(world);
            return world;
        }

        /// <summary>The way a capital's first quarter is chosen: behind it first, then the sides, the front last.</summary>
        static readonly int[] QuarterOrder = { 1, 2, 0, 3, 5, 4 };

        /// <summary>
        /// Each capital starts with one quarter on an empty neighbouring cell of its own, as its
        /// five houses fill its centre.
        /// </summary>
        static void CapitalQuarters(World world)
        {
            foreach (Country country in world.Countries)
            {
                City capital = world.Cities[country.Capital];
                foreach (int d in QuarterOrder)
                {
                    int j = world.Neighbor(capital.Cell, d);
                    if (j < 0)
                        continue;
                    Cell c = world.Cells[j];
                    if (c.Owner != country.Index || !c.IsLand || c.Biome == Biome.Mountains || c.City >= 0 || c.District >= 0
                        || c.Resource != Resource.None || c.Place != Place.None || c.Monster != Monster.None)
                        continue;
                    c.District = capital.Index;
                    capital.Districts.Add(j);
                    break;
                }
            }
        }

        static float Smooth(float edge0, float edge1, float x)
        {
            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Fills elevation and moisture; returns the sea level that gives the land share.</summary>
        static float Elevate(World world, Rng rng, WorldSettings settings, float[] elevation, float[] moisture,
            float[] relief)
        {
            var shape = new Noise2(rng.NextInt());
            var detail = new Noise2(rng.NextInt());
            var warp = new Noise2(rng.NextInt());
            var wet = new Noise2(rng.NextInt());
            var ridges = new Noise2(rng.NextInt());
            float minX = world.MinX, maxX = world.MaxX, minZ = world.MinZ, maxZ = world.MaxZ;
            int n = world.Cells.Length;
            for (int i = 0; i < n; i++)
            {
                float x = world.CenterX(i), z = world.CenterZ(i);
                float wx = x + 10f * warp.Fbm(x * 0.035f, z * 0.035f, 3);
                float wz = z + 10f * warp.Fbm(x * 0.035f + 41.3f, z * 0.035f - 17.9f, 3);
                float broad = shape.Fbm(wx * 0.022f, wz * 0.022f, 3);
                float fine = detail.Fbm(wx * 0.06f, wz * 0.06f, 4);
                // The land sinks towards the rim over the outer 18 units, along the warped
                // coordinates so the coast bends; only the last few units are sea for sure.
                float edge = Math.Min(Math.Min(x - minX, maxX - x), Math.Min(z - minZ, maxZ - z));
                float warpedEdge = Math.Min(Math.Min(wx - minX, maxX - wx), Math.Min(wz - minZ, maxZ - wz));
                float rise = Smooth(0f, 18f, warpedEdge);
                float open = Smooth(1.5f, 5f, edge);
                float e = 0.8f * broad + 0.3f * fine + 1.0f * (rise - 0.5f);
                elevation[i] = e * open - (1f - open) * 1.5f;
                moisture[i] = wet.Fbm(x * 0.07f, z * 0.07f, 4);
                // Ranges: high along the zero lines of a noise, so mountains run in chains.
                float ridge = 1f - 2f * MathF.Abs(ridges.Fbm(wx * 0.045f, wz * 0.045f, 3));
                relief[i] = e + 0.45f * ridge * ridge;
            }
            var sorted = (float[])elevation.Clone();
            Array.Sort(sorted);
            int land = Math.Clamp((int)MathF.Round(n * settings.LandShare), 1, n);
            return sorted[n - land];
        }

        static float Quantile(List<float> sorted, float q) =>
            sorted.Count == 0 ? 0f : sorted[Math.Clamp((int)(q * (sorted.Count - 1)), 0, sorted.Count - 1)];

        /// <summary>Breadth-first steps from the nearest source cell (0 at sources).</summary>
        public static int[] Distances(World world, Func<int, bool> source)
        {
            int n = world.Cells.Length;
            var distance = new int[n];
            var queue = new Queue<int>();
            for (int i = 0; i < n; i++)
            {
                if (source(i))
                {
                    distance[i] = 0;
                    queue.Enqueue(i);
                }
                else
                    distance[i] = int.MaxValue;
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                for (int d = 0; d < 6; d++)
                {
                    int j = world.Neighbor(i, d);
                    if (j >= 0 && distance[j] == int.MaxValue)
                    {
                        distance[j] = distance[i] + 1;
                        queue.Enqueue(j);
                    }
                }
            }
            return distance;
        }

        /// <summary>Sets the biomes; returns each land cell's distance to water.</summary>
        static int[] Classify(World world, float[] elevation, float[] moisture, float[] relief, float seaLevel)
        {
            Cell[] cells = world.Cells;
            int n = cells.Length;
            var land = new bool[n];
            for (int i = 0; i < n; i++)
            {
                land[i] = elevation[i] >= seaLevel;
                cells[i].Elevation = elevation[i] - seaLevel;
            }
            // A lone land cell reads as noise: sink it.
            for (int i = 0; i < n; i++)
            {
                if (!land[i])
                    continue;
                bool alone = true;
                for (int d = 0; d < 6 && alone; d++)
                {
                    int j = world.Neighbor(i, d);
                    alone = j < 0 || !land[j];
                }
                if (alone)
                    land[i] = false;
            }

            int[] toLand = Distances(world, i => land[i]);
            int[] toWater = Distances(world, i => !land[i]);

            // Open sea: water connected to the map's rim.
            var openSea = new bool[n];
            var queue = new Queue<int>();
            for (int i = 0; i < n; i++)
            {
                Cell c = cells[i];
                bool rim = c.Col == 0 || c.Row == 0 || c.Col == world.Width - 1 || c.Row == world.Height - 1;
                if (rim && !land[i])
                {
                    openSea[i] = true;
                    queue.Enqueue(i);
                }
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                for (int d = 0; d < 6; d++)
                {
                    int j = world.Neighbor(i, d);
                    if (j >= 0 && !land[j] && !openSea[j])
                    {
                        openSea[j] = true;
                        queue.Enqueue(j);
                    }
                }
            }

            var landElevation = new List<float>();
            var landRelief = new List<float>();
            var landMoisture = new List<float>();
            for (int i = 0; i < n; i++)
            {
                if (land[i])
                {
                    landElevation.Add(elevation[i]);
                    landRelief.Add(relief[i]);
                    landMoisture.Add(moisture[i]);
                }
            }
            landElevation.Sort();
            landRelief.Sort();
            landMoisture.Sort();
            float beachLevel = Quantile(landElevation, 0.18f);
            float hillLevel = Quantile(landRelief, 0.80f);
            float mountainLevel = Quantile(landRelief, 0.925f);
            float plainsLevel = Quantile(landMoisture, 0.26f);
            float forestLevel = Quantile(landMoisture, 0.66f);

            for (int i = 0; i < n; i++)
            {
                Cell c = cells[i];
                if (!land[i])
                {
                    c.Lake = !openSea[i];
                    c.Biome = toLand[i] <= 1 ? Biome.Shallows : Biome.Ocean;
                    continue;
                }
                bool seaShore = false;
                for (int d = 0; d < 6 && !seaShore; d++)
                {
                    int j = world.Neighbor(i, d);
                    seaShore = j >= 0 && openSea[j];
                }
                float e = elevation[i], m = moisture[i];
                // Coasts stay low: a mountain needs a cell of land between it and the sea.
                if (relief[i] >= mountainLevel && toWater[i] >= 2)
                    c.Biome = Biome.Mountains;
                else if (relief[i] >= hillLevel)
                    c.Biome = Biome.Hills;
                else if (seaShore && e < beachLevel)
                    c.Biome = Biome.Beach;
                else if (m >= forestLevel)
                    c.Biome = Biome.Forest;
                else if (m <= plainsLevel)
                    c.Biome = Biome.Plains;
                else
                    c.Biome = Biome.Meadow;
            }
            return toWater;
        }

        internal static float StepCost(Biome biome)
        {
            switch (biome)
            {
                case Biome.Meadow:
                case Biome.Plains: return 1f;
                case Biome.Beach: return 1.2f;
                case Biome.Forest: return 1.5f;
                case Biome.Hills: return 2f;
                case Biome.Mountains: return 4f;
                default: return float.PositiveInfinity;
            }
        }

        static int MinPairDistance(World world, int[] picks)
        {
            int best = int.MaxValue;
            for (int a = 0; a < picks.Length; a++)
            for (int b = a + 1; b < picks.Length; b++)
                best = Math.Min(best, Hex.Distance(world.Cells[picks[a]].Hex, world.Cells[picks[b]].Hex));
            return best;
        }

        static int SpreadScore(World world, int[] picks, int[] coastDistance)
        {
            int inland = 0;
            foreach (int p in picks)
                inland += Math.Min(coastDistance[p], 5);
            return MinPairDistance(world, picks) * 100 + inland;
        }

        /// <summary>
        /// Capitals far apart, each with the land it reaches, then its towns; at last the land is
        /// cut to what the towns reach (<see cref="Influence.Trim"/>). Returns the owners before
        /// the cut, by which the roads between the capitals are laid.
        /// </summary>
        static int[] Kingdoms(World world, Rng rng, WorldSettings settings, int[] coastDistance)
        {
            Cell[] cells = world.Cells;
            int n = cells.Length;

            // Landmasses, to keep capitals off small islands.
            var mass = new int[n];
            var massSize = new List<int>();
            for (int i = 0; i < n; i++)
                mass[i] = -1;
            for (int i = 0; i < n; i++)
            {
                if (cells[i].IsWater || mass[i] >= 0)
                    continue;
                int label = massSize.Count, size = 0;
                var queue = new Queue<int>();
                queue.Enqueue(i);
                mass[i] = label;
                while (queue.Count > 0)
                {
                    int c = queue.Dequeue();
                    size++;
                    for (int d = 0; d < 6; d++)
                    {
                        int j = world.Neighbor(c, d);
                        if (j >= 0 && cells[j].IsLand && mass[j] < 0)
                        {
                            mass[j] = label;
                            queue.Enqueue(j);
                        }
                    }
                }
                massSize.Add(size);
            }

            var candidates = new List<int>();
            for (int i = 0; i < n; i++)
            {
                Cell c = cells[i];
                bool open = c.Biome == Biome.Meadow || c.Biome == Biome.Plains || c.Biome == Biome.Forest;
                if (open && massSize[mass[i]] >= 40 && coastDistance[i] >= 2)
                    candidates.Add(i);
            }
            if (candidates.Count < settings.Countries)
            {
                candidates.Clear();
                for (int i = 0; i < n; i++)
                {
                    Cell c = cells[i];
                    if (c.IsLand && c.Biome != Biome.Mountains && massSize[mass[i]] >= 12)
                        candidates.Add(i);
                }
            }
            int count = Math.Min(settings.Countries, Math.Min(candidates.Count, Flags.KingdomTincture.Length));
            if (count == 0)
                return Influence.Trim(world);

            // Capitals as far apart as possible: the best of random picks, then hill climbing.
            int[] best = null;
            int bestScore = -1;
            var pool = new List<int>(candidates);
            for (int trial = 0; trial < 300; trial++)
            {
                var picks = new int[count];
                for (int k = 0; k < count; k++)
                {
                    int j = rng.Range(k, pool.Count);
                    (pool[k], pool[j]) = (pool[j], pool[k]);
                    picks[k] = pool[k];
                }
                int score = SpreadScore(world, picks, coastDistance);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = picks;
                }
            }
            for (int step = 0; step < 800; step++)
            {
                int slot = rng.Range(0, count);
                int candidate = candidates[rng.Range(0, candidates.Count)];
                if (Array.IndexOf(best, candidate) >= 0)
                    continue;
                int old = best[slot];
                best[slot] = candidate;
                int score = SpreadScore(world, best, coastDistance);
                if (score >= bestScore)
                    bestScore = score;
                else
                    best[slot] = old;
            }

            // The players' capital stands on the biggest landmass, so their heroes can walk to
            // the wild land.
            int biggest = 0;
            for (int k = 1; k < count; k++)
            {
                if (massSize[mass[best[k]]] > massSize[mass[best[biggest]]])
                    biggest = k;
            }
            if (massSize[mass[best[biggest]]] > massSize[mass[best[0]]])
                (best[0], best[biggest]) = (best[biggest], best[0]);

            var names = new List<string>(Names.Cities);
            rng.Shuffle(names);
            int nameIndex = 0;
            string NextName() => nameIndex < names.Count ? names[nameIndex++] : Names.Numbered(++nameIndex);

            for (int k = 0; k < count; k++)
            {
                var country = new Country { Index = k, Name = Names.Kingdoms[Flags.KingdomTincture[k]] };
                world.Countries.Add(country);
                country.Capital = world.AddCity(k, best[k], true, Towns.CapitalHouses, NextName()).Index;
            }

            // Land: every capital spreads by movement cost; the cheapest claim wins.
            var cost = new float[n];
            var claimant = new int[n];
            for (int i = 0; i < n; i++)
            {
                cost[i] = float.PositiveInfinity;
                claimant[i] = -1;
            }
            var reach = new float[count];
            var heap = new MinHeap();
            for (int k = 0; k < count; k++)
            {
                reach[k] = rng.Range(settings.MinReach, settings.MaxReach);
                int capital = world.Cities[world.Countries[k].Capital].Cell;
                cost[capital] = 0;
                claimant[capital] = k;
                heap.Push(0, capital * 16 + k);
            }
            while (heap.Count > 0)
            {
                heap.Pop(out float d, out int value);
                int i = value >> 4, k = value & 15;
                if (d > cost[i] || claimant[i] != k || cells[i].Owner >= 0)
                    continue;
                cells[i].Owner = k;
                for (int dir = 0; dir < 6; dir++)
                {
                    int j = world.Neighbor(i, dir);
                    if (j < 0 || cells[j].IsWater)
                        continue;
                    float next = d + StepCost(cells[j].Biome);
                    if (next <= reach[k] && next < cost[j])
                    {
                        cost[j] = next;
                        claimant[j] = k;
                        heap.Push(next, j * 16 + k);
                    }
                }
            }

            ClaimWaters(world);
            AddTowns(world, rng, count, NextName);
            world.Player = 0;
            // The land grows from the towns in the game (Influence.Grow): the kingdoms start with
            // what their towns reach, so each has a ring to fill from the first turn.
            int[] owners = Influence.Trim(world);
            ClaimWaters(world);
            return owners;
        }

        /// <summary>
        /// Coastal waters (shallows next to a kingdom's land, on the sea or a lake) belong to the
        /// kingdom with the most land around them.
        /// </summary>
        static void ClaimWaters(World world)
        {
            foreach (Cell c in world.Cells)
            {
                if (c.Biome == Biome.Shallows)
                    c.Owner = Influence.ShoreOwner(world, c.Index);
            }
        }

        static int NearestCity(World world, Hex hex)
        {
            int best = int.MaxValue;
            foreach (City city in world.Cities)
                best = Math.Min(best, Hex.Distance(hex, world.Cells[city.Cell].Hex));
            return best;
        }

        /// <summary>Two or three towns per kingdom, inside its land and apart from other cities.</summary>
        static void AddTowns(World world, Rng rng, int countries, Func<string> nextName)
        {
            Cell[] cells = world.Cells;
            for (int k = 0; k < countries; k++)
            {
                int towns = rng.Range(2, 4);
                for (int t = 0; t < towns; t++)
                {
                    var options = new List<int>();
                    var weights = new List<float>();
                    float total = 0;
                    foreach (Cell c in cells)
                    {
                        if (c.Owner != k || c.IsWater || c.City >= 0 || c.Biome == Biome.Mountains)
                            continue;
                        bool inner = true;
                        for (int d = 0; d < 6 && inner; d++)
                        {
                            int j = world.Neighbor(c.Index, d);
                            inner = j >= 0 && (cells[j].Owner == k || (cells[j].IsWater && cells[j].Owner < 0));
                        }
                        if (!inner || NearestCity(world, c.Hex) < 3)
                            continue;
                        float weight = c.Biome == Biome.Meadow || c.Biome == Biome.Plains ? 3f
                            : c.Biome == Biome.Beach ? 2f : 1f;
                        options.Add(c.Index);
                        weights.Add(weight);
                        total += weight;
                    }
                    if (options.Count == 0)
                        break;
                    float pick = rng.Value() * total;
                    int chosen = options[options.Count - 1];
                    for (int o = 0; o < options.Count; o++)
                    {
                        pick -= weights[o];
                        if (pick < 0)
                        {
                            chosen = options[o];
                            break;
                        }
                    }
                    world.AddCity(k, chosen, false, rng.Range(2, 4), nextName());
                }
            }
        }

        static bool FreeForResource(World world, Cell c)
        {
            if (c.City >= 0 || c.Monster != Monster.None || c.Resource != Resource.None)
                return false;
            for (int d = 0; d < 6; d++)
            {
                int j = world.Neighbor(c.Index, d);
                if (j >= 0 && world.Cells[j].Resource != Resource.None)
                    return false;
            }
            return true;
        }

        static void PlaceResources(World world, Rng rng)
        {
            var order = new List<int>();
            for (int i = 0; i < world.Cells.Length; i++)
                order.Add(i);
            rng.Shuffle(order);
            foreach (int i in order)
            {
                Cell c = world.Cells[i];
                if (!FreeForResource(world, c))
                    continue;
                float r = rng.Value();
                switch (c.Biome)
                {
                    case Biome.Plains:
                        if (r < 0.2f) c.Resource = Resource.Wheat;
                        break;
                    case Biome.Forest:
                        if (r < 0.14f) c.Resource = Resource.Wood;
                        break;
                    case Biome.Meadow:
                        if (r < 0.1f) c.Resource = Resource.Sheep;
                        break;
                    case Biome.Hills:
                    case Biome.Mountains:
                        if (r < 0.17f) c.Resource = Resource.Stone;
                        else if (r < 0.26f) c.Resource = Resource.Gold;
                        break;
                    case Biome.Shallows:
                        if (r < (c.Lake ? 0.12f : 0.07f)) c.Resource = Resource.Fish;
                        break;
                }
            }

            // Every kingdom gets food, wood, stone and gold of its own: the tasks ask for a farm, a
            // lumber camp, a quarry and a gold mine, and without them a kingdom could be stuck
            // (on about one map in seven the nearest gold lay behind a lair or a neighbour).
            foreach (Country country in world.Countries)
            {
                Ensure(world, rng, country.Index, Resource.Wheat, Biome.Plains, Biome.Meadow, true);
                Ensure(world, rng, country.Index, Resource.Wood, Biome.Forest, Biome.Forest, true);
                Ensure(world, rng, country.Index, Resource.Stone, Biome.Hills, Biome.Mountains, false);
                Ensure(world, rng, country.Index, Resource.Gold, Biome.Hills, Biome.Mountains, false);
            }
        }

        /// <summary>
        /// Gives the kingdom the resource if it has none: on its own <paramref name="first"/> or
        /// <paramref name="second"/> land apart from other resources, else on such land beside one,
        /// else on any free land of the kingdom, which becomes <paramref name="first"/> land when
        /// <paramref name="convert"/> is set (a grove for wood, a field for wheat).
        /// </summary>
        static void Ensure(World world, Rng rng, int country, Resource resource, Biome first, Biome second, bool convert)
        {
            var apart = new List<int>();
            var beside = new List<int>();
            var anywhere = new List<int>();
            foreach (Cell c in world.Cells)
            {
                if (c.Owner != country || c.IsWater)
                    continue;
                if (c.Resource == resource)
                    return;
                if (c.City >= 0 || c.Monster != Monster.None || c.Resource != Resource.None)
                    continue;
                if (c.Biome == first || c.Biome == second)
                    (FreeForResource(world, c) ? apart : beside).Add(c.Index);
                else if (c.Biome != Biome.Mountains && c.Biome != Biome.Beach)
                    anywhere.Add(c.Index);
            }
            List<int> options = apart.Count > 0 ? apart : beside.Count > 0 ? beside : anywhere;
            if (options.Count == 0)
                return;
            Cell chosen = world.Cells[options[rng.Range(0, options.Count)]];
            chosen.Resource = resource;
            if (options == anywhere && convert)
                chosen.Biome = first;
        }

        /// <summary>
        /// Dirt roads: every town joins its capital, and the capitals of kingdoms that share a
        /// border join each other. Each road takes the cheapest way over land: hills and mountains
        /// cost more, lairs and foreign land are avoided, and a stretch where a road already runs
        /// is cheap, so roads merge into a network instead of running side by side. The land is
        /// the kingdoms' before it was cut to the towns' reach (<paramref name="owners"/>), so the
        /// capitals of kingdoms whose land met there are joined too.
        /// </summary>
        static void Roads(World world, int[] owners)
        {
            Cell[] cells = world.Cells;
            var links = new List<(int from, int to, int a, int b)>();
            foreach (Country country in world.Countries)
            {
                int capital = world.Cities[country.Capital].Cell;
                var towns = new List<int>();
                foreach (int c in country.Cities)
                {
                    if (c != country.Capital)
                        towns.Add(world.Cities[c].Cell);
                }
                // The nearest towns first, so the farther roads can join them.
                towns.Sort((x, y) => Hex.Distance(cells[x].Hex, cells[capital].Hex)
                    .CompareTo(Hex.Distance(cells[y].Hex, cells[capital].Hex)));
                foreach (int town in towns)
                    links.Add((town, capital, country.Index, country.Index));
            }
            int count = world.Countries.Count;
            var neighbours = new bool[count, count];
            foreach (Cell c in cells)
            {
                if (c.IsWater || owners[c.Index] < 0)
                    continue;
                for (int d = 0; d < 6; d++)
                {
                    int j = world.Neighbor(c.Index, d);
                    if (j >= 0 && cells[j].IsLand && owners[j] >= 0 && owners[j] != owners[c.Index])
                        neighbours[owners[c.Index], owners[j]] = true;
                }
            }
            for (int a = 0; a < count; a++)
            for (int b = a + 1; b < count; b++)
            {
                if (neighbours[a, b])
                    links.Add((world.Cities[world.Countries[a].Capital].Cell, world.Cities[world.Countries[b].Capital].Cell, a, b));
            }
            foreach (var link in links)
            {
                List<int> path = RoadPath(world, owners, link.from, link.to, link.a, link.b);
                if (path == null)
                    continue;
                for (int k = 0; k + 1 < path.Count; k++)
                {
                    for (int d = 0; d < 6; d++)
                    {
                        if (world.Neighbor(path[k], d) != path[k + 1])
                            continue;
                        cells[path[k]].Roads |= (byte)(1 << d);
                        cells[path[k + 1]].Roads |= (byte)(1 << ((d + 3) % 6));
                    }
                }
            }
        }

        /// <summary>The cheapest way over land from one cell to another, or null; the land's owners as <paramref name="owners"/> has them.</summary>
        static List<int> RoadPath(World world, int[] owners, int from, int to, int ownerA, int ownerB)
        {
            Cell[] cells = world.Cells;
            int n = cells.Length;
            var cost = new float[n];
            var previous = new int[n];
            for (int i = 0; i < n; i++)
            {
                cost[i] = float.PositiveInfinity;
                previous[i] = -1;
            }
            var heap = new MinHeap();
            cost[from] = 0;
            heap.Push(0, from);
            while (heap.Count > 0)
            {
                heap.Pop(out float d, out int i);
                if (d > cost[i])
                    continue;
                if (i == to)
                    break;
                for (int dir = 0; dir < 6; dir++)
                {
                    int j = world.Neighbor(i, dir);
                    if (j < 0 || cells[j].IsWater)
                        continue;
                    Cell c = cells[j];
                    float step = StepCost(c.Biome);
                    if (owners[j] != ownerA && owners[j] != ownerB)
                        step *= owners[j] < 0 ? 1.5f : 3f;
                    if (c.Monster != Monster.None)
                        step *= 4f;
                    if (c.Roads != 0)
                        step *= 0.35f;
                    // A new bridge costs a little: roads gather at the ones already built.
                    if (((cells[i].Rivers >> dir) & 1) != 0 && ((cells[i].Roads >> dir) & 1) == 0)
                        step += 1.5f;
                    float next = d + step;
                    if (next < cost[j])
                    {
                        cost[j] = next;
                        previous[j] = i;
                        heap.Push(next, j);
                    }
                }
            }
            if (float.IsPositiveInfinity(cost[to]))
                return null;
            var path = new List<int>();
            for (int i = to; i >= 0; i = previous[i])
                path.Add(i);
            path.Reverse();
            return path;
        }
    }
}
