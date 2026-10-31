using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Procedural.Generation;

internal static class ValidationSweep
{
    private static string OutputDirectory()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            string project = Path.Combine(dir.FullName, "games/procedural");
            if (Directory.Exists(Path.Combine(project, "tools/core")))
                return Path.Combine(project, "artifacts/core");
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    internal static int Run(string[] args)
    {
        string output = OutputDirectory();
        Directory.CreateDirectory(output);
        var reports = new List<JsonElement>();
        bool passed = true;
        foreach (IslandRecipe recipe in Recipes(args))
        {
            var clock = Stopwatch.StartNew();
            IslandResult result = IslandPipeline.Run(recipe, args[0] == "PreviewSample" ? BakeMode.Preview : BakeMode.Bake, null, null, CancellationToken.None);
            clock.Stop();
            string stem = recipe.preset + "-" + recipe.seed + "-" + recipe.relief + "-" + recipe.riverDensity.ToString("G", CultureInfo.InvariantCulture) + (recipe.particleErosion ? "-erosion" : "");
            if (args[0] == "PreviewSample")
                stem += "-preview";
            if (recipe.channelHeadArea != 0)
                stem += "-area" + recipe.channelHeadArea.ToString("G", CultureInfo.InvariantCulture);
            if (recipe.peakHeight != 0)
                stem += "-peak" + recipe.peakHeight.ToString("G", CultureInfo.InvariantCulture);
            Dump(result, Path.Combine(output, stem));
            using var json = JsonDocument.Parse(result.ReportJson);
            JsonElement root = json.RootElement;
            JsonElement metrics = root.GetProperty("metrics");
            JsonElement targets = root.GetProperty("targets");
            var row = new
            {
                preset = recipe.preset.ToString(),
                recipe.seed,
                relief = recipe.relief.ToString(),
                recipe.particleErosion,
                peak = result.PeakHeight,
                moistureP10 = metrics.GetProperty("moistureP10").GetDouble(),
                moistureP50 = metrics.GetProperty("moistureP50").GetDouble(),
                moistureP90 = metrics.GetProperty("moistureP90").GetDouble(),
                moistureClasses = metrics.GetProperty("moistureClassMeans").Clone(),
                moistureClassCounts = metrics.GetProperty("moistureClassCounts").Clone(),
                moistureOrdered = metrics.GetProperty("moistureClassesOrdered").GetBoolean(),
                twiAnisotropy = metrics.GetProperty("twiGridAnisotropy").GetDouble(),
                slopeP50 = metrics.GetProperty("landSlopeP50Degrees").GetDouble(),
                slopeP90 = metrics.GetProperty("landSlopeP90Degrees").GetDouble(),
                lowland = metrics.GetProperty("lowlandShareOfLand").GetDouble(),
                floodplain = metrics.GetProperty("floodplainShareOfLand").GetDouble(),
                coastSinuosity = metrics.GetProperty("coastSinuosity").GetDouble(),
                mainStemHack = metrics.GetProperty("hackExponentMainStemMedian").Clone(),
                mainStemBasins = metrics.GetProperty("hackMainStemPerBasin").Clone(),
                receiversChanged = metrics.GetProperty("receiversChangedFraction").GetDouble(),
                residual = metrics.GetProperty("reliefFixedPointResidual").GetDouble(),
                converged = metrics.GetProperty("reliefConvergedOnChannels").GetBoolean(),
                reliefIterations = metrics.GetProperty("reliefIterations").GetInt32(),
                channelReceiversChanged = metrics.GetProperty("channelReceiversChangedFraction").GetDouble(),
                lakesWithOneOutlet = result.Lakes.Count(lake => lake.OutletCount == 1),
                rock = metrics.GetProperty("rockShareOfLand").GetDouble(),
                sand = metrics.GetProperty("sandShareOfLand").GetDouble(),
                facet = metrics.GetProperty("facetShare").GetDouble(),
                anisotropy = metrics.GetProperty("d8Anisotropy").GetDouble(),
                beach = metrics.GetProperty("beachShareOfCoastline").GetDouble(),
                maxStrahler = metrics.GetProperty("maxStrahlerOrder").GetInt32(),
                mouths = metrics.GetProperty("numberOfMouths").GetInt32(),
                density = targets.GetProperty("riverDensityAchieved").GetDouble(),
                lakes = result.Lakes.Count,
                lakesWithInflow = result.Lakes.Count(lake => lake.InflowCatchmentArea >= result.Recipe.channelHeadArea && lake.InflowChannelLength >= result.WorldSize * 0.1f),
                inflowCatchments = result.Lakes.Select(lake => lake.InflowCatchmentArea).ToArray(),
                inflowChannelLengths = result.Lakes.Select(lake => lake.InflowChannelLength).ToArray(),
                lakeCoastDistances = result.Lakes.Select(lake => lake.CoastDistance).ToArray(),
                theta = metrics.GetProperty("concavityThetaPooled").Clone(),
                median = metrics.GetProperty("concavityThetaBasinMedian").Clone(),
                hack = metrics.GetProperty("hackExponentAcrossMouths").Clone(),
                passed = result.Passed,
                warnings = root.GetProperty("warnings").Clone(),
                coreMs = clock.Elapsed.TotalMilliseconds,
                heightHash = Hash(result.Height.Data),
                dischargeHash = Hash(result.Discharge.Data),
                wetnessHash = Hash(result.Wetness.Data),
                moistureHash = Hash(result.Moisture.Data),
                surfaceHashes = result.Surface.Select(layer => Hash(layer.Data)).ToArray()
            };
            JsonElement value = JsonSerializer.SerializeToElement(row);
            reports.Add(value);
            Console.WriteLine(value);
            passed &= row.lakes == result.Recipe.lakes && row.lakesWithInflow == row.lakes && row.lakesWithOneOutlet == row.lakes && result.Passed && targets.GetProperty("peakHeightWithinTolerance").GetBoolean() && row.maxStrahler >= (recipe.preset == Preset.Small ? 3 : 4);
        }

        string name = args[0] == "QualitySweep" ? "validation" : "sample";
        File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        return passed ? 0 : 1;
    }

