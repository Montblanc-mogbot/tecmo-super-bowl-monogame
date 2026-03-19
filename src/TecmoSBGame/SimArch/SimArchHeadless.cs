using System;

namespace TecmoSBGame.SimArch;

public static class SimArchHeadless
{
    public static int RunTwoPlaysScenario(int ticks = 240)
    {
        using var sim = new Sim();

        // Deterministic play selection (same as the in-game bootstrap for now).
        sim.ApplyPlaySelection(new Sim.PendingPlaySelection(
            PlayNumber: 10,
            FormationId: "00",
            OffensivePlayName: "DEMO",
            OffensivePlaySlot: "DEMO"));

        for (var i = 0; i < ticks; i++)
            sim.Update(dtSeconds: 1f / 60f);

        // Minimal invariant checks (kept intentionally lightweight for now).
        if (sim.Snapshot.Tick <= 0)
        {
            Console.Error.WriteLine("[headless-2plays] FAIL: no ticks advanced");
            return 2;
        }

        Console.WriteLine($"[headless-2plays] OK ticks={sim.Snapshot.Tick} players={sim.Snapshot.Players.Length}");
        return 0;
    }
}
