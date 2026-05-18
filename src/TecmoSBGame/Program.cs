using System;
using TecmoSBGame.Persistence;
using TecmoSBGame.SimArch;

// Arch-only entrypoint.
//
// CLI:
//   --headless-2plays [ticks]
//   --headless-scrimmage-pack
//   --headless-pass-metadata
//   --headless-pass-outcomes
//   --headless-fumble
//   --headless-pressure [ticks]
//   --headless-drive
//   --headless-quarter-flow
//   --headless-scoreboard
//   --headless-stats
//   --headless-kickoff-after-score
//   --headless-kickoff
//   --headless-punt
//   --headless-field-goal
//   --save-roundtrip [rootDir]
//   --season-roundtrip [rootDir]
//   --season-meta-flow [rootDir]
//   --headless-determinism-check <scenario> [runs]
//   --runtime-capture [ticks]
// Runs Arch sim deterministic headless scenarios (no graphics).
if (args.Length > 0 && string.Equals(args[0], "--headless-2plays", StringComparison.OrdinalIgnoreCase))
{
    var ticks = 240;
    if (args.Length > 1 && int.TryParse(args[1], out var parsed) && parsed > 0)
        ticks = parsed;

    Environment.ExitCode = SimArchHeadless.RunTwoPlaysScenario(ticks);
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--headless-scrimmage-pack", StringComparison.OrdinalIgnoreCase))
{
    Environment.ExitCode = SimArchHeadless.RunScrimmageScenarioPack();
    return;
}

if (args.Length > 0 && (string.Equals(args[0], "--headless-pass-metadata", StringComparison.OrdinalIgnoreCase)
    || string.Equals(args[0], "--headless-pass-outcomes", StringComparison.OrdinalIgnoreCase)))
{
    Environment.ExitCode = SimArchHeadless.RunPassMetadataScenario();
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--headless-fumble", StringComparison.OrdinalIgnoreCase))
{
    Environment.ExitCode = SimArchHeadless.RunFumbleRecoveryScenario();
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--headless-pressure", StringComparison.OrdinalIgnoreCase))
{
    var ticks = 120;
    if (args.Length > 1 && int.TryParse(args[1], out var parsed) && parsed > 0)
        ticks = parsed;

    Environment.ExitCode = SimArchHeadless.RunPressureScenario(ticks);
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--headless-drive", StringComparison.OrdinalIgnoreCase))
{
    Environment.ExitCode = SimArchHeadless.RunDriveLifecycleScenario();
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--headless-quarter-flow", StringComparison.OrdinalIgnoreCase))
{
    Environment.ExitCode = SimArchHeadless.RunQuarterFlowScenario();
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--headless-scoreboard", StringComparison.OrdinalIgnoreCase))
{
    Environment.ExitCode = SimArchHeadless.RunScoreboardIntegrationScenario();
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--headless-stats", StringComparison.OrdinalIgnoreCase))
{
    Environment.ExitCode = SimArchHeadless.RunStatsScenario();
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--headless-kickoff-after-score", StringComparison.OrdinalIgnoreCase))
{
    Environment.ExitCode = SimArchHeadless.RunKickoffAfterScoreScenario();
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--headless-kickoff", StringComparison.OrdinalIgnoreCase))
{
    Environment.ExitCode = SimArchHeadless.RunKickoffScenario();
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--headless-punt", StringComparison.OrdinalIgnoreCase))
{
    Environment.ExitCode = SimArchHeadless.RunPuntScenario();
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--headless-field-goal", StringComparison.OrdinalIgnoreCase))
{
    Environment.ExitCode = SimArchHeadless.RunFieldGoalScenario();
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--save-roundtrip", StringComparison.OrdinalIgnoreCase))
{
    var rootDir = args.Length > 1 ? args[1] : null;
    Environment.ExitCode = SaveRoundTripRunner.Run(rootDir);
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--season-roundtrip", StringComparison.OrdinalIgnoreCase))
{
    var rootDir = args.Length > 1 ? args[1] : null;
    Environment.ExitCode = SeasonRoundTripRunner.Run(rootDir);
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--season-meta-flow", StringComparison.OrdinalIgnoreCase))
{
    var rootDir = args.Length > 1 ? args[1] : null;
    Environment.ExitCode = SeasonMetaFlowRunner.Run(rootDir);
    return;
}

if (args.Length > 0 && string.Equals(args[0], "--headless-determinism-check", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("usage: --headless-determinism-check <scenario> [runs]");
        Environment.ExitCode = 2;
        return;
    }

    var runs = 2;
    if (args.Length > 2 && (!int.TryParse(args[2], out runs) || runs < 2))
    {
        Console.Error.WriteLine("runs must be an integer >= 2");
        Environment.ExitCode = 2;
        return;
    }

    Environment.ExitCode = SimArchHeadless.RunDeterminismCheck(args[1], runs);
    return;
}

var captureTicks = 0;
if (args.Length > 0 && string.Equals(args[0], "--runtime-capture", StringComparison.OrdinalIgnoreCase))
{
    captureTicks = 240;
    if (args.Length > 1 && (!int.TryParse(args[1], out captureTicks) || captureTicks <= 0))
    {
        Console.Error.WriteLine("usage: --runtime-capture [ticks]");
        Environment.ExitCode = 2;
        return;
    }
}

using var game = new TecmoSBGame.MainGameArch(captureTicks);
game.Run();
