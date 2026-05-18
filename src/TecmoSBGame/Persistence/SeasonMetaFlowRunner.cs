using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TecmoSBGame.Persistence;

public static class SeasonMetaFlowRunner
{
    public static int Run(string? rootDir)
    {
        var resolver = new SavePathResolver(rootDir);
        var saveManager = new SaveManager(resolver);
        var seasonManager = new SeasonManager(saveManager);
        var presentation = new SeasonPresentationService();

        var season = seasonManager.CreateSeason("season-meta", new[] { 0, 1, 2, 3 });
        presentation.RefreshDerivedState(season);
        seasonManager.SaveSeasonState(season);

        while (season.CurrentWeek <= season.TotalWeeks)
        {
            var week = season.GetWeek(season.CurrentWeek);
            if (week is null)
                throw new InvalidOperationException($"Missing week {season.CurrentWeek}.");

            var results = SimulateWeek(week).ToList();
            seasonManager.ApplyWeekResults(season, week.Week, results);
            presentation.RefreshDerivedState(season);
            seasonManager.SaveSeasonState(season);
        }

        presentation.RefreshDerivedState(season);
        seasonManager.SaveSeasonState(season);
        var loaded = seasonManager.LoadSeasonState(season.SlotName);
        if (loaded is null)
            throw new InvalidOperationException("Failed to reload saved meta season state.");

        presentation.RefreshDerivedState(loaded);

        var hub = presentation.RenderHub(loaded);
        var standings = presentation.RenderStandings(loaded);
        var leaders = presentation.RenderLeaders(loaded);
        var records = presentation.RenderRecords(loaded);
        var playoffs = presentation.RenderPlayoffs(loaded);
        var proBowl = presentation.RenderProBowl(loaded);

        var summaryPath = Path.Combine(resolver.RootDirectory, "season-meta-summary.txt");
        File.WriteAllText(summaryPath, string.Join(Environment.NewLine + Environment.NewLine, new[]
        {
            hub,
            standings,
            leaders,
            records,
            playoffs,
            proBowl,
        }));

        if (loaded.CurrentWeek <= loaded.TotalWeeks)
            throw new InvalidOperationException("Season did not advance to completion.");
        if (loaded.Playoffs.Seeds.Count == 0 || string.IsNullOrWhiteSpace(loaded.Playoffs.Champion))
            throw new InvalidOperationException("Playoff/champion summary was not derived.");
        if (!loaded.ProBowl.IsReady || loaded.ProBowl.TeamCodes.Count == 0)
            throw new InvalidOperationException("Pro Bowl summary was not derived.");
        if (loaded.Records.Entries.Count == 0 || loaded.Leaders.Entries.Count == 0)
            throw new InvalidOperationException("Leaders/records were not derived.");

        Console.WriteLine($"[season-meta-flow] PASS root={resolver.RootDirectory} champion={loaded.Playoffs.Champion} summary={summaryPath}");
        return 0;
    }

    private static IEnumerable<SeasonGameResult> SimulateWeek(SeasonWeekSchedule week)
    {
        foreach (var game in week.Games)
        {
            var homeScore = 14 + ((game.HomeTeamId * 7 + week.Week * 5 + game.GameNumber * 3) % 21);
            var awayScore = 10 + ((game.AwayTeamId * 5 + week.Week * 4 + game.GameNumber * 2) % 21);
            if (homeScore == awayScore)
                homeScore += 3;

            yield return SeasonManager.CreateGameResult(
                game.HomeTeamId,
                game.AwayTeamId,
                homeScore,
                awayScore,
                wasSimulated: true,
                source: $"season-meta-week-{week.Week}");
        }
    }
}
