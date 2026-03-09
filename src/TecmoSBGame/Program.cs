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


if (args.Length > 0 && string.Equals(args[0], "--headless-coverage", StringComparison.OrdinalIgnoreCase))
{
    var ticks = 240;
    if (args.Length > 1 && int.TryParse(args[1], out var parsed) && parsed > 0)
        ticks = parsed;

    Environment.ExitCode = HeadlessRunner.RunCoverageScenario(ticks);
    return;
}

using var game = new MainGame();
game.Run();
