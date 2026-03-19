namespace TecmoSBGame.SimArch.Headless;

/// <summary>
/// Headless runners for SimArch.
///
/// Ported from: ArchiveMge/Headless/HeadlessRunner.cs
/// </summary>
public static class HeadlessRunner
{
    public static int Run(int ticks = 300)
    {
        // For now, route to the existing Arch smoke scenario.
        return SimArchHeadless.RunTwoPlaysScenario(ticks);
    }

    public static int RunTwoPlaysScenario(int ticks = 240)
        => SimArchHeadless.RunTwoPlaysScenario(ticks);
}
