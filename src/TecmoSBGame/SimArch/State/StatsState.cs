using System.Collections.Generic;

namespace TecmoSBGame.SimArch.State;

public sealed class StatsState
{
    public MatchStats Match { get; } = new();
    public PlayStats CurrentPlay { get; } = new();
    public List<StatEventRecord> EventLog { get; } = new();

    public void ResetForNewPlay(int playId)
    {
        CurrentPlay.Reset(playId);
        EventLog.Clear();
    }

    public void Record(StatEventRecord record)
    {
        EventLog.Add(record);
    }
}

public sealed class MatchStats
{
    public Dictionary<int, TeamStats> Teams { get; } = new();
    public Dictionary<int, PlayerStats> Players { get; } = new();

    public TeamStats GetTeam(int teamIndex)
    {
        if (!Teams.TryGetValue(teamIndex, out var team))
        {
            team = new TeamStats();
            Teams[teamIndex] = team;
        }

        return team;
    }

    public PlayerStats GetPlayer(int playerId)
    {
        if (!Players.TryGetValue(playerId, out var player))
        {
            player = new PlayerStats();
            Players[playerId] = player;
        }

        return player;
    }
}

public sealed class PlayStats
{
    public int PlayId { get; private set; }
    public int RushingYards { get; set; }
    public int PassingYards { get; set; }
    public bool Turnover { get; set; }
    public int? BallCarrierId { get; set; }
    public int? PasserId { get; set; }
    public int? ReceiverId { get; set; }
    public int? InterceptorId { get; set; }

    public void Reset(int playId)
    {
        PlayId = playId;
        RushingYards = 0;
        PassingYards = 0;
        Turnover = false;
        BallCarrierId = null;
        PasserId = null;
        ReceiverId = null;
        InterceptorId = null;
    }
}

public sealed class TeamStats
{
    public int RushingAttempts { get; set; }
    public int RushingYards { get; set; }
    public int PassAttempts { get; set; }
    public int PassCompletions { get; set; }
    public int PassingYards { get; set; }
    public int TurnoversCommitted { get; set; }
    public int TurnoversForced { get; set; }
    public int Interceptions { get; set; }
    public int FumbleRecoveries { get; set; }
}

public sealed class PlayerStats
{
    public int RushingAttempts { get; set; }
    public int RushingYards { get; set; }
    public int PassAttempts { get; set; }
    public int PassCompletions { get; set; }
    public int PassingYards { get; set; }
    public int Receptions { get; set; }
    public int ReceivingYards { get; set; }
    public int InterceptionsThrown { get; set; }
    public int InterceptionsCaught { get; set; }
    public int FumblesRecovered { get; set; }
    public int TurnoversCommitted { get; set; }
    public int TurnoversForced { get; set; }
}

public readonly record struct StatEventRecord(
    int PlayId,
    string EventType,
    int TeamIndex,
    int? PlayerId,
    int Yards,
    bool Turnover,
    string Detail);
