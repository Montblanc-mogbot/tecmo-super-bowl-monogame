using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TecmoSBGame.Persistence;

public sealed class SeasonManager
{
    private readonly SaveManager _saveManager;
    private readonly SeasonPresentationService _presentationService;

    public SeasonManager(SaveManager saveManager, SeasonPresentationService? presentationService = null)
    {
        _saveManager = saveManager ?? throw new ArgumentNullException(nameof(saveManager));
        _presentationService = presentationService ?? new SeasonPresentationService();
    }

    public SeasonState CreateSeason(string slotName, IReadOnlyList<int> teamIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotName);
        ArgumentNullException.ThrowIfNull(teamIds);
        if (teamIds.Count < 2 || teamIds.Count % 2 != 0)
            throw new ArgumentException("Season scaffolding requires an even team count of at least 2.", nameof(teamIds));

        var normalized = teamIds.Distinct().ToList();
        if (normalized.Count != teamIds.Count)
            throw new ArgumentException("Season scaffolding requires unique team ids.", nameof(teamIds));

        var state = new SeasonState
        {
            SlotName = slotName,
            CurrentWeek = 1,
            TotalWeeks = normalized.Count - 1,
            TeamIds = normalized,
            Schedule = BuildRoundRobin(normalized),
            Standings = normalized.Select(id => new SeasonTeamRecord { TeamId = id, TeamCode = $"T{id:00}" }).ToList(),
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

        _presentationService.RefreshDerivedState(state);
        return state;
    }

    public SeasonAdvanceResult ApplyWeekResults(SeasonState state, int week, IReadOnlyList<SeasonGameResult> results)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(results);

        var targetWeek = state.GetWeek(week) ?? throw new InvalidOperationException($"Week {week} is not present in season '{state.SlotName}'.");
        if (results.Count != targetWeek.Games.Count)
            throw new InvalidOperationException($"Week {week} expected {targetWeek.Games.Count} results but received {results.Count}.");

        for (var i = 0; i < targetWeek.Games.Count; i++)
        {
            var scheduled = targetWeek.Games[i];
            var result = results[i];
            if (scheduled.HomeTeamId != result.HomeTeamId || scheduled.AwayTeamId != result.AwayTeamId)
                throw new InvalidOperationException($"Week {week} result {i} did not match scheduled teams {scheduled.AwayTeamId} at {scheduled.HomeTeamId}.");
            if (scheduled.IsComplete)
                throw new InvalidOperationException($"Week {week} game {scheduled.GameNumber} already has a result.");

            scheduled.Result = result;
            ApplyResultToStandings(state, result);
        }

        var weekCompleted = targetWeek.IsComplete;
        if (weekCompleted)
            state.CurrentWeek = Math.Min(state.TotalWeeks + 1, week + 1);

        state.UpdatedAtUtc = DateTimeOffset.UtcNow;
        _presentationService.RefreshDerivedState(state);

        return new SeasonAdvanceResult
        {
            AdvancedWeek = state.CurrentWeek,
            WeekCompleted = weekCompleted,
            SeasonComplete = state.CurrentWeek > state.TotalWeeks,
            GamesApplied = results.Count,
        };
    }

    public void SaveSeasonState(SeasonState season)
    {
        _saveManager.SaveSeasonState(season);
        _saveManager.SaveSeasonSlot(BuildSummary(season));
    }

    public SeasonState? LoadSeasonState(string slotName)
    {
        var loaded = _saveManager.LoadSeasonState(slotName);
        if (loaded is not null)
            _presentationService.RefreshDerivedState(loaded);
        return loaded;
    }

    public static SeasonGameResult CreateGameResult(int homeTeamId, int awayTeamId, int homeScore, int awayScore, bool wasSimulated, string source)
    {
        return new SeasonGameResult
        {
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            HomeScore = homeScore,
            AwayScore = awayScore,
            WinningTeamId = homeScore == awayScore ? null : homeScore > awayScore ? homeTeamId : awayTeamId,
            WasSimulated = wasSimulated,
            Source = source,
        };
    }

    public static SeasonSlotSummary BuildSummary(SeasonState season)
    {
        var upcoming = season.GetWeek(Math.Min(season.CurrentWeek, Math.Max(1, season.TotalWeeks)));
        var featuredGame = upcoming?.Games.FirstOrDefault();
        return new SeasonSlotSummary
        {
            SlotName = season.SlotName,
            Week = season.CurrentWeek,
            HomeTeamId = featuredGame?.HomeTeamId ?? season.TeamIds.FirstOrDefault(),
            AwayTeamId = featuredGame?.AwayTeamId ?? season.TeamIds.Skip(1).FirstOrDefault(),
            UpdatedAtUtc = season.UpdatedAtUtc,
        };
    }

    private static List<SeasonWeekSchedule> BuildRoundRobin(IReadOnlyList<int> teamIds)
    {
        var rotation = teamIds.ToList();
        var weeks = new List<SeasonWeekSchedule>();
        for (var week = 1; week <= rotation.Count - 1; week++)
        {
            var games = new List<SeasonScheduledGame>();
            for (var pair = 0; pair < rotation.Count / 2; pair++)
            {
                var away = rotation[pair];
                var home = rotation[rotation.Count - 1 - pair];
                if (week % 2 == 0)
                    (home, away) = (away, home);

                games.Add(new SeasonScheduledGame
                {
                    Week = week,
                    GameNumber = pair + 1,
                    HomeTeamId = home,
                    AwayTeamId = away,
                });
            }

            weeks.Add(new SeasonWeekSchedule { Week = week, Games = games });
            RotateRoundRobin(rotation);
        }

        return weeks;
    }

    private static void RotateRoundRobin(List<int> rotation)
    {
        var fixedTeam = rotation[0];
        var last = rotation[^1];
        rotation.RemoveAt(rotation.Count - 1);
        rotation.Insert(1, last);
        rotation[0] = fixedTeam;
    }

    private static void ApplyResultToStandings(SeasonState state, SeasonGameResult result)
    {
        var home = state.GetOrCreateRecord(result.HomeTeamId);
        var away = state.GetOrCreateRecord(result.AwayTeamId);

        home.PointsFor += result.HomeScore;
        home.PointsAgainst += result.AwayScore;
        away.PointsFor += result.AwayScore;
        away.PointsAgainst += result.HomeScore;

        switch (result.Outcome)
        {
            case SeasonGameOutcome.HomeWin:
                home.Wins++;
                away.Losses++;
                break;
            case SeasonGameOutcome.AwayWin:
                away.Wins++;
                home.Losses++;
                break;
            default:
                home.Ties++;
                away.Ties++;
                break;
        }
    }
}
