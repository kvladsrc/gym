// Optional socket-free host for the very same NUnit tests: dotnet run -p:InProcessTests=true -- [Sample|PreviewSample|QualitySweep|SeedSweep|Timing]
using System;
using System.Collections.Generic;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

internal static class InProcessRunner
{
    private sealed class Listener : ITestListener
    {
        internal System.IO.TextWriter Writer;
        public void TestStarted(ITest test)
        {
        }

        public void TestFinished(ITestResult result)
        {
            if (result.Test.IsSuite)
                return;
            Writer.WriteLine(result.ResultState + " " + result.FullName);
            if (!string.IsNullOrEmpty(result.Message))
                Writer.WriteLine(result.Message);
            if (!string.IsNullOrEmpty(result.StackTrace))
                Writer.WriteLine(result.StackTrace);
            if (!string.IsNullOrEmpty(result.Output))
                Writer.WriteLine(result.Output);
        }

        public void TestOutput(TestOutput output)
        {
            Writer.Write(output.Text);
        }

        public void SendMessage(TestMessage message)
        {
        }
    }

    private static int Main(string[] args)
    {
        if (args.Length > 0 && (args[0] == "QualitySweep" || args[0] == "Sample" || args[0] == "PreviewSample"))
            return ValidationSweep.Run(args);
        if (args.Length > 1 || (args.Length == 1 && args[0] != "Timing" && args[0] != "SeedSweep"))
        {
            Console.Error.WriteLine("Usage: Sample|PreviewSample <preset> <seed> [density] [relief] [erosion] [channelHeadArea] [peakHeight] | QualitySweep | SeedSweep | Timing (no arguments runs all tests)");
            return 2;
        }

        var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
        runner.Load(typeof(Procedural.Tests.GenerationTests).Assembly, new Dictionary<string, object> { { "NumberOfTestWorkers", 0 } });
        TestFilter filter = args.Length > 0 ? TestFilter.FromXml("<filter><test>Procedural.Tests.GenerationTests." + (args[0] == "Timing" ? "TimingSmallPreviewAndBake" : "SeedSweep") + "</test></filter>") : TestFilter.Empty;
        ITestResult result = runner.Run(new Listener { Writer = Console.Out }, filter);
        Console.WriteLine("Passed=" + result.PassCount + " Failed=" + result.FailCount + " Skipped=" + result.SkipCount);
        return result.FailCount == 0 ? 0 : 1;
    }
}
