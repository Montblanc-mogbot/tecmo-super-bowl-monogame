using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TecmoSBGame.Persistence;

public sealed class SeasonPresentationService
{
    public void RefreshDerivedState(SeasonState season)
    {
        ArgumentNullException.ThrowIfNull(season);

        season.Standings = season.Standings
            .OrderByDescending(r => r.Wins)
            .ThenBy(r => r.Losses)
            .ThenByDescending(r => r.PointDifferential)
            .ThenBy(r => r.TeamId)
            .ToList();

        season.Leaders = BuildLeaders(season);
        season.Records = BuildRecords(season);
        season.Playoffs = BuildPlayoffs(season);
        season.ProBowl = BuildProBowl(season);
        season.ChampionSummary = season.Playoffs.Champion;
    }

    public string RenderHub(SeasonState season)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"SEASON SLOT {season.SlotName}");
        sb.AppendLine($"WEEK {season.CurrentWeek}/{season.TotalWeeks}");
        sb.AppendLine(season.CurrentWeek > season.TotalWeeks ? "STATUS: POSTSEASON" : "STATUS: REGULAR SEASON");
        if (!string.IsNullOrWhiteSpace(season.Playoffs.ChampionshipMatchup))
            sb.AppendLine($"PLAYOFFS: {season.Playoffs.ChampionshipMatchup}");
        if (!string.IsNullOrWhiteSpace(season.Playoffs.Champion))
            sb.AppendLine($"CHAMPION: {season.Playoffs.Champion}");
        sb.AppendLine();
        sb.Append(RenderWeekSummary(season));
        return sb.ToString().TrimEnd();
    }

    public string RenderWeekSummary(SeasonState season)
    {
        var week = season.GetWeek(Math.Min(season.CurrentWeek, Math.Max(1, season.TotalWeeks)));
        var sb = new StringBuilder();
        sb.AppendLine("SCHEDULE");
        if (week is null)
        {
            sb.AppendLine("  NO ACTIVE WEEK");
            return sb.ToString().TrimEnd();
        }

        foreach (var game in week.Games)
        {
            if (game.Result is null)
                sb.AppendLine($"  W{game.Week} G{game.GameNumber}: T{game.AwayTeamId:00} @ T{game.HomeTeamId:00}");
            else
                sb.AppendLine($"  W{game.Week} G{game.GameNumber}: T{game.Result.AwayTeamId:00} {game.Result.AwayScore} @ T{game.Result.HomeTeamId:00} {game.Result.HomeScore}");
        }

        return sb.ToString().TrimEnd();
    }

    public string RenderStandings(SeasonState season)
    {
        var sb = new StringBuilder();
        sb.AppendLine("STANDINGS");
        var rank = 1;
        foreach (var team in season.Standings)
        {
            sb.AppendLine($"  {rank}. {team.TeamCode} {team.Wins}-{team.Losses}-{team.Ties} PF:{team.PointsFor} PA:{team.PointsAgainst} DIFF:{team.PointDifferential:+#;-#;0}");
            rank++;
        }

        return sb.ToString().TrimEnd();
    }

    public string RenderLeaders(SeasonState season)
    {
        var sb = new StringBuilder();
        sb.AppendLine("LEADERS");
        foreach (var entry in season.Leaders.Entries)
            sb.AppendLine($"  {entry.Category}: {entry.TeamCode} {entry.Value} ({entry.Detail})");
        return sb.ToString().TrimEnd();
    }

    public string RenderRecords(SeasonState season)
    {
        var sb = new StringBuilder();
        sb.AppendLine("RECORDS");
        foreach (var entry in season.Records.Entries)
            sb.AppendLine($"  {entry.Label}: {entry.TeamCode} {entry.Value} ({entry.Detail})");
        return sb.ToString().TrimEnd();
    }

    public string RenderPlayoffs(SeasonState season)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PLAYOFF PICTURE");
        foreach (var seed in season.Playoffs.Seeds)
            sb.AppendLine($"  #{seed.Seed} {seed.TeamCode} {seed.Wins}-{seed.Losses}-{seed.Ties}");
        if (!string.IsNullOrWhiteSpace(season.Playoffs.ChampionshipMatchup))
            sb.AppendLine($"  TITLE GAME: {season.Playoffs.ChampionshipMatchup}");
        if (!string.IsNullOrWhiteSpace(season.Playoffs.Champion))
            sb.AppendLine($"  CHAMPION: {season.Playoffs.Champion}");
        return sb.ToString().TrimEnd();
    }

    public string RenderProBowl(SeasonState season)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PRO BOWL");
        if (!season.ProBowl.IsReady)
        {
            sb.AppendLine("  NOT READY");
            return sb.ToString().TrimEnd();
        }

        sb.AppendLine($"  ALL-STARS: {string.Join(", ", season.ProBowl.TeamCodes)}");
        return sb.ToString().TrimEnd();
    }

    private static SeasonLeadersSnapshot BuildLeaders(SeasonState season)
    {
        var entries = new List<SeasonLeaderEntry>();
        var topWinPct = season.Standings.OrderByDescending(s => s.WinPercentage).ThenByDescending(s => s.PointDifferential).FirstOrDefault();
        var topPoints = season.Standings.OrderByDescending(s => s.PointsFor).ThenByDescending(s => s.WinPercentage).FirstOrDefault();
        var topDefense = season.Standings.OrderBy(s => s.PointsAgainst).ThenByDescending(s => s.WinPercentage).FirstOrDefault();

        if (topWinPct is not null)
            entries.Add(new SeasonLeaderEntry { Category = "BEST RECORD", TeamId = topWinPct.TeamId, TeamCode = topWinPct.TeamCode, Value = topWinPct.Wins, Detail = $"{topWinPct.Wins}-{topWinPct.Losses}-{topWinPct.Ties}" });
        if (topPoints is not null)
            entries.Add(new SeasonLeaderEntry { Category = "TOP OFFENSE", TeamId = topPoints.TeamId, TeamCode = topPoints.TeamCode, Value = topPoints.PointsFor, Detail = $"PF {topPoints.PointsFor}" });
        if (topDefense is not null)
            entries.Add(new SeasonLeaderEntry { Category = "TOP DEFENSE", TeamId = topDefense.TeamId, TeamCode = topDefense.TeamCode, Value = topDefense.PointsAgainst, Detail = $"PA {topDefense.PointsAgainst}" });

        return new SeasonLeadersSnapshot { Entries = entries };
    }

    private static SeasonRecordBook BuildRecords(SeasonState season)
    {
        var entries = new List<SeasonRecordEntry>();
        var highestPf = season.Standings.OrderByDescending(s => s.PointsFor).FirstOrDefault();
        var bestDiff = season.Standings.OrderByDescending(s => s.PointDifferential).FirstOrDefault();
        var mostWins = season.Standings.OrderByDescending(s => s.Wins).ThenBy(s => s.Losses).FirstOrDefault();

        if (highestPf is not null)
            entries.Add(new SeasonRecordEntry { Label = "MOST POINTS", TeamId = highestPf.TeamId, TeamCode = highestPf.TeamCode, Value = highestPf.PointsFor, Detail = $"PA {highestPf.PointsAgainst}" });
        if (bestDiff is not null)
            entries.Add(new SeasonRecordEntry { Label = "BEST DIFF", TeamId = bestDiff.TeamId, TeamCode = bestDiff.TeamCode, Value = bestDiff.PointDifferential, Detail = $"PF {bestDiff.PointsFor} / PA {bestDiff.PointsAgainst}" });
        if (mostWins is not null)
            entries.Add(new SeasonRecordEntry { Label = "MOST WINS", TeamId = mostWins.TeamId, TeamCode = mostWins.TeamCode, Value = mostWins.Wins, Detail = $"{mostWins.Wins}-{mostWins.Losses}-{mostWins.Ties}" });

        return new SeasonRecordBook { Entries = entries };
    }

    private static SeasonPlayoffState BuildPlayoffs(SeasonState season)
    {
        var ordered = season.Standings
            .OrderByDescending(s => s.Wins)
            .ThenBy(s => s.Losses)
            .ThenByDescending(s => s.PointDifferential)
            .ThenBy(s => s.TeamId)
            .ToList();

        var seeds = ordered.Take(Math.Min(4, ordered.Count))
            .Select((team, index) => new SeasonPlayoffSeed
            {
                Seed = index + 1,
                TeamId = team.TeamId,
                TeamCode = team.TeamCode,
                Wins = team.Wins,
                Losses = team.Losses,
                Ties = team.Ties,
            })
            .ToList();

        var locked = season.CurrentWeek > season.TotalWeeks;
        var championship = seeds.Count >= 2 ? $"{seeds[0].TeamCode} vs {seeds[1].TeamCode}" : string.Empty;
        var champion = locked && seeds.Count > 0 ? seeds[0].TeamCode : string.Empty;

        return new SeasonPlayoffState
        {
            IsLocked = locked,
            ChampionshipResolved = locked && seeds.Count > 0,
            Seeds = seeds,
            ChampionshipMatchup = championship,
            Champion = champion,
        };
    }

    private static SeasonProBowlState BuildProBowl(SeasonState season)
    {
        var ready = season.CurrentWeek > season.TotalWeeks;
        var selections = season.Standings.Take(Math.Min(2, season.Standings.Count)).ToList();
        return new SeasonProBowlState
        {
            IsReady = ready,
            TeamIds = selections.Select(s => s.TeamId).ToList(),
            TeamCodes = selections.Select(s => s.TeamCode).ToList(),
        };
    }
}
