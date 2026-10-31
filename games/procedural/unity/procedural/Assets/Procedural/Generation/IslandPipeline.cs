using System;
using System.Diagnostics;
using System.Threading;

namespace Procedural.Generation
{
    /// <summary>Deterministic CPU island generation. Safe to call concurrently on background threads.</summary>
    public static class IslandPipeline
    {
        /// <summary>Generate an island; optional checkpoints cache Shape, Relief and Upsample. Cancellation throws.</summary>
        public static IslandResult Run(IslandRecipe recipe, BakeMode mode, string checkpointDir, IProgress<PipelineProgress> progress, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            if (!Enum.IsDefined(typeof(BakeMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            IslandRecipe r = TerrainStages.Resolve(recipe);
            var s = new TerrainState();
            var water = new WaterState();
            string[] names =
            {
                "Shape",
                "Relief",
                "Upsample",
                "ParticleErosion",
                "Lakes",
                "Rivers",
                "Hydrology",
                "Moisture",
                "Surface",
                "Vegetation",
                "Report"
            };
            var times = new double[names.Length];
            var loaded = new bool[names.Length];
            ulong key = 0;
            Grid[] materials = null;
            VegetationResult vegetation = null;
            IslandResult result = null;
            for (int stage = 0; stage < names.Length; stage++)
            {
                cancel.ThrowIfCancellationRequested();
                int current = stage;
                Action<float> notify = f =>
                {
                    if (progress != null)
                        progress.Report(new PipelineProgress(names[current], current, names.Length, f));
                };
                notify(0);
                var timer = Stopwatch.StartNew();
                if (stage < 3)
                {
                    key = Checkpoints.Key(key, r, mode, stage);
                    loaded[stage] = Checkpoints.Load(checkpointDir, stage, key, s, cancel);
                }

                if (!loaded[stage])
                {
                    switch (stage)
                    {
                        case 0:
                            TerrainStages.Shape(r, s, cancel);
                            break;
                        case 1:
                            TerrainStages.Relief(r, mode, s, cancel, notify);
                            break;
                        case 2:
                            TerrainStages.Upsample(r, mode, s, cancel);
                            break;
                        case 3:
                            if (r.particleErosion && mode == BakeMode.Bake)
                                ParticleErosion.Run(r, s, cancel);
                            break;
                        case 4:
                            WaterStages.Lakes(r, s, water, cancel, mode);
                            break;
                        case 5:
                            WaterStages.Rivers(r, s, water, cancel);
                            break;
                        case 6:
                            WaterStages.FinalHydrology(s, water, cancel);
                            break;
                        case 7:
                            water.Moisture = WetnessStages.Build(r, s, water, cancel);
                            break;
                        case 8:
                            materials = WaterStages.Materials(r, s, water, cancel);
                            break;
                        case 9:
                            if (r.vegetation && mode == BakeMode.Bake)
                                vegetation = VegetationStage.Place(new VegetationInput
                                {
                                    Height = s.Height,
                                    Moisture = water.Moisture.Moisture,
                                    Surface = materials,
                                    Ocean = s.Ocean,
                                    Water = water.Moisture.Water,
                                    WaterDistance = water.Moisture.WaterDistance,
                                    Seed = r.seed,
                                    Peak = r.peakHeight,
                                    ForestCover = r.forestCover
                                }, SpeciesTable.Temperate(r.peakHeight), cancel);
                            break;
                        case 10:
                            result = new IslandResult
                            {
                                Recipe = r,
                                Mode = mode,
                                WorldSize = r.worldSize,
                                SeaDepth = 0,
                                Height = s.Height,
                                Surface = materials,
                                Wetness = water.Moisture.Wetness,
                                Moisture = water.Moisture.Moisture,
                                LakeId = water.LakeId,
                                Lakes = water.Lakes,
                                Rivers = water.Rivers,
                                Discharge = new Grid(s.Height.Width, s.Height.Height, s.Height.CellSize)
                            };
                            Array.Copy(water.Routing.Discharge, result.Discharge.Data, result.Discharge.Data.Length);
                            for (int i = 0; i < s.Height.Data.Length; i++)
                            {
                                MathCore.Check(cancel, i);
                                result.PeakHeight = Math.Max(result.PeakHeight, s.Height.Data[i]);
                                result.SeaDepth = Math.Max(result.SeaDepth, -s.Height.Data[i]);
                            }

                            result.Species = vegetation != null ? vegetation.Species : SpeciesTable.Temperate(r.peakHeight);
                            result.Plants = vegetation != null ? vegetation.Plants : new System.Collections.Generic.List<PlantInstance>();
                            result.DetailCoverage = vegetation != null ? vegetation.DetailCoverage : EmptyDetails(result.Species, s.Height);
                            Reporting.Build(result, s, water, names, times, loaded, cancel);
                            if (vegetation != null)
                            {
                                water.Report.Vegetation = vegetation;
                                water.Report.Invariants.AddRange(vegetation.Invariants);
                                water.Report.Warnings.AddRange(vegetation.Warnings);
                                foreach (Invariant v in vegetation.Invariants)
                                    result.Passed &= v.Passed;
                            }

                            break;
                    }

                    if (stage < 3)
                        Checkpoints.Save(checkpointDir, stage, key, s, cancel);
                }

                times[stage] = timer.Elapsed.TotalMilliseconds;
                notify(1);
            }

            result.ReportJson = Reporting.Serialize(result, s, water, names, times, loaded);
            cancel.ThrowIfCancellationRequested();
            return result;
        }

        private static Grid[] EmptyDetails(SpeciesSpec[] species, Grid height)
        {
            var maps = new System.Collections.Generic.List<Grid>();
            foreach (SpeciesSpec s in species)
                if (s.Detail)
                    maps.Add(new Grid(height.Width, height.Height, height.CellSize));
            return maps.ToArray();
        }
    }
}
