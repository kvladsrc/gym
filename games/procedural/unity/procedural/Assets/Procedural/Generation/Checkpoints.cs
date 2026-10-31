using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace Procedural.Generation
{
    internal static class Checkpoints
    {
        // Bump this when changing algorithms, constants or the binary schema.
        private const int Version = 6;
        internal static ulong Key(ulong previous, IslandRecipe r, BakeMode mode, int stage)
        {
            using (var ms = new MemoryStream())
            {
                using (var b = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
                {
                    b.Write(Version);
                    // Any rebuild of the core invalidates checkpoints, even when Version was not bumped.
                    b.Write(typeof(Checkpoints).Assembly.ManifestModule.ModuleVersionId.ToByteArray());
                    b.Write(previous);
                    b.Write(stage);
                    if (stage == 0)
                    {
                        b.Write(r.seed);
                        b.Write(r.worldSize);
                        b.Write(r.simResolution);
                        b.Write(r.landFraction);
                        b.Write(r.coastRoughness);
                        b.Write(r.elongation);
                        b.Write(r.orientationDegrees);
                        b.Write(r.massifs);
                        b.Write(r.ruggedness);
                        b.Write(r.peakHeight);
                        b.Write(r.beachWidth);
                    }

                    if (stage == 1)
                    {
                        b.Write((int)r.relief);
                        b.Write(r.peakHeight);
                        b.Write((int)mode);
                        b.Write(r.channelHeadArea);
                        b.Write(r.implicitTimeStep);
                    }

                    if (stage == 2)
                    {
                        b.Write(r.heightmapResolution);
                        b.Write(r.microRelief);
                        b.Write(r.seaDepth);
                        b.Write(r.beachShare);
                        b.Write(r.beachWidth);
                    }
                }

                return Hash(ms.ToArray());
            }
        }

        private static ulong Hash(byte[] data, CancellationToken cancel = default)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                for (int i = 0; i < data.Length; i++)
                {
                    MathCore.Check(cancel, i);
                    h = (h ^ data[i]) * 1099511628211UL;
                }

                return h;
            }
        }

        private static string PathFor(string dir, int stage, ulong key)
        {
            return Path.Combine(dir, (stage + 1).ToString(CultureInfo.InvariantCulture) + "-" + key.ToString("x16", CultureInfo.InvariantCulture) + ".bin");
        }

        internal static bool Load(string dir, int stage, ulong key, TerrainState s, CancellationToken cancel)
        {
            if (dir == null)
                return false;
            string path = PathFor(dir, stage, key);
            if (!File.Exists(path))
                return false;
            try
            {
                cancel.ThrowIfCancellationRequested();
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length < 28)
                    return false;
                using (var ms = new MemoryStream(bytes))
                using (var b = new BinaryReader(ms))
                {
                    if (b.ReadInt32() != Version || b.ReadUInt64() != key || b.ReadInt32() != stage)
                        return false;
                    ulong checksum = b.ReadUInt64();
                    var payload = new byte[bytes.Length - 24];
                    Array.Copy(bytes, 24, payload, 0, payload.Length);
                    if (checksum != Hash(payload, cancel))
                        return false;
                    Grid a = ReadGrid(b, cancel), extra = stage == 0 ? ReadGrid(b, cancel) : null;
                    int iterations = b.ReadInt32();
                    int stableIterations = b.ReadInt32();
                    float changed = b.ReadSingle();
                    float changedAll = b.ReadSingle();
                    float networkChanged = b.ReadSingle();
                    float heightChange = b.ReadSingle();
                    float uphill = b.ReadSingle();
                    float nearTies = b.ReadSingle();
                    float residual = b.ReadSingle();
                    var ocean = new bool[a.Data.Length];
                    for (int i = 0; i < ocean.Length; i++)
                    {
                        MathCore.Check(cancel, i);
                        ocean[i] = b.ReadBoolean();
                    }

                    bool[] rock = null;
                    if (stage == 2)
                    {
                        rock = new bool[a.Data.Length];
                        for (int i = 0; i < rock.Length; i++)
                        {
                            MathCore.Check(cancel, i);
                            rock[i] = b.ReadBoolean();
                        }
                    }

                    if (stage == 0)
                    {
                        s.Mask = a;
                        s.Uplift = extra;
                    }
                    else if (stage == 1)
                        s.Relief = a;
                    else
                    {
                        s.Height = a;
                        s.Rock = rock;
                        s.Beach = new float[a.Data.Length];
                        for (int i = 0; i < s.Beach.Length; i++)
                        {
                            MathCore.Check(cancel, i);
                            s.Beach[i] = b.ReadSingle();
                        }
                    }

                    if (ms.Position != ms.Length)
                        return false;
                    s.Ocean = ocean;
                    s.Iterations = iterations;
                    s.StableIterations = stableIterations;
                    s.Changed = changed;
                    s.ChangedAll = changedAll;
                    s.NetworkChanged = networkChanged;
                    s.RelativeHeightChange = heightChange;
                    s.FlipsFromUphill = uphill;
                    s.FlipsFromNearTies = nearTies;
                    s.FixedPointResidual = residual;
                    return true;
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        internal static void Save(string dir, int stage, ulong key, TerrainState s, CancellationToken cancel)
        {
            if (dir == null)
                return;
            Directory.CreateDirectory(dir);
            byte[] payload;
            using (var ms = new MemoryStream())
            {
                using (var b = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
                {
                    WriteGrid(b, stage == 0 ? s.Mask : stage == 1 ? s.Relief : s.Height, cancel);
                    if (stage == 0)
                        WriteGrid(b, s.Uplift, cancel);
                    b.Write(s.Iterations);
                    b.Write(s.StableIterations);
                    b.Write(s.Changed);
                    b.Write(s.ChangedAll);
                    b.Write(s.NetworkChanged);
                    b.Write(s.RelativeHeightChange);
                    b.Write(s.FlipsFromUphill);
                    b.Write(s.FlipsFromNearTies);
                    b.Write(s.FixedPointResidual);
                    for (int i = 0; i < s.Ocean.Length; i++)
                    {
                        MathCore.Check(cancel, i);
                        b.Write(s.Ocean[i]);
                    }

                    if (stage == 2)
                        for (int i = 0; i < s.Rock.Length; i++)
                        {
                            MathCore.Check(cancel, i);
                            b.Write(s.Rock[i]);
                        }

                    if (stage == 2)
                        for (int i = 0; i < s.Beach.Length; i++)
                        {
                            MathCore.Check(cancel, i);
                            b.Write(s.Beach[i]);
                        }
                }

                payload = ms.ToArray();
            }

            string path = PathFor(dir, stage, key), temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = File.Create(temp))
                using (var b = new BinaryWriter(stream))
                {
                    b.Write(Version);
                    b.Write(key);
                    b.Write(stage);
                    b.Write(Hash(payload, cancel));
                    b.Write(payload);
                }

                cancel.ThrowIfCancellationRequested();
                // Concurrent identical runs may have already published the same deterministic file.
                try
                {
                    File.Move(temp, path);
                }
                catch (IOException)
                {
                    if (!File.Exists(path))
                        throw;
                    File.Copy(temp, path, true);
                }
            }
            finally
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }

        private static void WriteGrid(BinaryWriter b, Grid g, CancellationToken cancel)
        {
            b.Write(g.Width);
            b.Write(g.Height);
            b.Write(g.CellSize);
            for (int i = 0; i < g.Data.Length; i++)
            {
                MathCore.Check(cancel, i);
                b.Write(g.Data[i]);
            }
        }

        private static Grid ReadGrid(BinaryReader b, CancellationToken cancel)
        {
            int w = b.ReadInt32(), h = b.ReadInt32();
            float cell = b.ReadSingle();
            if (w < 2 || h < 2 || w > 8193 || h > 8193 || (long)w * h * 4 > b.BaseStream.Length - b.BaseStream.Position)
                throw new InvalidDataException("Invalid checkpoint dimensions.");
            var g = new Grid(w, h, cell);
            for (int i = 0; i < g.Data.Length; i++)
            {
                MathCore.Check(cancel, i);
                g.Data[i] = b.ReadSingle();
                if (float.IsNaN(g.Data[i]) || float.IsInfinity(g.Data[i]))
                    throw new InvalidDataException("Invalid checkpoint sample.");
            }

            return g;
        }
    }
}
