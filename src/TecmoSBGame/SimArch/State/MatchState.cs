using System;

namespace TecmoSBGame.SimArch.State;

/// <summary>
/// Arch-native match model (score/clock/possession/spot/down-distance).
///
/// This intentionally mirrors the legacy MGE MatchState enough to port rules systems,
/// but lives in SimArch namespace to avoid cross-sim coupling.
/// </summary>
public sealed class MatchState
{
    public int Quarter { get; set; } = 1;

    /// <summary>Seconds remaining in the quarter.</summary>
    public int GameClockSeconds { get; set; } = 5 * 60;

    public int PossessionTeam { get; set; } = 0;

    public OffenseDirection OffenseDirection { get; set; } = OffenseDirection.LeftToRight;

    public int Down { get; set; } = 1;

    public int YardsToGo { get; set; } = 10;

    public BallSpot BallSpot { get; set; } = BallSpot.Own(25);

    public int Team0Score { get; set; }

    public int Team1Score { get; set; }

    public int PlayNumber { get; set; }

    public int DriveId { get; set; }

    public bool MatchOver { get; set; }

    public int GetScore(int teamIndex) => teamIndex == 0 ? Team0Score : Team1Score;

    public void AddScore(int teamIndex, int points)
    {
        if (teamIndex == 0) Team0Score += points;
        else Team1Score += points;
    }

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

    public void SpotBall(BallSpot spot) => BallSpot = spot;

    public void SpotBallAbsoluteYard(int absoluteYard0To100)
        => BallSpot = BallSpot.FromAbsoluteYard(absoluteYard0To100, OffenseDirection);

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

    public static string FormatClock(int seconds)
    {
        seconds = Math.Max(0, seconds);
        var m = seconds / 60;
        var s = seconds % 60;
        return $"{m}:{s:D2}";
    }
}

public enum OffenseDirection
{
    LeftToRight = 0,
    RightToLeft = 1,
}

public readonly record struct BallSpot(FieldSide Side, int YardLine)
{
    public static BallSpot Own(int yardLine) => new(FieldSide.Own, Clamp0To50(yardLine));
    public static BallSpot Opp(int yardLine) => new(FieldSide.Opp, Clamp0To50(yardLine));
    public static BallSpot Midfield() => new(FieldSide.Midfield, 50);

    public static BallSpot FromAbsoluteYard(int absoluteYard0To100, OffenseDirection dir)
    {
        absoluteYard0To100 = Math.Clamp(absoluteYard0To100, 0, 100);

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
