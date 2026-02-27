using System;
using TecmoSBGame;
using TecmoSBGame.Headless;

// CLI:
//   --headless [ticks]
// Runs a deterministic, windowless simulation for CI/headless verification.
if (args.Length > 0 && string.Equals(args[0], "--headless", StringComparison.OrdinalIgnoreCase))
{
    var ticks = 300;
    if (args.Length > 1 && int.TryParse(args[1], out var parsed) && parsed > 0)
        ticks = parsed;

    Environment.ExitCode = HeadlessRunner.Run(ticks);
    return;
}

using var game = new MainGame();
game.Run();
