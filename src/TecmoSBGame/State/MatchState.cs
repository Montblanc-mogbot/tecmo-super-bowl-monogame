using System;

namespace TecmoSBGame.State;

/// <summary>
/// Pure data model representing the current match state (score/clock/possession/ball spot/etc).
///
/// Intentionally has no MonoGame or ECS dependencies so it can be shared across runtime,
/// headless simulation, tests, and future RAM/state decoders.
/// </summary>
public sealed class MatchState
{
    // ---- Core match fields (keep simple; systems can interpret/validate as needed) ----

    /// <summary>1..4 (OT later).</summary>
    public int Quarter { get; set; } = 1;

    /// <summary>Seconds remaining in the current quarter.</summary>
    public int GameClockSeconds { get; set; } = 5 * 60;

    /// <summary>0-based team index that currently has possession.</summary>
    public int PossessionTeam { get; set; } = 0;

    /// <summary>Direction of the offense for the current possession.</summary>
    public OffenseDirection OffenseDirection { get; set; } = OffenseDirection.LeftToRight;

    /// <summary>1..4 (and beyond for penalties; keep as data).</summary>
    public int Down { get; set; } = 1;

    /// <summary>Yards needed for a first down (typically 10; goal-to-go handled later).</summary>
    public int YardsToGo { get; set; } = 10;

    /// <summary>Where the ball is spotted for the next snap.</summary>
    public BallSpot BallSpot { get; set; } = BallSpot.Own(25);

    /// <summary>Score for team 0.</summary>
    public int Team0Score { get; set; } = 0;

    /// <summary>Score for team 1.</summary>
    public int Team1Score { get; set; } = 0;

    /// <summary>Monotonic play counter (increments when a play ends).</summary>
    public int PlayNumber { get; set; } = 0;

    /// <summary>Optional drive id (increments on possession changes, etc.).</summary>
    public int DriveId { get; set; } = 0;

    // ---- Small helpers (avoid encoding deep game rules at this stage) ----

    public int GetScore(int teamIndex) => teamIndex == 0 ? Team0Score : Team1Score;

    public void AddScore(int teamIndex, int points)
    {
        if (teamIndex == 0)
            Team0Score += points;
        else
            Team1Score += points;
    }

    /// <summary>
    /// Convenience setup for a kickoff slice: receiving team starts with the ball at its own 25.
    /// </summary>
    public void ResetForKickoff(int kickingTeam, int receivingTeam, int touchbackYardLine = 25)
    {
        PossessionTeam = receivingTeam;
        OffenseDirection = receivingTeam == 0 ? OffenseDirection.LeftToRight : OffenseDirection.RightToLeft;

        Down = 1;
        YardsToGo = 10;

        BallSpot = BallSpot.Own(touchbackYardLine);
    }

    public void SpotBall(BallSpot spot) => BallSpot = spot;

    /// <summary>
    /// Spots the ball from an absolute field yard (0..100 from the left goal line),
    /// converting into Own/Opp/Midfield relative to the current offense direction.
    /// </summary>
    public void SpotBallAbsoluteYard(int absoluteYard0To100)
    {
        BallSpot = BallSpot.FromAbsoluteYard(absoluteYard0To100, OffenseDirection);
    }

    /// <summary>
    /// Minimal down/distance progression helper. Does not handle turnovers, penalties,
    /// goal-to-go, etc. Those will be introduced by dedicated systems.
    /// </summary>
    public void AdvanceDownDistance(int yardsGained, bool firstDown)
    {
        if (firstDown)
        {
            Down = 1;
            YardsToGo = 10;
            return;
        }

        Down = Math.Max(1, Down + 1);
        YardsToGo = Math.Max(0, YardsToGo - yardsGained);
    }

    public string ToSummaryString()
    {
        return $"Q{Quarter} {FormatClock(GameClockSeconds)} | poss=T{PossessionTeam} {OffenseDirection} | {FormatDownDistance()} @ {BallSpot} | score {Team0Score}-{Team1Score} | play#{PlayNumber} drive#{DriveId}";
    }

    public static string FormatClock(int seconds)
    {
        seconds = Math.Max(0, seconds);
        var m = seconds / 60;
        var s = seconds % 60;
        return $"{m}:{s:D2}";
    }

    public string FormatDownDistance()
    {
        static string DownSuffix(int d) => d switch
        {
            1 => "1st",
            2 => "2nd",
            3 => "3rd",
            _ => $"{d}th",
        };

        return $"{DownSuffix(Down)}&{YardsToGo}";
    }
}

public enum OffenseDirection
{
    LeftToRight = 0,
    RightToLeft = 1,
}

public readonly record struct BallSpot(FieldSide Side, int YardLine)
{
    /// <summary>
    /// YardLine is 0..50. Side is relative to the offense (Own/Opp/Midfield).
    /// </summary>
    public static BallSpot Own(int yardLine) => new(FieldSide.Own, Clamp0To50(yardLine));
    public static BallSpot Opp(int yardLine) => new(FieldSide.Opp, Clamp0To50(yardLine));
    public static BallSpot Midfield() => new(FieldSide.Midfield, 50);

    public static BallSpot FromAbsoluteYard(int absoluteYard0To100, OffenseDirection dir)
    {
        absoluteYard0To100 = Math.Clamp(absoluteYard0To100, 0, 100);

        // Absolute yard is from the left goal line. Convert to distance from offense's own goal.
        var distFromOwnGoal = dir == OffenseDirection.LeftToRight
            ? absoluteYard0To100
            : 100 - absoluteYard0To100;

        if (distFromOwnGoal == 50)
            return Midfield();

        if (distFromOwnGoal < 50)
            return Own(distFromOwnGoal);

        return Opp(100 - distFromOwnGoal);
    }

    private static int Clamp0To50(int yardLine) => Math.Clamp(yardLine, 0, 50);

    public override string ToString() => Side switch
    {
        FieldSide.Own => $"OWN {YardLine}",
        FieldSide.Opp => $"OPP {YardLine}",
        FieldSide.Midfield => "50",
        _ => $"{Side} {YardLine}",
    };
}

public enum FieldSide
{
    Own = 0,
    Opp = 1,
    Midfield = 2,
}
