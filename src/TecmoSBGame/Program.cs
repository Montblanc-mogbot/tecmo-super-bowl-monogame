using System;
using TecmoSBGame.SimArch;

// Arch-only entrypoint.
//
// CLI:
//   --headless-2plays [ticks]
// Runs an Arch sim deterministic smoke test (no graphics).
if (args.Length > 0 && string.Equals(args[0], "--headless-2plays", StringComparison.OrdinalIgnoreCase))
{
    var ticks = 240;
    if (args.Length > 1 && int.TryParse(args[1], out var parsed) && parsed > 0)
        ticks = parsed;

    Environment.ExitCode = SimArchHeadless.RunTwoPlaysScenario(ticks);
    return;
}

using var game = new TecmoSBGame.MainGameArch();
game.Run();
