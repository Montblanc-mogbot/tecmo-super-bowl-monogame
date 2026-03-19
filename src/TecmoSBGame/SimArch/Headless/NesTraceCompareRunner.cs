using System;
using System.IO;
using System.Text.Json;

namespace TecmoSBGame.SimArch.Headless;

/// <summary>
/// Compares an emulator-produced NES trace to a SimArch run.
///
/// Ported from: ArchiveMge/Headless/NesTraceCompareRunner.cs
/// </summary>
public static class NesTraceCompareRunner
{
    public static int Run(string traceJsonPath, int maxTicks = 180)
    {
        if (!File.Exists(traceJsonPath))
        {
            Console.Error.WriteLine($"trace not found: {traceJsonPath}");
            return 2;
        }

        var traceJson = File.ReadAllText(traceJsonPath);
        var trace = JsonSerializer.Deserialize<NesTrace>(traceJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (trace is null)
        {
            Console.Error.WriteLine("failed to parse trace json");
            return 3;
        }

        // TODO: true diffing once snapshot parity fields are stabilized.
        // For now, just run the Arch scenario for the requested tick count.
        var rc = SimArchHeadless.RunTwoPlaysScenario(maxTicks);
        Console.WriteLine($"[nes-trace-compare] traceFrames={trace.Frames.Count} ranTicks={maxTicks} rc={rc}");
        return rc;
    }
}
