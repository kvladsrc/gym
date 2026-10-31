using System;
using System.Collections.Generic;
using System.Threading;

namespace Procedural.Generation
{
    internal static partial class WaterStages
    {
        // 0.1 mm is below the 1 mm physical validation tolerance, but above
        // float rounding at supported island heights on both Mono and CoreCLR.
        internal const double LakeLevelTolerance = 0.0001;
        private const int MinimumDamBasin = 6;
        internal static bool SameLakeLevel(float a, float b)
        {
            return Math.Abs((double)a - b) <= LakeLevelTolerance;
        }

        internal static bool Submerged(float level, float bed)
        {
            return (double)level - bed > LakeLevelTolerance;
        }

        internal static void Lakes(IslandRecipe r, TerrainState s, WaterState water, CancellationToken cancel, BakeMode mode = BakeMode.Bake)
        {
            water.DamFloodBudget = mode == BakeMode.Preview ? 4 : 16;
            Grid g = s.Height;
            int n = g.Data.Length, w = g.Width;
            float cell = g.CellSize;
            InitRimFloor(water, n, cancel);
            Drainage d = Hydrology.Route(g, s.Ocean, cancel);
            FillIncidentalPits(g, s.Ocean, d, n, cancel);
            bool[] channel = BuildChannelMask(d, s.Ocean, n, cell, cancel);
            float[] coast = TerrainStages.DistanceTo(s.Ocean, w, w, cancel);
            float[] upstreamChannel = UpstreamChannelLengths(d, channel, n, cancel);
            int[] candidates = PickLakeCandidates(r, g, d, channel, coast, upstreamChannel, cell, water, cancel, out int count, out float siteSpacing);
            SelectLakeSites(r, g, s.Ocean, d, candidates, count, siteSpacing, water, cancel);

            d = Hydrology.Route(g, s.Ocean, cancel);
            water.LakeId = new int[n];
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                water.LakeId[i] = -1;
            }

            var visited = new bool[n];
            var queue = new int[n];
            var keepRim = new int[n];
            for (int root = 0; root < n; root++)
            {
                MathCore.Check(cancel, root);
                if (visited[root] || s.Ocean[root] || !Submerged(d.Fill[root], g.Data[root]))
                    continue;
                if (!FloodCandidateBasin(r, g, s.Ocean, d, water, queue, visited, root, w, cancel, out int tail, out float level))
                    continue;
                LabelLakeBasin(g, s.Ocean, d, water, queue, tail, level, w, cell, visited, keepRim, cancel);
            }

            FinishRimFloors(g, water, n, w, cancel);
            water.Routing = Hydrology.Route(g, s.Ocean, cancel);
        }

