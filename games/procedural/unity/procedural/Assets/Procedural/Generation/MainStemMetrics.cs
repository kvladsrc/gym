using System;
using System.Collections.Generic;
using System.Threading;

namespace Procedural.Generation
{
    internal sealed class MainStemFit
    {
        internal int Mouth, Samples;
        internal float Area;
        internal double Exponent, AreaDecades;
    }

    internal static class MainStemMetrics
    {
        internal static void Measure(Drainage d, Grid g, ReportData report, CancellationToken cancel)
        {
            float landArea = d.Stack.Length * g.CellSize * g.CellSize;
            var exponents = new List<double>();
            foreach (int mouth in d.Stack)
            {
                MathCore.Check(cancel, mouth);
                if (!d.Ocean[d.Receiver[mouth]] || d.Discharge[mouth] < landArea * 0.05f)
                    continue;
                Fit fit = new Fit();
                float floor = Math.Max(16 * g.CellSize * g.CellSize, d.Discharge[mouth] * 0.001f);
                int i = mouth;
                while (i >= 0 && d.Discharge[i] >= floor)
                {
                    MathCore.Check(cancel, i);
                    fit.Add(d.Discharge[i], d.Length[i]);
                    int donor = -1;
                    for (int j = d.DonorHead[i]; j >= 0; j = d.DonorNext[j])
                        if (donor < 0 || d.Discharge[j] > d.Discharge[donor])
                            donor = j;
                    i = donor;
                }

                double exponent = fit.N >= 20 && fit.MaxX - fit.MinX >= Math.Log(10) ? fit.Slope() : double.NaN;
                report.MainStems.Add(new MainStemFit { Mouth = mouth, Area = d.Discharge[mouth], Samples = fit.N, AreaDecades = (fit.MaxX - fit.MinX) / Math.Log(10), Exponent = exponent });
                if (!double.IsNaN(exponent))
                    exponents.Add(exponent);
            }

            exponents.Sort();
            int count = exponents.Count;
            if (count > 0)
                report.MainStemHack = count % 2 == 1 ? exponents[count / 2] : (exponents[count / 2 - 1] + exponents[count / 2]) * 0.5;
        }
    }
}
