using System;

namespace TecmoSBGame.Persistence;

public static class SaveRoundTripRunner
{
    public static int Run(string? rootOverride = null)
    {
        var resolver = new SavePathResolver(rootOverride);
        var manager = new SaveManager(resolver);

        var settings = new GameSettings
        {
            MasterVolume = 0.65f,
            PauseOnFocusLoss = false,
            LastSelectedTeam = "NYG",
        };
        manager.SaveSettings(settings);
        var loadedSettings = manager.LoadSettingsOrDefault();
        if (Math.Abs(loadedSettings.MasterVolume - settings.MasterVolume) > 0.0001f
            || loadedSettings.PauseOnFocusLoss != settings.PauseOnFocusLoss
            || loadedSettings.LastSelectedTeam != settings.LastSelectedTeam)
        {
            Console.Error.WriteLine("[save-roundtrip] FAIL settings reload mismatch");
            return 1;
        }

        var season = new SeasonSlotSummary
        {
            SlotName = "season-1",
            Week = 3,
            HomeTeamId = 7,
            AwayTeamId = 2,
        };
        manager.SaveSeasonSlot(season);
        var loadedSeason = manager.LoadSeasonSlot(season.SlotName);
        if (loadedSeason is null
            || loadedSeason.Week != season.Week
            || loadedSeason.HomeTeamId != season.HomeTeamId
            || loadedSeason.AwayTeamId != season.AwayTeamId)
        {
            Console.Error.WriteLine("[save-roundtrip] FAIL season slot reload mismatch");
            return 1;
        }

        Console.WriteLine($"[save-roundtrip] PASS root={resolver.RootDirectory}");
        Console.WriteLine($"[save-roundtrip] settings={resolver.SettingsFilePath}");
        Console.WriteLine($"[save-roundtrip] season={resolver.GetSeasonSlotPath(season.SlotName)}");
        return 0;
    }
}
