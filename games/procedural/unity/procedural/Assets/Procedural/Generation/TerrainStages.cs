using System;
using System.Threading;

namespace Procedural.Generation
{
    internal sealed class TerrainState
    {
        internal Grid Mask, Uplift, Relief, Height;
        internal bool[] Ocean, Rock;
        internal float[] Beach;
        internal int Iterations, StableIterations;
        internal float Changed, ChangedAll, NetworkChanged = 1, RelativeHeightChange = 1;
        internal float FlipsFromUphill, FlipsFromNearTies, FixedPointResidual = 1;
    }

    internal static class TerrainStages
    {
        // Threshold hillslopes leave headroom for interpolation/detail below 45 degree talus.
        internal const float StreamPowerM = 0.65f;
        internal const float CriticalSlope = 0.78128563f; // tan(38 degrees)
        internal static IslandRecipe Resolve(IslandRecipe input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            IslandRecipe r = input.Clone();
            bool small = r.preset == Preset.Small;
            if (r.worldSize == 0)
                r.worldSize = small ? 1024 : 2048;
            if (r.simResolution == 0)
                r.simResolution = small ? 256 : 512;
            if (r.heightmapResolution == 0)
                r.heightmapResolution = 2049; // 0.5 m on Small, 1 m on Medium
            if (r.peakHeight == 0)
                r.peakHeight = small ? 100 : 160;
            if (r.seaDepth == 0)
                r.seaDepth = small ? 30 : 50;
            if (r.maxRiverWidth == 0)
                r.maxRiverWidth = small ? 8 : 15;
            if (r.maxRiverDepth == 0)
                r.maxRiverDepth = small ? 1.5f : 2.5f;
            if (r.implicitTimeStep == 0)
                r.implicitTimeStep = 1;
            if (r.beachWidth == 0)
                r.beachWidth = Math.Min(32, r.worldSize / 32);
            if (r.channelHeadArea == 0)
                r.channelHeadArea = 1000f * (float)Math.Pow(10, 1 - r.ruggedness) * Math.Min(1, r.worldSize / 1024) * Math.Min(1, r.worldSize / 1024);
            float[] values =
            {
                r.worldSize,
                r.peakHeight,
                r.seaDepth,
                r.maxRiverWidth,
                r.maxRiverDepth,
                r.landFraction,
                r.coastRoughness,
                r.elongation,
                r.orientationDegrees,
                r.ruggedness,
                r.riverDensity,
                r.beachShare,
                r.beachWidth,
                r.channelHeadArea,
                r.erosionDropletsPerSquareMetre,
                r.implicitTimeStep
            };
            for (int i = 0; i < values.Length; i++)
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
                    throw new ArgumentException("Recipe values must be finite.");
            ValidateRanges(r);
            return r;
        }

        private static void ValidateRanges(IslandRecipe r)
        {
            bool scale = r.worldSize > 0 && r.peakHeight > 0 && r.seaDepth > 0;
            bool rivers = r.maxRiverWidth > 0 && r.maxRiverDepth > 0 && r.riverDensity >= 0;
            bool land = r.landFraction > 0 && r.landFraction < 0.81f && r.elongation >= 1;
            bool shape = UnitRange(r.coastRoughness) && r.massifs >= 0 && r.massifs <= 1000;
            bool hills = UnitRange(r.ruggedness) && r.channelHeadArea > 0 && r.implicitTimeStep > 0;
            bool coast = UnitRange(r.beachShare) && r.beachWidth > 0 && r.lakes >= 0;
            bool detail = r.erosionDropletsPerSquareMetre >= 0 && UnitRange(r.forestCover) && UnitRange(r.microRelief);
            bool grid = r.simResolution >= 8 && r.simResolution <= 4096;
            bool heightmap = r.heightmapResolution >= 17 && r.heightmapResolution <= 8193;
            bool powerOfTwo = ((r.heightmapResolution - 1) & (r.heightmapResolution - 2)) == 0;
            bool enums = Enum.IsDefined(typeof(Preset), r.preset) && Enum.IsDefined(typeof(ReliefMode), r.relief);
            if (!(scale && rivers && land && shape && hills && coast && detail && grid && heightmap && powerOfTwo && enums))
                throw new ArgumentException("Recipe settings are outside their supported ranges (land fraction < 0.81, grid >= 8, heightmap 2^k+1 >= 17).");
        }

        private static bool UnitRange(float value)
        {
            return value >= 0 && value <= 1;
        }

        internal static bool[] SelectLand(Grid mask, float fraction, CancellationToken cancel)
        {
            int n = mask.Data.Length, w = mask.Width, h = mask.Height, target = (int)Math.Round(n * (double)fraction);
            if (target < 1 || target >= n)
                throw new ArgumentException("Land target must contain at least one land and sea sample.");
            bool[] land = RankLand(mask, n, target, cancel);
            var seen = new bool[n];
            var queue = new int[n];
            int root = LargestLandComponent(land, w, h, n, seen, queue, cancel);
            int end = FloodFromRoot(root, w, h, land, seen, queue, n, cancel);
            if (end == target)
                return seen;
            return GrowToTarget(mask, seen, queue, end, target, cancel);
        }

        private static bool[] RankLand(Grid mask, int n, int target, CancellationToken cancel)
        {
            var rank = new int[n];
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                rank[i] = i;
            }

