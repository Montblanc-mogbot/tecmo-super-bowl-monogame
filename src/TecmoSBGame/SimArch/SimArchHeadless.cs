using System;

namespace TecmoSBGame.SimArch;

public static class SimArchHeadless
{
    public static int RunTwoPlaysScenario(int ticks = 240)
    {
        var formationData = TecmoSB.FormationDataYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "formations", "formation_data.yaml"));
        var defensiveFormationData = TecmoSB.DefensiveFormationDataYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "formations", "defensive_formation_data.yaml"));
        var playList = TecmoSB.PlayListYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "playcall", "playlist.yaml"));
        var defensePlays = TecmoSB.DefensePlayYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "defenseplays", "bank4_defense_special_pointers.yaml"));
        var playData = TecmoSB.PlayDataYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "playdata", "bank5_6_play_data.yaml"));

        using var sim = new Sim(formationData, defensiveFormationData, playList, playData, defensePlays);

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
