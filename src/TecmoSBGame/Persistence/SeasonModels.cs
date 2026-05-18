using System;
using System.Collections.Generic;
using System.Linq;

namespace TecmoSBGame.Persistence;

public enum SeasonGameOutcome
{
    Scheduled = 0,
    HomeWin = 1,
    AwayWin = 2,
    Tie = 3,
}

public sealed class SeasonTeamRecord
{
    public int TeamId { get; set; }
    public string TeamCode { get; set; } = string.Empty;
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Ties { get; set; }
    public int PointsFor { get; set; }
    public int PointsAgainst { get; set; }

    public int GamesPlayed => Wins + Losses + Ties;
    public double WinPercentage => GamesPlayed == 0 ? 0d : (Wins + (Ties * 0.5d)) / GamesPlayed;
    public int PointDifferential => PointsFor - PointsAgainst;
}

public sealed class SeasonGameResult
{
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public int? WinningTeamId { get; set; }
    public bool WasSimulated { get; set; }
    public string Source { get; set; } = "exhibition";

    public SeasonGameOutcome Outcome => HomeScore == AwayScore
        ? SeasonGameOutcome.Tie
        : WinningTeamId == HomeTeamId
            ? SeasonGameOutcome.HomeWin
            : SeasonGameOutcome.AwayWin;
}

public sealed class SeasonScheduledGame
{
    public int Week { get; set; }
    public int GameNumber { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public SeasonGameResult? Result { get; set; }
    public bool IsComplete => Result is not null;
}

public sealed class SeasonWeekSchedule
{
    public int Week { get; set; }
    public List<SeasonScheduledGame> Games { get; set; } = new();

    public bool IsComplete => Games.Count > 0 && Games.All(g => g.IsComplete);
}

public sealed class SeasonState
{
    public int SchemaVersion { get; set; } = 2;
    public string SlotName { get; set; } = "season-1";
    public int CurrentWeek { get; set; } = 1;
    public int TotalWeeks { get; set; }
    public List<int> TeamIds { get; set; } = new();
    public List<SeasonWeekSchedule> Schedule { get; set; } = new();
    public List<SeasonTeamRecord> Standings { get; set; } = new();
    public SeasonRecordBook Records { get; set; } = new();
    public SeasonLeadersSnapshot Leaders { get; set; } = new();
    public SeasonPlayoffState Playoffs { get; set; } = new();
    public SeasonProBowlState ProBowl { get; set; } = new();
    public string ChampionSummary { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public SeasonWeekSchedule? GetWeek(int week)
        => Schedule.FirstOrDefault(s => s.Week == week);

    public SeasonTeamRecord GetOrCreateRecord(int teamId)
    {
        var existing = Standings.FirstOrDefault(r => r.TeamId == teamId);
        if (existing is not null)
            return existing;

        var created = new SeasonTeamRecord { TeamId = teamId, TeamCode = $"T{teamId:00}" };
        Standings.Add(created);
        return created;
    }
}

public sealed class SeasonLeaderEntry
{
    public string Category { get; set; } = string.Empty;
    public int TeamId { get; set; }
    public string TeamCode { get; set; } = string.Empty;
    public int Value { get; set; }
    public string Detail { get; set; } = string.Empty;
}

public sealed class SeasonLeadersSnapshot
{
    public List<SeasonLeaderEntry> Entries { get; set; } = new();
}

public sealed class SeasonRecordEntry
{
    public string Label { get; set; } = string.Empty;
    public int TeamId { get; set; }
    public string TeamCode { get; set; } = string.Empty;
    public int Value { get; set; }
    public string Detail { get; set; } = string.Empty;
}

public sealed class SeasonRecordBook
{
    public List<SeasonRecordEntry> Entries { get; set; } = new();
}

public sealed class SeasonPlayoffSeed
{
    public int Seed { get; set; }
    public int TeamId { get; set; }
    public string TeamCode { get; set; } = string.Empty;
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Ties { get; set; }
}

public sealed class SeasonPlayoffState
{
    public bool IsLocked { get; set; }
    public bool ChampionshipResolved { get; set; }
    public List<SeasonPlayoffSeed> Seeds { get; set; } = new();
    public string ChampionshipMatchup { get; set; } = string.Empty;
    public string Champion { get; set; } = string.Empty;
}

public sealed class SeasonProBowlState
{
    public bool IsReady { get; set; }
    public List<int> TeamIds { get; set; } = new();
    public List<string> TeamCodes { get; set; } = new();
}

public sealed class SeasonAdvanceResult
{
    public int AdvancedWeek { get; set; }
    public bool WeekCompleted { get; set; }
    public bool SeasonComplete { get; set; }
    public int GamesApplied { get; set; }
}
