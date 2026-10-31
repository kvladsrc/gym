using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using Procedural.Generation;

namespace Procedural.Tests
{
    /// <summary>
    /// Fingerprints of whole results for behaviour-preserving refactors: run before and
    /// after, compare the printed lines. Report timings are excluded.
    /// </summary>
    public class GoldenHashTests
    {
        private static IEnumerable<(string, IslandRecipe, BakeMode)> Cases()
        {
            yield return ("tiny-7", Tiny(7), BakeMode.Bake);
            yield return ("tiny-13", Tiny(13), BakeMode.Bake);
            yield return ("small-1", new IslandRecipe { seed = 1 }, BakeMode.Bake);
            yield return ("small-2-preview", new IslandRecipe { seed = 2 }, BakeMode.Preview);
        }

        private static IslandRecipe Tiny(int seed)
        {
            return new IslandRecipe
            {
                seed = seed, worldSize = 256, simResolution = 64, heightmapResolution = 129,
                peakHeight = 45, seaDepth = 12, maxRiverWidth = 6, maxRiverDepth = 1, riverDensity = 2, lakes = 2
            };
        }

        [Test, Explicit]
        public void PrintGoldenHashes()
        {
            var lines = new StringBuilder();
            foreach (var (name, recipe, mode) in Cases())
            {
                IslandResult r = IslandPipeline.Run(recipe, mode, null, null, CancellationToken.None);
                lines.AppendLine(name + " " + Fingerprint(r));
            }

            string path = Environment.GetEnvironmentVariable("GOLDEN_OUT");
            if (!string.IsNullOrEmpty(path))
                File.WriteAllText(path, lines.ToString());
            Console.WriteLine(lines.ToString());
        }

        internal static string Fingerprint(IslandResult r)
        {
            var h = new Hasher();
            h.Grid(r.Height);
            h.Grid(r.Discharge);
            h.Grid(r.Wetness);
            h.Grid(r.Moisture);
            foreach (Grid g in r.Surface) h.Grid(g);
            foreach (int id in r.LakeId) h.Int(id);
            foreach (Lake l in r.Lakes) { h.Float(l.Level); h.Int(l.Cells); h.Int(l.OutletX); h.Int(l.OutletZ); }
            foreach (RiverSegment s in r.Rivers)
            {
                h.Int(s.Order); h.Int(s.Downstream);
                h.Floats(s.X); h.Floats(s.Z); h.Floats(s.Surface); h.Floats(s.Bed); h.Floats(s.Width);
            }
            foreach (PlantInstance p in r.Plants)
            {
                h.Int(p.Species); h.Float(p.X); h.Float(p.Z); h.Float(p.Y); h.Float(p.Scale); h.Float(p.Yaw); h.Float(p.Tilt);
            }
            foreach (Grid g in r.DetailCoverage) h.Grid(g);
            string report = Regex.Replace(r.ReportJson, "\"ms\":[-+0-9.eE]+", "\"ms\":0");
            return h.Value.ToString("x16") + " report " + Hasher.Of(report).ToString("x16") + " plants " + r.Plants.Count + " passed " + r.Passed;
        }

        private sealed class Hasher
        {
            internal ulong Value = 1469598103934665603UL;
            internal void Byte(byte b) { Value = (Value ^ b) * 1099511628211UL; }
            internal void Int(int v) { foreach (byte b in BitConverter.GetBytes(v)) Byte(b); }
            internal void Float(float v) { foreach (byte b in BitConverter.GetBytes(v)) Byte(b); }
            internal void Floats(float[] a) { foreach (float v in a) Float(v); }
            internal void Grid(Grid g) { Int(g.Width); Int(g.Height); Float(g.CellSize); Floats(g.Data); }
            internal static ulong Of(string s) { var h = new Hasher(); foreach (byte b in Encoding.UTF8.GetBytes(s)) h.Byte(b); return h.Value; }
        }
    }
}
