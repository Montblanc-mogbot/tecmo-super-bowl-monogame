using System;
using TecmoSBGame;
using TecmoSBGame.Headless;

// CLI:
//   --headless [ticks]
// Runs a deterministic, windowless simulation for CI/headless verification.
//
//   --headless-coverage [ticks]
// Runs a deterministic scenario that exercises coverage AI + ball-in-air break.
if (args.Length > 0 && string.Equals(args[0], "--headless", StringComparison.OrdinalIgnoreCase))
{
    var ticks = 300;
    if (args.Length > 1 && int.TryParse(args[1], out var parsed) && parsed > 0)
        ticks = parsed;

    Environment.ExitCode = HeadlessRunner.Run(ticks);
    return;
}


if (args.Length > 0 && string.Equals(args[0], "--headless-2plays", StringComparison.OrdinalIgnoreCase))
{
    var ticks = 240;
    if (args.Length > 1 && int.TryParse(args[1], out var parsed) && parsed > 0)
        ticks = parsed;

    Environment.ExitCode = HeadlessRunner.RunTwoPlaysScenario(ticks);
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--headless-2plays-arch", StringComparison.OrdinalIgnoreCase))
{
    var ticks = 240;
    if (args.Length > 1 && int.TryParse(args[1], out var parsed) && parsed > 0)
        ticks = parsed;

    Environment.ExitCode = HeadlessRunner.RunTwoPlaysScenarioArch(ticks);
    return;
}


if (args.Length > 0 && string.Equals(args[0], "--headless-coverage", StringComparison.OrdinalIgnoreCase))
{
    var ticks = 240;
    if (args.Length > 1 && int.TryParse(args[1], out var parsed) && parsed > 0)
        ticks = parsed;

    Environment.ExitCode = HeadlessRunner.RunCoverageScenario(ticks);
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--headless-nes-compare", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("usage: --headless-nes-compare <trace.json> [maxTicks]");
        Environment.ExitCode = 2;
        return;
    }

    var tracePath = args[1];
    var ticks = 180;
    if (args.Length > 2 && int.TryParse(args[2], out var parsed) && parsed > 0)
        ticks = parsed;

    Environment.ExitCode = NesTraceCompareRunner.Run(tracePath, ticks);
    return;
}

// Simulation mode flag (default: legacy MonoGame.Extended.Entities)
var simMode = TecmoSBGame.SimArch.SimMode.Mge;
for (var i = 0; i < args.Length; i++)
{
    var a = args[i];
    if (a.StartsWith("--sim=", StringComparison.OrdinalIgnoreCase))
    {
        var v = a.Substring("--sim=".Length);
        if (string.Equals(v, "arch", StringComparison.OrdinalIgnoreCase))
            simMode = TecmoSBGame.SimArch.SimMode.Arch;
        else if (string.Equals(v, "mge", StringComparison.OrdinalIgnoreCase))
            simMode = TecmoSBGame.SimArch.SimMode.Mge;
    }
}

using var game = new MainGame(simMode);
game.Run();