    private static IEnumerable<IslandRecipe> Recipes(string[] args)
    {
        if (args[0] == "Sample" || args[0] == "PreviewSample")
        {
            if (args.Length < 3 || args.Length > 8)
                throw new ArgumentException("Sample|PreviewSample <preset> <seed> [density] [relief] [erosion] [channelHeadArea] [peakHeight]");
            var recipe = new IslandRecipe
            {
                preset = args[1] == "Tiny" ? Preset.Small : Enum.Parse<Preset>(args[1]),
                seed = int.Parse(args[2], CultureInfo.InvariantCulture)
            };
            if (args[1] == "Tiny")
            {
                recipe.worldSize = 256;
                recipe.simResolution = 64;
                recipe.heightmapResolution = 129;
                recipe.peakHeight = 45;
                recipe.seaDepth = 12;
                recipe.maxRiverWidth = 6;
                recipe.maxRiverDepth = 1;
                recipe.riverDensity = 2;
            }

            if (args.Length >= 4)
                recipe.riverDensity = float.Parse(args[3], CultureInfo.InvariantCulture);
            if (args.Length >= 5)
                recipe.relief = Enum.Parse<ReliefMode>(args[4]);
            if (args.Length >= 6)
                recipe.particleErosion = bool.Parse(args[5]);
            if (args.Length >= 7)
                recipe.channelHeadArea = float.Parse(args[6], CultureInfo.InvariantCulture);
            if (args.Length >= 8)
                recipe.peakHeight = float.Parse(args[7], CultureInfo.InvariantCulture);
            yield return recipe;
            yield break;
        }

        foreach (Preset preset in new[]
        {
            Preset.Small,
            Preset.Medium
        })
            for (int seed = 1; seed <= (preset == Preset.Small ? 5 : 2); seed++)
                yield return new IslandRecipe
                {
                    preset = preset,
                    seed = seed
                };
    }

    private static void Dump(IslandResult result, string stem)
    {
        File.WriteAllText(stem + ".json", result.ReportJson + Environment.NewLine);
        File.WriteAllText(stem + ".water.json", JsonSerializer.Serialize(new { lakes = result.Lakes.Select(l => new { l.Id, l.Level, l.Area }), rivers = result.Rivers.Select(r => new { r.X, r.Z, r.Surface, r.Width }) }));
        using (var surface = new BinaryWriter(File.Create(stem + ".surface.raw")))
        {
            surface.Write(result.Height.Width);
            surface.Write(result.Height.CellSize);
            foreach (Grid layer in result.Surface)
                foreach (float value in layer.Data)
                    surface.Write(value);
            foreach (int id in result.LakeId)
                surface.Write(id);
        }

        using (var moisture = new BinaryWriter(File.Create(stem + ".moisture.raw")))
        {
            moisture.Write(result.Height.Width);
            moisture.Write(result.Height.CellSize);
            foreach (Grid field in new[]
            {
                result.Wetness,
                result.Moisture
            }

            )
                foreach (float value in field.Data)
                    moisture.Write(value);
        }

        using var binary = new BinaryWriter(File.Create(stem + ".raw"));
        binary.Write(result.Height.Width);
        binary.Write(result.Height.CellSize);
        foreach (float height in result.Height.Data)
            binary.Write(height);
    }

    private static string Hash(float[] values)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            foreach (float value in values)
                foreach (byte b in BitConverter.GetBytes(value))
                    hash = (hash ^ b) * 1099511628211UL;
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}