        private static void InitRimFloor(WaterState water, int n, CancellationToken cancel)
        {
            water.RimFloor = new float[n];
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                water.RimFloor[i] = float.MinValue;
            }
        }

        private static void FillIncidentalPits(Grid g, bool[] ocean, Drainage d, int n, CancellationToken cancel)
        {
            // Remove incidental interpolation/detail pits before introducing deliberate water bodies.
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                if (!ocean[i])
                    g.Data[i] = d.Fill[i];
            }
        }

        private static bool[] BuildChannelMask(Drainage d, bool[] ocean, int n, float cell, CancellationToken cancel)
        {
            var channel = new bool[n];
            float minQ = d.Stack.Length * cell * cell * 0.0008f;
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                channel[i] = !ocean[i] && d.Discharge[i] >= minQ;
            }

            return channel;
        }

        private static float[] UpstreamChannelLengths(Drainage d, bool[] channel, int n, CancellationToken cancel)
        {
            var upstreamChannel = new float[n];
            for (int k = d.Stack.Length - 1; k >= 0; k--)
            {
                MathCore.Check(cancel, k);
                int i = d.Stack[k];
                if (channel[i])
                    upstreamChannel[d.Receiver[i]] = Math.Max(upstreamChannel[d.Receiver[i]], upstreamChannel[i] + d.Distance[i]);
            }

            return upstreamChannel;
        }

        private static int[] PickLakeCandidates(IslandRecipe r, Grid g, Drainage d, bool[] channel, float[] coast, float[] upstreamChannel, float cell, WaterState water, CancellationToken cancel, out int count, out float siteSpacing)
        {
            int n = g.Data.Length;
            var candidates = new int[n];
            var score = new float[n];
            count = 0;
            siteSpacing = Math.Max(7.5f * cell, r.worldSize * 0.054f);
            float minimumCoastDistance = Math.Max(siteSpacing * (2f / 3), r.worldSize * 0.06f);
            float minimumInflowArea = Math.Min(r.channelHeadArea, r.worldSize * r.worldSize * 0.005f);
            for (int i = 0; i < n; i++)
            {
                MathCore.Check(cancel, i);
                if (!channel[i] || coast[i] * cell < minimumCoastDistance || upstreamChannel[i] < r.worldSize * 0.12f || d.Discharge[i] < minimumInflowArea || g.Data[i] < 2)
                    continue;
                candidates[count++] = i;
                float valleyWidth = DamSpan(g, d, i, 2, Math.Max(r.worldSize * 0.12f, 20 * cell));
                score[i] = valleyWidth / r.worldSize * 0.4f - coast[i] * cell / r.worldSize * 0.25f - upstreamChannel[i] / r.worldSize;
            }

            water.LakeCandidates = count;
            MathCore.SortIndices(candidates, count, score, false, cancel);
            return candidates;
        }

        private static void SelectLakeSites(IslandRecipe r, Grid g, bool[] ocean, Drainage d, int[] candidates, int count, float siteSpacing, WaterState water, CancellationToken cancel)
        {
            int w = g.Width;
            float cell = g.CellSize;
            var sites = new List<int>();
            var reserved = new HashSet<int>();
            ulong seed = MathCore.Stage(r.seed, "Lakes");
            for (int k = 0; k < count && sites.Count < r.lakes && water.DamFloodChecks < water.DamFloodBudget; k++)
            {
                MathCore.Check(cancel, k);
                int c = candidates[k], x = c % w, z = c / w;
                bool near = false;
                for (int j = 0; j < sites.Count; j++)
                {
                    float dx = (sites[j] % w - x) * cell, dz = (sites[j] / w - z) * cell;
                    if (dx * dx + dz * dz < siteSpacing * siteSpacing)
                        near = true;
                }

                if (near)
                    continue;
                if (!BuildDam(r, g, ocean, d, c, seed, cancel, reserved, water))
                    continue;
                sites.Add(c);
            }
        }

        private static bool FloodCandidateBasin(IslandRecipe r, Grid g, bool[] ocean, Drainage d, WaterState water, int[] queue, bool[] visited, int root, int w, CancellationToken cancel, out int tail, out float level)
        {
            int head = 0;
            tail = 1;
            queue[0] = root;
            visited[root] = true;
            level = d.Fill[root];
            float depth = 0;
            while (head < tail)
            {
                MathCore.Check(cancel, head);
                int i = queue[head++], x = i % w, z = i / w;
                depth = Math.Max(depth, level - g.Data[i]);
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, zz = z + dz;
                        if (xx < 0 || zz < 0 || xx >= w || zz >= w)
                            continue;
                        int j = zz * w + xx;
                        if (!visited[j] && !ocean[j] && SameLakeLevel(d.Fill[j], level) && Submerged(level, g.Data[j]))
                        {
                            visited[j] = true;
                            queue[tail++] = j;
                        }
                    }
            }

            if (tail < 6 || depth < 0.75f || water.Lakes.Count >= r.lakes || !HasLakeInflow(r, g, d, queue, tail))
            {
                for (int k = 0; k < tail; k++)
                    g.Data[queue[k]] = level;
                return false;
            }

            return true;
        }

        private static void LabelLakeBasin(Grid g, bool[] ocean, Drainage d, WaterState water, int[] queue, int tail, float level, int w, float cell, bool[] visited, int[] keepRim, CancellationToken cancel)
        {
            int id = water.Lakes.Count, wetCount = tail;
            for (int k = 0; k < tail; k++)
                water.LakeId[queue[k]] = id;
            // Include the level spill/rim path in the basin. Several wet cells may
            // point at the SAME rim cell; that is one outlet, not several lakes.
            tail = IncludeSpillRim(g, ocean, d, water, queue, tail, level, id, visited);
            int outlet = FindSpillOutlet(d, water, queue, tail, id);
            if (outlet < 0)
                throw new InvalidOperationException("Flooded basin has no spill.");
            int spillReceiver = d.Receiver[outlet];
            // Keep one physical spill path; a 5 cm berm closes competing equal-level
            // rim saddles. This edits terrain, never adds an epsilon to flood levels.
            KeepPhysicalSpillPath(d, water, queue, wetCount, keepRim, id, outlet, cancel);
            int cells = TrimCompetingRimCells(g, water, queue, wetCount, tail, keepRim, id, level);
            RaiseShoreBerms(g, water, queue, tail, w, level, id, outlet, spillReceiver);
            water.Lakes.Add(new Lake { Id = id, Cells = cells, Area = cells * cell * cell, Level = level, OutletX = outlet % w, OutletZ = outlet / w });
        }

        private static int IncludeSpillRim(Grid g, bool[] ocean, Drainage d, WaterState water, int[] queue, int tail, float level, int id, bool[] visited)
        {
            for (int k = 0; k < tail; k++)
            {
                int rec = d.Receiver[queue[k]];
                if (!ocean[rec] && water.LakeId[rec] < 0 && SameLakeLevel(d.Fill[rec], level) && SameLakeLevel(g.Data[rec], level))
                {
                    g.Data[rec] = level; // Canonical physical spill/rim, independent of register precision.
                    water.LakeId[rec] = id;
                    visited[rec] = true;
                    queue[tail++] = rec;
                }
            }

            return tail;
        }

        private static int FindSpillOutlet(Drainage d, WaterState water, int[] queue, int tail, int id)
        {
            int outlet = -1;
            for (int k = 0; k < tail; k++)
            {
                int i = queue[k], rec = d.Receiver[i];
                if (water.LakeId[rec] != id && (outlet < 0 || i < outlet))
                    outlet = i;
            }

            return outlet;
        }

        private static void KeepPhysicalSpillPath(Drainage d, WaterState water, int[] queue, int wetCount, int[] keepRim, int id, int outlet, CancellationToken cancel)
        {
            for (int k = 0; k < wetCount; k++)
            {
                int i = queue[k];
                int steps = 0;
                while (water.LakeId[d.Receiver[i]] == id)
                {
                    MathCore.Check(cancel, steps++);
                    i = d.Receiver[i];
                }

                if (i != outlet)
                    continue;
                i = queue[k];
                while (water.LakeId[i] == id)
                {
                    keepRim[i] = id + 1;
                    i = d.Receiver[i];
                }

                break;
            }
        }

        private static int TrimCompetingRimCells(Grid g, WaterState water, int[] queue, int wetCount, int tail, int[] keepRim, int id, float level)
        {
            int cells = wetCount;
            for (int k = wetCount; k < tail; k++)
            {
                int i = queue[k];
                if (keepRim[i] == id + 1)
                    cells++;
                else
                {
                    water.LakeId[i] = -1;
                    g.Data[i] = level + 0.05f;
                    water.RimFloor[i] = Math.Max(water.RimFloor[i], level + 0.05f);
                }
            }

            return cells;
        }

        private static void RaiseShoreBerms(Grid g, WaterState water, int[] queue, int tail, int w, float level, int id, int outlet, int spillReceiver)
        {
            for (int k = 0; k < tail; k++)
            {
                int i = queue[k];
                if (water.LakeId[i] != id)
                    continue;
                int x = i % w, z = i / w;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, zz = z + dz;
                        if (xx < 0 || zz < 0 || xx >= w || zz >= w)
                            continue;
                        int j = zz * w + xx;
                        if (water.LakeId[j] < 0 && (double)g.Data[j] <= (double)level + LakeLevelTolerance && !(i == outlet && Submerged(level, g.Data[j])) && j != spillReceiver)
                        {
                            g.Data[j] = level + 0.05f;
                            water.RimFloor[j] = Math.Max(water.RimFloor[j], level + 0.05f);
                        }
                    }
            }
        }

        private static void FinishRimFloors(Grid g, WaterState water, int n, int w, CancellationToken cancel)
        {
            for (int i = 0; i < n; i++)
                if (water.LakeId[i] >= 0)
                {
                    MathCore.Check(cancel, i);
                    int x = i % w, z = i / w;
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx, zz = z + dz;
                            if (xx >= 0 && zz >= 0 && xx < w && zz < w && g[xx, zz] >= water.Lakes[water.LakeId[i]].Level)
                                water.RimFloor[zz * w + xx] = Math.Max(water.RimFloor[zz * w + xx], water.Lakes[water.LakeId[i]].Level + 0.05f);
                        }
                }
        }

        private static bool HasLakeInflow(IslandRecipe recipe, Grid height, Drainage drainage, int[] cells, int count)
        {
            // A curved dam can also impound a small side pocket. Qualify every
            // labelled reservoir, rather than assuming every flooded pocket is
            // the basin that passed the candidate check.
            var body = new HashSet<int>();
            for (int k = 0; k < count; k++)
                body.Add(cells[k]);
            int inlet = -1;
            for (int k = 0; k < count; k++)
                for (int donor = drainage.DonorHead[cells[k]]; donor >= 0; donor = drainage.DonorNext[donor])
                    if (!body.Contains(donor) && (inlet < 0 || drainage.Discharge[donor] > drainage.Discharge[inlet]))
                        inlet = donor;
            if (inlet < 0 || drainage.Discharge[inlet] < recipe.channelHeadArea)
                return false;
            float length = 0;
            int upstream = inlet;
            while (upstream >= 0 && !body.Contains(upstream) && !Submerged(drainage.Fill[upstream], height.Data[upstream]))
            {
                length += drainage.Distance[upstream];
                int next = -1;
                for (int donor = drainage.DonorHead[upstream]; donor >= 0; donor = drainage.DonorNext[donor])
                    if (next < 0 || drainage.Discharge[donor] > drainage.Discharge[next])
                        next = donor;
                upstream = next;
            }

            return length >= recipe.worldSize * 0.12f;
        }

        private static void ValleyAxis(Grid g, Drainage d, int site, out float tx, out float tz)
        {
            int upstream = site;
            for (int k = 0; k < 2; k++)
            {
                int donor = -1;
                for (int j = d.DonorHead[upstream]; j >= 0; j = d.DonorNext[j])
                    if (donor < 0 || d.Discharge[j] > d.Discharge[donor])
                        donor = j;
                if (donor < 0)
                    break;
                upstream = donor;
            }

            int downstream = site;
            for (int k = 0; k < 2 && d.Receiver[downstream] >= 0; k++)
                downstream = d.Receiver[downstream];
            tx = downstream % g.Width - upstream % g.Width;
            tz = downstream / g.Width - upstream / g.Width;
            float length = (float)Norm(tx, tz);
            if (length == 0)
                length = 1;
            tx /= length;
            tz /= length;
        }

        private static float DamSpan(Grid g, Drainage d, int site, float rise, float maximum)
        {
            return DamAxis(g, d, site, rise, maximum, out _, out _, out _, out _);
        }

        private static float DamAxis(Grid g, Drainage d, int site, float rise, float maximum, out float tx, out float tz, out float left, out float right)
        {
            ValleyAxis(g, d, site, out float baseX, out float baseZ);
            float best = float.PositiveInfinity;
            tx = baseX;
            tz = baseZ;
            left = right = maximum;
            // Curved raster valleys need a local cross-section, rather than the
            // chord of several bends. Limit rotation to 45 degrees from the flow.
            for (int angle = -3; angle <= 3; angle++)
            {
                float cs = (float)Math.Cos(angle * Math.PI / 12), sn = (float)Math.Sin(angle * Math.PI / 12);
                float vx = baseX * cs - baseZ * sn, vz = baseX * sn + baseZ * cs;
                float span = 0, firstBank = 0, secondBank = 0;
                for (int side = -1; side <= 1; side += 2)
                {
                    float reach;
                    for (reach = g.CellSize; reach <= maximum; reach += g.CellSize)
                    {
                        float x = site % g.Width - side * vz * reach / g.CellSize;
                        float z = site / g.Width + side * vx * reach / g.CellSize;
                        if (MathCore.Sample(g, x, z) > g.Data[site] + rise + 0.8f)
                            break;
                    }

                    if (reach > maximum)
                    {
                        span = float.PositiveInfinity;
                        break;
                    }

                    span += reach;
                    if (side < 0)
                        firstBank = reach;
                    else
                        secondBank = reach;
                }

                if (span < best)
                {
                    best = span;
                    tx = vx;
                    tz = vz;
                    left = firstBank;
                    right = secondBank;
                }
            }

            return best;
        }

        internal static bool BuildDam(IslandRecipe r, Grid g, bool[] ocean, Drainage d, int site, ulong seed, CancellationToken cancel, HashSet<int> reserved = null, WaterState diagnostics = null)
        {
            const float initialRise = 2;
            for (float rise = initialRise; rise <= Math.Max(6, 2.5f * g.CellSize) && (diagnostics == null || diagnostics.DamFloodChecks < diagnostics.DamFloodBudget); rise += 1)
                if (TryDam(r, g, ocean, d, site, seed, rise, cancel, reserved, diagnostics))
                    return true;
            return false;
        }

        /// <summary>Geometry and edit bookkeeping for one dam body.</summary>
        private sealed class DamBody
        {
            internal readonly IslandRecipe Recipe;
            internal readonly Grid Grid;
            internal readonly bool[] Ocean;
            internal readonly Drainage Drainage;
            internal readonly HashSet<int> Reserved;
            internal readonly List<int> Edited;
            internal readonly List<float> Previous;
            internal readonly CancellationToken Cancel;
            internal readonly int Site, SiteX, SiteZ, Radius;
            internal readonly float Cell, Span, BaseHeight, Level, HalfThickness, Tx, Tz, Left, Right;
            internal readonly ulong Seed;

            internal DamBody(IslandRecipe recipe, Grid grid, bool[] ocean, Drainage drainage, int site, ulong seed, float cell, float span, float tx, float tz, float left, float right, float rise, HashSet<int> reserved, List<int> edited, List<float> previous, CancellationToken cancel)
            {
                Recipe = recipe;
                Grid = grid;
                Ocean = ocean;
                Drainage = drainage;
                Site = site;
                Seed = seed;
                Cell = cell;
                Span = span;
                Tx = tx;
                Tz = tz;
                Left = left;
                Right = right;
                BaseHeight = grid.Data[site];
                Level = BaseHeight + rise;
                HalfThickness = Math.Max(cell * 2, rise * 4);
                Radius = (int)Math.Ceiling(Math.Max(Math.Max(left, right) + HalfThickness, span * 1.75f) / cell);
                SiteX = site % grid.Width;
                SiteZ = site / grid.Width;
                Reserved = reserved;
                Edited = edited;
                Previous = previous;
                Cancel = cancel;
            }
        }

        private static bool TryDam(IslandRecipe r, Grid g, bool[] ocean, Drainage d, int site, ulong seed, float rise, CancellationToken cancel, HashSet<int> reserved, WaterState diagnostics)
        {
            if (!BeginDamAttempt(site, reserved, diagnostics))
                return false;

            float cell = g.CellSize;
            float span = DamAxis(g, d, site, rise, Math.Max(r.worldSize * 0.12f, 20 * cell), out float tx, out float tz, out float left, out float right);
            if (float.IsPositiveInfinity(span))
            {
                if (diagnostics != null)
                    diagnostics.DamNarrow++;
                return false;
            }

            var edited = new List<int>();
            var previous = new List<float>();
            var body = new DamBody(r, g, ocean, d, site, seed, cell, span, tx, tz, left, right, rise, reserved, edited, previous, cancel);
            RaiseDamBody(body);

            // A local contour flood rejects open or oversized sites before the full
            // Priority-Flood labels the final upstream valley. No lake bed is dug.
            float level = body.Level - Math.Min(0.4f, rise * 0.15f);
            int root = FindContourRoot(g, d, site, level);
            var seen = new HashSet<int>();
            var queue = new Queue<int>();
            if (root >= 0 && g.Data[root] < level)
            {
                queue.Enqueue(root);
                seen.Add(root);
            }

            int limit = Math.Max(64, (int)(d.Stack.Length * 0.08f));
            bool open = FloodContour(g, ocean, level, queue, seen, limit, cancel);
            bool overlaps = HasReservedOverlap(reserved, seen, edited);
            bool adequateInflow = false;
            if (!open && !overlaps && seen.Count >= MinimumDamBasin && seen.Count <= limit)
            {
                if (diagnostics != null)
                    diagnostics.DamFloodChecks++;
                adequateInflow = ValidateFloodedBasin(r, g, ocean, root, reserved, edited, seen, queue, ref overlaps, out Drainage flooded, out int inlet, cancel);
                if (!overlaps && adequateInflow && seen.Count >= MinimumDamBasin && seen.Count <= limit)
                {
                    ReserveBasin(r, reserved, seen, flooded, inlet);
                    return true;
                }
            }

            RecordDamFailure(diagnostics, open, overlaps, adequateInflow, seen.Count, limit);
            for (int k = 0; k < edited.Count; k++)
                g.Data[edited[k]] = previous[k];
            return false;
        }

        private static bool BeginDamAttempt(int site, HashSet<int> reserved, WaterState diagnostics)
        {
            if (diagnostics != null)
                diagnostics.DamAttempts++;
            if (reserved == null || !reserved.Contains(site))
                return true;
            if (diagnostics != null)
                diagnostics.DamOverlap++;
            return false;
        }

        private static void RaiseDamBody(DamBody body)
        {
            Grid g = body.Grid;
            bool[] ocean = body.Ocean;
            Drainage d = body.Drainage;
            for (int z = Math.Max(1, body.SiteZ - body.Radius); z <= Math.Min(g.Height - 2, body.SiteZ + body.Radius); z++)
                for (int x = Math.Max(1, body.SiteX - body.Radius); x <= Math.Min(g.Width - 2, body.SiteX + body.Radius); x++)
                {
                    int i = z * g.Width + x;
                    MathCore.Check(body.Cancel, i);
                    if (ocean[i] || (body.Reserved != null && body.Reserved.Contains(i)))
                        continue;
                    float dx = (x - body.SiteX) * body.Cell, dz = (z - body.SiteZ) * body.Cell;
                    float original = g.Data[i], target = original;
                    // Low, smooth depositional hummocks interrupt straight contour
                    // shores on planar valley sides. Raise only; no basin is dug.
                    // They participate in both independent floods and site rejection.
                    float shoreBand = 1 - MathCore.Smooth(0.2f, 1.2f, Math.Abs(original - body.Level));
                    if (shoreBand > 0)
                    {
                        float reach = (float)Math.Sqrt(dx * dx + dz * dz);
                        float local = 1 - MathCore.Smooth(body.Span * 0.75f, body.Span * 1.75f, reach);
                        float noise = MathCore.Noise(x * body.Cell / 17, z * body.Cell / 17, body.Seed + 73);
                        float channelProtection = 1 - MathCore.Smooth(body.Recipe.channelHeadArea * 0.05f, body.Recipe.channelHeadArea * 0.25f, d.Discharge[i]);
                        target += 0.55f * noise * noise * shoreBand * local * channelProtection;
                    }

                    float along = dx * body.Tx + dz * body.Tz, across = -dx * body.Tz + dz * body.Tx;
                    // The bowed crest returns to its bank anchors and central spill.
                    float bankFraction = across / Math.Max(body.Cell, across < 0 ? body.Left : body.Right);
                    float bow = (float)Math.Sin(Math.PI * bankFraction) + 0.3f * (float)Math.Sin(3 * Math.PI * bankFraction);
                    along -= Math.Min(12, body.Span * 0.15f) * bow;
                    if (Math.Abs(along) <= body.HalfThickness && across >= -body.Left && across <= body.Right)
                    {
                        float notch = MathCore.Smooth(0, 2 * body.Cell, Math.Abs(across));
                        float crest = body.Level + notch * (0.65f + 0.25f * MathCore.Noise(x * 0.1f, z * 0.1f, body.Seed));
                        float shape = 1 - MathCore.Smooth(body.Cell * 0.75f, body.HalfThickness, Math.Abs(along));
                        target = Math.Max(target, body.BaseHeight + (crest - body.BaseHeight) * shape);
                    }

                    if (target > original)
                    {
                        body.Edited.Add(i);
                        body.Previous.Add(original);
                        g.Data[i] = target;
                    }
                }
        }

        private static int FindContourRoot(Grid g, Drainage d, int site, float level)
        {
            int root = d.DonorHead[site];
            for (int j = root; j >= 0; j = d.DonorNext[j])
                if (d.Discharge[j] > d.Discharge[root])
                    root = j;
            for (int k = 0; k < 20 && root >= 0 && g.Data[root] >= level; k++)
            {
                int next = -1;
                for (int j = d.DonorHead[root]; j >= 0; j = d.DonorNext[j])
                    if (next < 0 || d.Discharge[j] > d.Discharge[next])
                        next = j;
                root = next;
            }

            return root;
        }

        private static bool FloodContour(Grid g, bool[] ocean, float level, Queue<int> queue, HashSet<int> seen, int limit, CancellationToken cancel)
        {
            bool open = false;
            while (queue.Count > 0 && seen.Count <= limit && !open)
            {
                int i = queue.Dequeue(), x = i % g.Width, z = i / g.Width;
                MathCore.Check(cancel, seen.Count);
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, zz = z + dz;
                        if (xx < 0 || zz < 0 || xx >= g.Width || zz >= g.Height)
                        {
                            open = true;
                            continue;
                        }

                        int j = zz * g.Width + xx;
                        if (g.Data[j] >= level || !seen.Add(j))
                            continue;
                        if (ocean[j])
                            open = true;
                        queue.Enqueue(j);
                    }
            }

            return open;
        }

        private static bool HasReservedOverlap(HashSet<int> reserved, HashSet<int> seen, List<int> edited)
        {
            bool overlaps = false;
            if (reserved != null)
            {
                foreach (int i in seen)
                    overlaps |= reserved.Contains(i);
                foreach (int i in edited)
                    overlaps |= reserved.Contains(i);
            }

            return overlaps;
        }

        private static bool ValidateFloodedBasin(IslandRecipe r, Grid g, bool[] ocean, int root, HashSet<int> reserved, List<int> edited, HashSet<int> seen, Queue<int> queue, ref bool overlaps, out Drainage flooded, out int inlet, CancellationToken cancel)
        {
            float maximumDepth = RefloodBasin(g, ocean, root, seen, queue, cancel, out flooded);
            MeasureBasinInflow(reserved, edited, seen, flooded, ref overlaps, out inlet, out float inflowLength);
            return maximumDepth >= 0.75f && inlet >= 0 && flooded.Discharge[inlet] >= r.channelHeadArea * 1.25f && inflowLength >= r.worldSize * 0.125f;
        }

        private static float RefloodBasin(Grid g, bool[] ocean, int root, HashSet<int> seen, Queue<int> queue, CancellationToken cancel, out Drainage flooded)
        {
            flooded = Hydrology.Route(g, ocean, cancel);
            float actualLevel = root >= 0 ? flooded.Fill[root] : 0;
            seen.Clear();
            queue.Clear();
            if (root >= 0 && Submerged(actualLevel, g.Data[root]))
            {
                seen.Add(root);
                queue.Enqueue(root);
            }

            float maximumDepth = 0;
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % g.Width, z = i / g.Width;
                maximumDepth = Math.Max(maximumDepth, actualLevel - g.Data[i]);
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, zz = z + dz;
                        if (xx < 0 || zz < 0 || xx >= g.Width || zz >= g.Height)
                            continue;
                        int j = zz * g.Width + xx;
                        if (SameLakeLevel(flooded.Fill[j], actualLevel) && Submerged(actualLevel, g.Data[j]) && seen.Add(j))
                            queue.Enqueue(j);
                    }
            }

            return maximumDepth;
        }

        private static void MeasureBasinInflow(HashSet<int> reserved, List<int> edited, HashSet<int> seen, Drainage flooded, ref bool overlaps, out int inlet, out float inflowLength)
        {
            if (reserved != null)
                foreach (int i in edited)
                    overlaps |= reserved.Contains(i);
            inlet = -1;
            foreach (int i in seen)
            {
                if (reserved != null)
                    overlaps |= reserved.Contains(i);
                for (int j = flooded.DonorHead[i]; j >= 0; j = flooded.DonorNext[j])
                    if (!seen.Contains(j) && (inlet < 0 || flooded.Discharge[j] > flooded.Discharge[inlet]))
                        inlet = j;
            }

            inflowLength = 0;
            int upstream = inlet;
            // An existing reservoir breaks a river reach. Do not count its
            // flat flood tree (or its reserved inflow) as this lake's inflow.
            while (upstream >= 0 && !seen.Contains(upstream) && (reserved == null || !reserved.Contains(upstream)))
            {
                inflowLength += flooded.Distance[upstream];
                int next = -1;
                for (int j = flooded.DonorHead[upstream]; j >= 0; j = flooded.DonorNext[j])
                    if (next < 0 || flooded.Discharge[j] > flooded.Discharge[next])
                        next = j;
                upstream = next;
            }
        }

        private static void ReserveBasin(IslandRecipe r, HashSet<int> reserved, HashSet<int> seen, Drainage flooded, int inlet)
        {
            if (reserved == null)
                return;
            reserved.UnionWith(seen);
            float protectedLength = 0;
            int protectedCell = inlet;
            while (protectedCell >= 0 && protectedLength < r.worldSize * 0.12f)
            {
                reserved.Add(protectedCell);
                protectedLength += flooded.Distance[protectedCell];
                int next = -1;
                for (int j = flooded.DonorHead[protectedCell]; j >= 0; j = flooded.DonorNext[j])
                    if (next < 0 || flooded.Discharge[j] > flooded.Discharge[next])
                        next = j;
                protectedCell = next;
            }
        }

        private static void RecordDamFailure(WaterState diagnostics, bool open, bool overlaps, bool adequateInflow, int seenCount, int limit)
        {
            if (diagnostics == null)
                return;
            if (open)
                diagnostics.DamOpen++;
            else if (seenCount < MinimumDamBasin)
                diagnostics.DamSmall++;
            else if (seenCount > limit)
                diagnostics.DamLarge++;
            else if (overlaps)
                diagnostics.DamOverlap++;
            else if (!adequateInflow)
                diagnostics.DamInflow++;
        }
    }
}