            MathCore.SortIndices(rank, n, mask.Data, true, cancel);
            if (mask.Data[rank[target - 1]] < -1e10f)
                throw new ArgumentException("Requested land cannot fit inside the sea border.");
            var land = new bool[n];
            for (int i = 0; i < target; i++)
            {
                MathCore.Check(cancel, i);
                land[rank[i]] = true;
            }

            return land;
        }

        private static int LargestLandComponent(bool[] land, int w, int h, int n, bool[] seen, int[] queue, CancellationToken cancel)
        {
            int largest = 0, root = -1;
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                if (!land[i] || seen[i])
                    continue;
                int head = 0, tail = 1;
                queue[0] = i;
                seen[i] = true;
                while (head < tail)
                {
                    MathCore.Check(cancel, head);
                    int c = queue[head++], x = c % w, z = c / w;
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx, zz = z + dz;
                            if (xx < 0 || zz < 0 || xx >= w || zz >= h)
                                continue;
                            int j = zz * w + xx;
                            if (land[j] && !seen[j])
                            {
                                seen[j] = true;
                                queue[tail++] = j;
                            }
                        }
                }

                if (tail > largest)
                {
                    largest = tail;
                    root = i;
                }
            }

            return root;
        }

        private static int FloodFromRoot(int root, int w, int h, bool[] land, bool[] seen, int[] queue, int n, CancellationToken cancel)
        {
            Array.Clear(seen, 0, n);
            int start = 0, end = 1;
            queue[0] = root;
            seen[root] = true;
            while (start < end)
            {
                MathCore.Check(cancel, start);
                int c = queue[start++], x = c % w, z = c / w;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, zz = z + dz;
                        if (xx < 0 || zz < 0 || xx >= w || zz >= h)
                            continue;
                        int j = zz * w + xx;
                        if (land[j] && !seen[j])
                        {
                            seen[j] = true;
                            queue[end++] = j;
                        }
                    }
            }

            return end;
        }

        private static bool[] GrowToTarget(Grid mask, bool[] land, int[] queue, int end, int target, CancellationToken cancel)
        {
            // Quantile removal can lose small islets. Restore count by connected, score-prioritized growth.
            var heap = new MinHeap(mask.Data.Length);
            var queued = (bool[])land.Clone();
            for (int k = 0; k < end; k++)
            {
                MathCore.Check(cancel, k);
                AddFrontier(queue[k], mask, land, queued, heap);
            }

            while (end < target)
            {
                MathCore.Check(cancel, end);
                int c = heap.Pop();
                land[c] = true;
                end++;
                AddFrontier(c, mask, land, queued, heap);
            }

            return land;
        }

        private static void AddFrontier(int c, Grid mask, bool[] land, bool[] queued, MinHeap heap)
        {
            int w = mask.Width, x = c % w, z = c / w;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int xx = x + dx, zz = z + dz;
                    if (xx < 0 || zz < 0 || xx >= w || zz >= mask.Height)
                        continue;
                    int j = zz * w + xx;
                    if (!queued[j] && !land[j] && mask.Data[j] > -1e10f)
                    {
                        queued[j] = true;
                        heap.Push(j, -mask.Data[j]);
                    }
                }
        }

        internal static void Shape(IslandRecipe r, TerrainState s, CancellationToken cancel)
        {
            int w = r.simResolution;
            float cell = r.worldSize / (w - 1);
            s.Mask = new Grid(w, w, cell);
            s.Uplift = new Grid(w, w, cell);
            ulong seed = MathCore.Stage(r.seed, "Shape"), rng = seed;
            float angle = (r.orientationDegrees < 0 ? MathCore.Random(ref rng) * 360 : r.orientationDegrees) * (float)(Math.PI / 180);
            float cs = (float)Math.Cos(angle), sn = (float)Math.Sin(angle), stretch = (float)Math.Sqrt(r.elongation);
            BuildMask(r, s.Mask, w, seed, angle, cs, sn, stretch, cancel);
            bool[] land = SelectLand(s.Mask, r.landFraction, cancel);
            s.Ocean = new bool[land.Length];
            float threshold = FindLandThreshold(s.Mask, land, cancel);
            float maxMask = ApplyLandMask(s.Mask, s.Ocean, land, threshold, cancel);
            float[] coastDistance = DistanceTo(s.Ocean, w, w, cancel);
            float maximumInland = MeasureMaximumInland(land, coastDistance, cell);
            // Leave enough horizontal run for a peak's critical slope plus its
            // coastal flat. Narrow custom islands use their best available interior.
            float minimumMassifInland = Math.Min(maximumInland * 0.65f, r.peakHeight / CriticalSlope + r.beachWidth);
            float[] mx = new float[r.massifs], mz = new float[r.massifs];
            PickMassifSites(r, s.Mask, land, w, coastDistance, cell, maxMask, minimumMassifInland, mx, mz, ref rng, cancel);
            BuildUplift(r, s.Uplift, land, w, seed, angle, mx, mz, cancel);
        }

        private static void BuildMask(IslandRecipe r, Grid mask, int w, ulong seed, float angle, float cs, float sn, float stretch, CancellationToken cancel)
        {
            for (int z = 0; z < w; z++)
                for (int x = 0; x < w; x++)
                {
                    int i = z * w + x;
                    MathCore.Check(cancel, i);
                    float u = x / (float)(w - 1), v = z / (float)(w - 1);
                    if (u <= 0.05f || u >= 0.95f || v <= 0.05f || v >= 0.95f)
                    {
                        mask.Data[i] = -1e20f;
                        continue;
                    }

                    float a = u * 2 - 1, b = v * 2 - 1;
                    a += r.coastRoughness * 0.65f * (MathCore.Fbm(u * 3, v * 3, seed) - 0.5f);
                    b += r.coastRoughness * 0.65f * (MathCore.Fbm(u * 3, v * 3, seed + 19) - 0.5f);
                    float xx = (cs * a + sn * b) / stretch, zz = (-sn * a + cs * b) * stretch;
                    float heading = (float)Math.Atan2(zz, xx);
                    float bays = 0.18f * (float)Math.Sin(5 * heading + angle) + 0.11f * (float)Math.Sin(9 * heading - angle * 2) + 0.065f * (float)Math.Sin(15 * heading + angle * 3);
                    mask.Data[i] = 1 - (float)Math.Sqrt(xx * xx + zz * zz) + r.coastRoughness * (1.7f * bays + 0.9f * (MathCore.Fbm(u * 7, v * 7, seed + 37) - 0.5f));
                }
        }

        private static float FindLandThreshold(Grid mask, bool[] land, CancellationToken cancel)
        {
            float threshold = float.MaxValue;
            for (int i = 0; i < land.Length; i++)
            {
                MathCore.Check(cancel, i);
                if (land[i])
                    threshold = Math.Min(threshold, mask.Data[i]);
            }

            return threshold;
        }

        private static float ApplyLandMask(Grid mask, bool[] ocean, bool[] land, float threshold, CancellationToken cancel)
        {
            float maxMask = 0;
            for (int i = 0; i < land.Length; i++)
            {
                MathCore.Check(cancel, i);
                ocean[i] = !land[i];
                mask.Data[i] = land[i] ? Math.Max(0.001f, mask.Data[i] - threshold) : -0.001f;
                maxMask = Math.Max(maxMask, mask.Data[i]);
            }

            return maxMask;
        }

        private static float MeasureMaximumInland(bool[] land, float[] coastDistance, float cell)
        {
            float maximumInland = 0;
            for (int i = 0; i < land.Length; i++)
                maximumInland = Math.Max(maximumInland, land[i] ? coastDistance[i] * cell : 0);
            return maximumInland;
        }

        private static void PickMassifSites(IslandRecipe r, Grid mask, bool[] land, int w, float[] coastDistance, float cell, float maxMask, float minimumMassifInland, float[] mx, float[] mz, ref ulong rng, CancellationToken cancel)
        {
            for (int j = 0; j < r.massifs; j++)
            {
                int i = 0, bestSite = 0;
                float bestSeparation = -1;
                for (int attempt = 0; attempt < 128; attempt++)
                {
                    do
                    {
                        i = Math.Min(land.Length - 1, (int)(MathCore.Random(ref rng) * land.Length));
                        cancel.ThrowIfCancellationRequested();
                    }
                    while (!land[i] || mask.Data[i] < maxMask * 0.25f || coastDistance[i] * cell < minimumMassifInland);
                    float separation = 1;
                    for (int k = 0; k < j; k++)
                    {
                        float dx = i % w / (float)(w - 1) - mx[k];
                        float dz = i / w / (float)(w - 1) - mz[k];
                        separation = Math.Min(separation, dx * dx + dz * dz);
                    }

                    if (separation > bestSeparation)
                    {
                        bestSeparation = separation;
                        bestSite = i;
                    }

                    if (separation >= 0.24f * 0.24f / Math.Max(1, r.massifs / 2f))
                        break;
                }

                i = bestSite;
                mx[j] = i % w / (float)(w - 1);
                mz[j] = i / w / (float)(w - 1);
            }
        }

        private static void BuildUplift(IslandRecipe r, Grid uplift, bool[] land, int w, ulong seed, float angle, float[] mx, float[] mz, CancellationToken cancel)
        {
            for (int i = 0; i < land.Length; i++)
            {
                MathCore.Check(cancel, i);
                if (!land[i])
                    continue;
                float x = i % w / (float)(w - 1), z = i / w / (float)(w - 1), blobs = 0;
                for (int j = 0; j < r.massifs; j++)
                {
                    float dx = x - mx[j], dz = z - mz[j];
                    float ridgeAngle = angle + j * 2.4f;
                    float along = dx * (float)Math.Cos(ridgeAngle) + dz * (float)Math.Sin(ridgeAngle);
                    float across = -dx * (float)Math.Sin(ridgeAngle) + dz * (float)Math.Cos(ridgeAngle);
                    blobs = Math.Max(blobs, (float)Math.Exp(-along * along / 0.028f - across * across / 0.009f));
                }

                float ridge = 1 - Math.Abs(2 * MathCore.Fbm(x * 5, z * 5, seed + 71) - 1);
                uplift.Data[i] = 0.36f + 2.5f * blobs * (0.7f + r.ruggedness * ridge);
            }
        }

        internal static void Relief(IslandRecipe r, BakeMode mode, TerrainState s, CancellationToken cancel, Action<float> progress)
        {
            int w = r.simResolution, n = w * w;
            s.Relief = new Grid(w, w, s.Mask.CellSize);
            ulong seed = MathCore.Stage(r.seed, "Relief");
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                if (!s.Ocean[i])
                    if (r.relief == ReliefMode.Noise)
                        s.Relief.Data[i] = s.Uplift.Data[i] * (0.3f + MathCore.Fbm(i % w * 0.04f, i / w * 0.04f, seed));
                    else
                        s.Relief.Data[i] = 0.001f * (0.1f + MathCore.Noise(i % w, i / w, seed));
            }

            if (r.relief != ReliefMode.Noise)
            {
                GrowNetwork(r, mode, s, cancel, progress);
            }

            // Shape hillslopes only after growth has settled, keeping its topology
            // fixed during the solve. Smoothing and physical slope projection can
            // change the final heightmap's free-routing graph; growth diagnostics
            // describe the network before this separate shape phase.
            if (r.relief != ReliefMode.Noise)
                SmoothRelief(s.Relief, s.Ocean, 10, cancel);
            CalibrateProjectedPeak(r, s, cancel);
            // Projection is min-plus on D8 edges and introduces octagonal faces.
            // Diffuse those faces AFTER projection; pre-projection smoothing alone
            // cannot remove them. A broad kernel also removes cubic grid ringing.
            float[] diffused = Blur(s.Relief.Data, s.Relief.Width, s.Relief.Height, 1, 1, cancel);
            Array.Copy(diffused, s.Relief.Data, diffused.Length);
            float peak = 0;
            for (int i = 0; i < s.Relief.Data.Length; i++)
            {
                if (s.Ocean[i])
                    s.Relief.Data[i] = 0;
                peak = Math.Max(peak, s.Relief.Data[i]);
            }

            float scale = r.peakHeight / Math.Max(0.0001f, peak);
            for (int i = 0; i < s.Relief.Data.Length; i++)
                s.Relief.Data[i] *= scale;
        }

        // Experiment parameters are internal; recipes and public entry points stay unchanged.
        internal const float ReliefSlopeLimit = 0.65f;
        internal const float RoutingHysteresis = 0.05f;
        private static void GrowNetwork(IslandRecipe r, BakeMode mode, TerrainState s, CancellationToken cancel, Action<float> progress)
        {
            var growth = new NetworkGrowth(r, mode, s, cancel, progress);
            Drainage d = growth.Run();
            SettleHillslopes(r, growth.ReliefGrid, growth.Uplift, d, cancel);
        }

        private sealed class NetworkGrowth
        {
            private readonly IslandRecipe r;
            private readonly TerrainState s;
            private readonly CancellationToken cancel;
            private readonly Action<float> progress;
            private readonly Grid g;
            private readonly int n;
            private readonly int max;
            private readonly float[] uplift;
            private int[] previous;
            private bool[] previousChannel;
            private int stable;

            internal Grid ReliefGrid
            {
                get { return g; }
            }

            internal float[] Uplift
            {
                get { return uplift; }
            }

            internal NetworkGrowth(IslandRecipe r, BakeMode mode, TerrainState s, CancellationToken cancel, Action<float> progress)
            {
                this.r = r;
                this.s = s;
                this.cancel = cancel;
                this.progress = progress;
                g = s.Relief;
                n = g.Data.Length;
                uplift = s.Uplift.Data;
                max = mode == BakeMode.Preview ? 100 : 250;
            }

            internal Drainage Run()
            {
                Drainage d = null;
                for (int k = 0; k < max; k++)
                {
                    cancel.ThrowIfCancellationRequested();
                    d = Hydrology.Route(g, s.Ocean, cancel, previous, RoutingHysteresis, k > 0);
                    IterationMetrics m = Step(d);
                    s.Iterations = k + 1;
                    s.ChangedAll = m.Flips / (float)Math.Max(1, d.Stack.Length);
                    s.Changed = m.ChannelFlips / (float)Math.Max(1, m.Channels);
                    s.RelativeHeightChange = s.FixedPointResidual = (float)(m.Change / Math.Max(1e-12, m.Magnitude));
                    s.FlipsFromUphill = m.Uphill / (float)Math.Max(1, m.Flips);
                    s.FlipsFromNearTies = m.Ties / (float)Math.Max(1, m.Flips);
                    stable = s.Changed < 0.01f && s.ChangedAll < 0.01f && s.FixedPointResidual < 0.001f ? stable + 1 : 0;
                    s.StableIterations = stable;
                    if (stable >= 3 || k == max - 1)
                        s.NetworkChanged = ChannelDisplacement(m.Channel, previousChannel, g.Width, g.Height, cancel);
                    previous = d.Receiver;
                    previousChannel = m.Channel;
                    progress((k + 1f) / max);
                    if (stable >= 3)
                        break;
                }

                return d;
            }

            private IterationMetrics Step(Drainage d)
            {
                int flips = 0, channelFlips = 0, channels = 0, uphill = 0, ties = 0;
                var channel = new bool[n];
                double change = 0, magnitude = 0;
                for (int j = 0; j < d.Stack.Length; j++)
                {
                    MathCore.Check(cancel, j);
                    int i = d.Stack[j];
                    bool flipped = previous == null || previous[i] != d.Receiver[i];
                    channel[i] = d.Discharge[i] >= r.channelHeadArea;
                    if (flipped)
                    {
                        flips++;
                        if (previous != null)
                        {
                            int old = previous[i], dx = old % g.Width - i % g.Width, dz = old / g.Width - i / g.Width;
                            double length = dx != 0 && dz != 0 ? Math.Sqrt(2) : 1;
                            double oldSlope = ((double)d.Fill[i] - d.Fill[old]) / (length * g.CellSize);
                            double newSlope = ((double)d.Fill[i] - d.Fill[d.Receiver[i]]) / d.Distance[i];
                            if (oldSlope <= 0)
                                uphill++;
                            if (Math.Abs(newSlope - oldSlope) <= Math.Max(1e-8, Math.Abs(newSlope) * 0.05))
                                ties++;
                        }
                    }

                    if (channel[i] || (previousChannel != null && previousChannel[i]))
                    {
                        channels++;
                        if (flipped)
                            channelFlips++;
                    }

                    // Pure stream power: no convex branch, height damping or update bound.
                    double height = g.Data[d.Receiver[i]] + uplift[i] * (double)d.Distance[i] / Math.Pow(d.Discharge[i], StreamPowerM);
                    change += Math.Abs(height - g.Data[i]);
                    magnitude += height;
                    g.Data[i] = (float)height;
                }

                return new IterationMetrics
                {
                    Flips = flips,
                    ChannelFlips = channelFlips,
                    Channels = channels,
                    Uphill = uphill,
                    Ties = ties,
                    Change = change,
                    Magnitude = magnitude,
                    Channel = channel
                };
            }
        }

        private struct IterationMetrics
        {
            internal int Flips, ChannelFlips, Channels, Uphill, Ties;
            internal double Change, Magnitude;
            internal bool[] Channel;
        }

        private static void SettleHillslopes(IslandRecipe r, Grid g, float[] uplift, Drainage d, CancellationToken cancel)
        {
            int n = g.Data.Length;
            // On the settled graph, fit the convex hillslopes and physical slope
            // limit to the requested peak. This phase does not feed back into routing.
            var factors = new float[n];
            var gradient = new float[n];
            for (int j = 0; j < d.Stack.Length; j++)
            {
                int i = d.Stack[j];
                factors[i] = AreaFactor(d.Discharge[i], r.channelHeadArea);
                gradient[i] = uplift[i] * factors[i];
            }

            float[] oldHeight = (float[])g.Data.Clone();
            float low = 0, high = 1;
            while (Sweep(r, g, d, gradient, factors, high, oldHeight, cancel) < r.peakHeight && high < 1e12f)
                high *= 2;
            for (int step = 0; step < 28; step++)
            {
                float mid = (low + high) * 0.5f;
                if (Sweep(r, g, d, gradient, factors, mid, oldHeight, cancel) < r.peakHeight)
                    low = mid;
                else
                    high = mid;
            }

            Sweep(r, g, d, gradient, factors, high, oldHeight, cancel);
        }

        private static float ChannelDisplacement(bool[] channel, bool[] previous, int width, int height, CancellationToken cancel)
        {
            if (previous == null)
                return 1;
            float[] before = DistanceTo(previous, width, height, cancel);
            float[] after = DistanceTo(channel, width, height, cancel);
            int count = 0, displaced = 0;
            for (int i = 0; i < channel.Length; i++)
            {
                MathCore.Check(cancel, i);
                if (channel[i])
                {
                    count++;
                    if (before[i] > 1.5f)
                        displaced++;
                }

                if (previous[i])
                {
                    count++;
                    if (after[i] > 1.5f)
                        displaced++;
                }
            }

            return displaced / (float)Math.Max(1, count);
        }

        internal static float AreaFactor(float area, float headArea)
        {
            return area < headArea ? area / headArea * (float)Math.Pow(headArea, -StreamPowerM) : (float)Math.Pow(area, -StreamPowerM);
        }

        // Isotropic nine-point diffusion removes raster frequencies before bicubic interpolation.
        private static void SmoothRelief(Grid grid, bool[] ocean, int passes, CancellationToken cancel)
        {
            float[] scratch = new float[grid.Data.Length];
            for (int pass = 0; pass < passes; pass++)
            {
                Array.Copy(grid.Data, scratch, scratch.Length);
                for (int z = 1; z < grid.Height - 1; z++)
                {
                    for (int x = 1; x < grid.Width - 1; x++)
                    {
                        int i = z * grid.Width + x;
                        MathCore.Check(cancel, i);
                        if (ocean[i])
                            continue;
                        float sum = 0;
                        for (int dz = -1; dz <= 1; dz++)
                            for (int dx = -1; dx <= 1; dx++)
                                sum += scratch[i + dz * grid.Width + dx] * (dx == 0 ? 2 : 1) * (dz == 0 ? 2 : 1);
                        grid.Data[i] = sum / 16;
                    }
                }
            }
        }

        // Separable box diffusion: fixed metre support at callers, O(passes*N),
        // double rolling sums, no branch on nearly equal floating-point values.
        internal static float[] Blur(float[] source, int width, int height, int radius, int passes, CancellationToken cancel)
        {
            var current = (float[])source.Clone();
            var scratch = new float[source.Length];
            int diameter = radius * 2 + 1;
            for (int pass = 0; pass < passes; pass++)
            {
                for (int z = 0; z < height; z++)
                {
                    MathCore.Check(cancel, z * width);
                    double sum = 0;
                    for (int k = -radius; k <= radius; k++)
                        sum += current[z * width + Math.Max(0, Math.Min(width - 1, k))];
                    for (int x = 0; x < width; x++)
                    {
                        scratch[z * width + x] = (float)(sum / diameter);
                        sum += current[z * width + Math.Min(width - 1, x + radius + 1)] - (double)current[z * width + Math.Max(0, x - radius)];
                    }
                }

                for (int x = 0; x < width; x++)
                {
                    MathCore.Check(cancel, x);
                    double sum = 0;
                    for (int k = -radius; k <= radius; k++)
                        sum += scratch[Math.Max(0, Math.Min(height - 1, k)) * width + x];
                    for (int z = 0; z < height; z++)
                    {
                        current[z * width + x] = (float)(sum / diameter);
                        sum += scratch[Math.Min(height - 1, z + radius + 1) * width + x] - (double)scratch[Math.Max(0, z - radius) * width + x];
                    }
                }
            }

            return current;
        }

        private static void CalibrateProjectedPeak(IslandRecipe r, TerrainState s, CancellationToken cancel)
        {
            // Bisect amplitude followed by a physical slope cap; never rescale a cap.
            float[] raw = (float[])s.Relief.Data.Clone();
            float low = 0, high = r.relief == ReliefMode.Noise ? r.peakHeight : 1;
            while (ProjectedPeak(s.Relief, raw, s.Ocean, high, cancel) < r.peakHeight && high < 1e12f)
                high *= 2;
            for (int step = 0; step < 28; step++)
            {
                float mid = (low + high) * 0.5f;
                if (ProjectedPeak(s.Relief, raw, s.Ocean, mid, cancel) < r.peakHeight)
                    low = mid;
                else
                    high = mid;
            }

            ProjectedPeak(s.Relief, raw, s.Ocean, high, cancel);
        }

        private static float Sweep(IslandRecipe r, Grid g, Drainage d, float[] gradient, float[] factors, float scale, float[] oldHeight, CancellationToken cancel)
        {
            float peak = 0;
            for (int j = 0; j < d.Stack.Length; j++)
            {
                MathCore.Check(cancel, j);
                int i = d.Stack[j];
                float receiverHeight = g.Data[d.Receiver[i]];
                float equilibrium = receiverHeight + d.Distance[i] * Math.Min(scale * gradient[i], ReliefSlopeLimit);
                if (r.relief == ReliefMode.ImplicitStreamPower)
                {
                    // Backward Euler, n=1, receiver-first (Fastscape order).
                    // dh/dt = s U - S/g(A), so K(A) = 1/g(A).
                    float factor = factors[i];
                    float a = r.implicitTimeStep / (factor * d.Distance[i]);
                    float uplifted = oldHeight[i] + r.implicitTimeStep * scale * gradient[i] / factor;
                    float implicitHeight = (uplifted + a * receiverHeight) / (1 + a);
                    equilibrium = MathCore.Clamp(implicitHeight, receiverHeight, receiverHeight + d.Distance[i] * ReliefSlopeLimit);
                }

                g.Data[i] = equilibrium;
                peak = Math.Max(peak, g.Data[i]);
            }

            return peak;
        }

        private static float ProjectedPeak(Grid g, float[] raw, bool[] ocean, float scale, CancellationToken cancel)
        {
            for (int i = 0; i < raw.Length; i++)
            {
                MathCore.Check(cancel, i);
                g.Data[i] = raw[i] * scale;
            }

            ClampSlopes(g, ocean, ReliefSlopeLimit, null, cancel);
            float peak = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                MathCore.Check(cancel, i);
                peak = Math.Max(peak, g.Data[i]);
            }

            return peak;
        }

        internal static float[] DistanceTo(bool[] source, int w, int h, CancellationToken cancel)
        {
            return EuclideanDistance.Transform(source, w, h, cancel);
        }

        internal static Grid Bicubic(Grid g, int resolution, CancellationToken cancel)
        {
            var result = new Grid(resolution, resolution, (g.Width - 1) * g.CellSize / (resolution - 1));
            float ratio = (g.Width - 1f) / (resolution - 1);
            for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                {
                    int i = z * resolution + x;
                    MathCore.Check(cancel, i);
                    float xx = x * ratio, zz = z * ratio;
                    int ix = (int)xx, iz = (int)zz;
                    float a = Row(g, ix, iz - 1, xx - ix), b = Row(g, ix, iz, xx - ix), c = Row(g, ix, iz + 1, xx - ix), d = Row(g, ix, iz + 2, xx - ix);
                    result.Data[i] = MathCore.Cubic(a, b, c, d, zz - iz);
                }

            return result;
        }

        private static float Row(Grid g, int x, int z, float t)
        {
            z = Math.Max(0, Math.Min(g.Height - 1, z));
            return MathCore.Cubic(g[Math.Max(0, x - 1), z], g[Math.Min(g.Width - 1, x), z], g[Math.Min(g.Width - 1, x + 1), z], g[Math.Min(g.Width - 1, x + 2), z], t);
        }

        internal static void Upsample(IslandRecipe r, BakeMode mode, TerrainState s, CancellationToken cancel)
        {
            int w = mode == BakeMode.Preview ? (r.heightmapResolution - 1) / 2 + 1 : r.heightmapResolution;
            s.Height = Bicubic(s.Relief, w, cancel);
            Grid mask = Bicubic(s.Mask, w, cancel);
            // Land count is calibrated again at the output sampling resolution.
            for (int z = 0; z < w; z++)
                for (int x = 0; x < w; x++)
                {
                    MathCore.Check(cancel, z * w + x);
                    if (x < w * 0.05f || z < w * 0.05f || x > (w - 1) * 0.95f || z > (w - 1) * 0.95f)
                        mask[x, z] = -1e20f;
                }

            bool[] land = SelectLand(mask, r.landFraction, cancel);
            int n = land.Length;
            s.Ocean = new bool[n];
            s.Rock = new bool[n];
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                s.Ocean[i] = !land[i];
                s.Height.Data[i] = land[i] ? Math.Max(0.02f, s.Height.Data[i]) : 0;
            }

            float[] inland = DistanceTo(s.Ocean, w, w, cancel), offshore = DistanceTo(land, w, w, cancel);
            Drainage initial = Hydrology.Route(s.Height, s.Ocean, cancel);
            var channel = new bool[n];
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                channel[i] = land[i] && initial.Discharge[i] >= initial.Stack.Length * s.Height.CellSize * s.Height.CellSize * 0.002f;
            }

            float[] channelDistance = DistanceTo(channel, w, w, cancel);
            s.Beach = BeachMask(r, s, inland, cancel);
            ulong seed = MathCore.Stage(r.seed, "Upsample");
            float cell = s.Height.CellSize;
            float[] baseHeight = (float[])s.Height.Data.Clone();
            int reliefRadius = Math.Max(1, (int)Math.Ceiling(6 / cell));
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                int x = i % w, z = i / w;
                if (!land[i])
                {
                    s.Height.Data[i] = -r.seaDepth * MathCore.Smooth(0, r.worldSize * 0.16f, offshore[i] * cell);
                    continue;
                }

                float lo = baseHeight[i], hi = lo;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = Math.Max(0, Math.Min(w - 1, x + dx * reliefRadius)), zz = Math.Max(0, Math.Min(w - 1, z + dz * reliefRadius));
                        float height = baseHeight[zz * w + xx];
                        lo = Math.Min(lo, height);
                        hi = Math.Max(hi, height);
                    }

                float local = MathCore.Smooth(0, 12, hi - lo);
                float detail = (MathCore.Fbm(x * cell / 25, z * cell / 25, seed, 3) - 0.5f) * 5 * r.ruggedness * local * MathCore.Smooth(0, 12, channelDistance[i] * cell);
                s.Height.Data[i] = Math.Max(0.02f, s.Height.Data[i] + detail);
                float distance = inland[i] * cell;
                if (s.Beach[i] > 0 && distance < r.beachWidth * 1.75f)
                {
                    float ramp = distance * 0.06f;
                    float blend = MathCore.Smooth(r.beachWidth, r.beachWidth * 1.75f, distance);
                    s.Height.Data[i] = Math.Min(s.Height.Data[i], ramp * (1 - blend) * s.Beach[i] + s.Height.Data[i] * (1 - s.Beach[i] * (1 - blend)));
                }
            }

            if (r.microRelief > 0)
                MicroRelief(r, s, channelDistance, inland, cancel);

            // Smooth the Euclidean nearest-coast envelope's medial-axis seams.
            // Keep land and the coastline exact; only submerged samples are copied.
            var submerged = new float[n];
            for (int i = 0; i < n; i++)
                submerged[i] = s.Ocean[i] ? s.Height.Data[i] : 0;
            float[] seabed = Blur(submerged, w, w, Math.Max(1, (int)Math.Ceiling(24 / cell)), 3, cancel);
            for (int i = 0; i < n; i++)
                if (s.Ocean[i])
                    s.Height.Data[i] += (Math.Min(-0.001f, seabed[i]) - s.Height.Data[i]) * MathCore.Smooth(16, 64, offshore[i] * cell);
            ClampSlopes(s.Height, s.Ocean, 1, s.Rock, cancel);
        }

        /// <summary>
        /// Metre-scale relief the macro relief cannot carry: ridged outcrops and cracks on
        /// steep rock, scree bumps on slopes below, soil hummocks elsewhere. Smooth near
        /// channels, on beaches and at the shore; runs before lakes and rivers, which shape
        /// and check the result. (Terraced strata were tried: their steps read as stairs.)
        /// </summary>
        private static void MicroRelief(IslandRecipe r, TerrainState s, float[] channelDistance, float[] inland, CancellationToken cancel)
        {
            Grid g = s.Height;
            int w = g.Width, n = g.Data.Length;
            float cell = g.CellSize, amount = r.microRelief * (0.5f + r.ruggedness);
            ulong seed = MathCore.Stage(r.seed, "MicroRelief");
            float rockFrom = (float)Math.Tan(24 * Math.PI / 180), rockTo = (float)Math.Tan(38 * Math.PI / 180);
            float screeFrom = (float)Math.Tan(15 * Math.PI / 180), screeTo = (float)Math.Tan(30 * Math.PI / 180);
            float[] before = (float[])g.Data.Clone();
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                if (s.Ocean[i])
                    continue;
                int x = i % w, z = i / w;
                float wx = x * cell, wz = z * cell;
                float fade = MathCore.Smooth(2, 10, channelDistance[i] * cell) * MathCore.Smooth(0, 6, inland[i] * cell) * (1 - s.Beach[i]);
                if (fade <= 0)
                    continue;
                float slope = SlopeOf(before, w, cell, x, z);
                float rock = MathCore.Smooth(rockFrom, rockTo, slope);
                float scree = MathCore.Smooth(screeFrom, screeTo, slope) * (1 - rock);
                float h = before[i];

                // Outcrops: ridged noise gives sharp, aperiodic crests (about 10 m) and cracks (about 3 m).
                float crest = (1 - Math.Abs(2 * MathCore.Fbm(wx / 10, wz / 10, seed + 1, 3) - 1)) - 0.55f;
                float crack = (1 - Math.Abs(2 * MathCore.Fbm(wx / 3, wz / 3, seed + 3, 2) - 1)) - 0.6f;
                float ledges = rock * (1.2f * crest + 0.35f * crack);

                // Scree: positive bumps of loose stone on slopes below the rock.
                float bumps = scree * 0.35f * Math.Max(0, MathCore.Fbm(wx / 1.8f, wz / 1.8f, seed + 4, 2) - 0.45f) * 2;

                // Hummocks: soil undulation at 6, 3 and 1.5 m.
                float hummocks = (1 - rock) * 0.5f * (MathCore.Fbm(wx / 6, wz / 6, seed + 5, 3) - 0.5f);

                g.Data[i] = Math.Max(0.02f, h + fade * amount * (ledges + bumps + hummocks));
            }
        }

        private static float SlopeOf(float[] h, int w, float cell, int x, int z)
        {
            int x0 = Math.Max(0, x - 1), x1 = Math.Min(w - 1, x + 1), z0 = Math.Max(0, z - 1), z1 = Math.Min(w - 1, z + 1);
            float dx = (h[z * w + x1] - h[z * w + x0]) / ((x1 - x0) * cell), dz = (h[z1 * w + x] - h[z0 * w + x]) / ((z1 - z0) * cell);
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private static float[] BeachMask(IslandRecipe r, TerrainState s, float[] inland, CancellationToken cancel)
        {
            int w = s.Height.Width, n = inland.Length;
            var scores = new float[n];
            var sorted = new float[n];
            int edges = 0;
            ulong seed = MathCore.Stage(r.seed, "Beach");
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                if (s.Ocean[i] || inland[i] != 1)
                    continue;
                scores[i] = MathCore.Fbm(i % w * s.Height.CellSize / (r.worldSize * 0.22f), i / w * s.Height.CellSize / (r.worldSize * 0.22f), seed, 2);
                if (s.Ocean[i - 1])
                    sorted[edges++] = scores[i];
                if (s.Ocean[i + 1])
                    sorted[edges++] = scores[i];
                if (s.Ocean[i - w])
                    sorted[edges++] = scores[i];
                if (s.Ocean[i + w])
                    sorted[edges++] = scores[i];
            }

            Array.Sort(sorted, 0, edges);
            float cut = edges == 0 || r.beachShare <= 0 ? -1 : sorted[Math.Min(edges - 1, (int)(edges * r.beachShare))];
            var sandy = new bool[n];
            var rocky = new bool[n];
            for (int i = 0; i < n; i++)
            {
                bool coast = !s.Ocean[i] && inland[i] == 1;
                sandy[i] = coast && scores[i] <= cut;
                rocky[i] = coast && scores[i] > cut;
            }

            float[] sandDistance = DistanceTo(sandy, w, w, cancel);
            float[] rockDistance = DistanceTo(rocky, w, w, cancel);
            var result = new float[n];
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                result[i] = s.Ocean[i] || r.beachShare == 0 ? 0 : r.beachShare == 1 ? 1 : MathCore.Smooth(-6, 6, (rockDistance[i] - sandDistance[i]) * s.Height.CellSize);
            }

            return result;
        }

        // Min-plus relaxation: only lower steep land, never move the coastline or seabed.
        private static void ClampSlopes(Grid g, bool[] ocean, float slope, bool[] rock, CancellationToken cancel)
        {
            int n = g.Data.Length, w = g.Width;
            float cell = g.CellSize * slope;
            bool changed;
            do
            {
                changed = false;
                for (int pass = 0; pass < 2; pass++)
                    for (int k = 0; k < n; k++)
                    {
                        MathCore.Check(cancel, k);
                        int i = pass == 0 ? k : n - 1 - k;
                        if (ocean[i])
                            continue;
                        int x = i % w, z = i / w;
                        float limit = g.Data[i];
                        for (int dz = -1; dz <= 1; dz++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int xx = x + dx, zz = z + dz;
                                if (xx < 0 || zz < 0 || xx >= w || zz >= g.Height)
                                    continue;
                                int j = zz * w + xx;
                                limit = Math.Min(limit, Math.Max(0, g.Data[j]) + cell * (dx != 0 && dz != 0 ? 1.41421356f : 1));
                            }

                        if (limit < g.Data[i] - 0.0001f)
                        {
                            g.Data[i] = limit;
                            if (rock != null)
                                rock[i] = true;
                            changed = true;
                        }
                    }
            }
            while (changed);
        }
    }
}
