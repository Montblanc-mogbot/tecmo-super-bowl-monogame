using System;
using System.IO;
using System.Linq;

namespace TecmoSBGame.Persistence;

public static class SeasonRoundTripRunner
{
    public static int Run(string? rootOverride = null)
    {
        var resolver = new SavePathResolver(rootOverride);
        var saveManager = new SaveManager(resolver);
        var seasonManager = new SeasonManager(saveManager);

        var season = seasonManager.CreateSeason("season-1", new[] { 0, 1, 2, 3 });
        var openingWeek = season.GetWeek(1);
        if (openingWeek is null || openingWeek.Games.Count != 2)
        {
            Console.Error.WriteLine("[season-roundtrip] FAIL missing week 1 schedule scaffolding");
            return 1;
        }

        var results = openingWeek.Games
            .Select((game, index) => SeasonManager.CreateGameResult(
                game.HomeTeamId,
                game.AwayTeamId,
                homeScore: 17 + index,
                awayScore: 10 + index,
                wasSimulated: true,
                source: "headless-week-1"))
            .ToList();

        var advance = seasonManager.ApplyWeekResults(season, 1, results);
        if (!advance.WeekCompleted || advance.AdvancedWeek != 2 || advance.GamesApplied != 2)
        {
            Console.Error.WriteLine($"[season-roundtrip] FAIL week advance mismatch advancedWeek={advance.AdvancedWeek} completed={advance.WeekCompleted} games={advance.GamesApplied}");
            return 1;
        }

        seasonManager.SaveSeasonState(season);
        var loaded = seasonManager.LoadSeasonState(season.SlotName);
        if (loaded is null)
        {
            Console.Error.WriteLine("[season-roundtrip] FAIL saved season did not reload");
            return 1;
        }

        var topRecord = loaded.Standings.FirstOrDefault();
        var reloadedWeek = loaded.GetWeek(1);
        if (loaded.CurrentWeek != 2
            || reloadedWeek is null
            || reloadedWeek.Games.Any(g => g.Result is null)
            || topRecord is null
            || topRecord.Wins != 1
            || topRecord.PointsFor < 17)
        {
            Console.Error.WriteLine("[season-roundtrip] FAIL loaded season state missing standings/results progression");
            return 1;
        }

        var seasonStatePath = resolver.GetSeasonStatePath(season.SlotName);
        var slotPath = resolver.GetSeasonSlotPath(season.SlotName);
        if (!File.Exists(seasonStatePath) || !File.Exists(slotPath))
        {
            Console.Error.WriteLine("[season-roundtrip] FAIL expected season save artifacts not found");
            return 1;
        }

        Console.WriteLine($"[season-roundtrip] PASS root={resolver.RootDirectory} week={loaded.CurrentWeek} leader={topRecord.TeamId} record={topRecord.Wins}-{topRecord.Losses}-{topRecord.Ties}");
        Console.WriteLine($"[season-roundtrip] state={seasonStatePath}");
        Console.WriteLine($"[season-roundtrip] slot={slotPath}");
        return 0;
    }
}
