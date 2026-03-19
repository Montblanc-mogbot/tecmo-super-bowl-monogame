using System;

namespace TecmoSBGame.SimArch.State;

public enum PlayPhase
{
    PreSnap = 0,
    InPlay = 1,
    PostPlay = 2,
}

public enum WhistleReason
{
    None = 0,
    Tackle = 1,
    OutOfBounds = 2,
    Touchdown = 3,
    Safety = 4,
    Touchback = 5,
    Incomplete = 6,
    Turnover = 7,
    Other = 99,
}

public readonly record struct PlayResult(int YardsGained, bool Turnover, bool Touchdown, bool Safety);

/// <summary>
/// Arch-native play model.
///
/// This will eventually own deterministic seeds/play clock/etc.
/// For now it tracks phase and play elapsed seconds.
/// </summary>
public sealed class PlayState
{
    public int PlayId { get; set; }

    public PlayPhase Phase { get; set; } = PlayPhase.PreSnap;

    public float PlayElapsedSeconds { get; set; }

    public WhistleReason WhistleReason { get; set; } = WhistleReason.None;

    public PlayResult Result { get; set; } = default;

    public int StartAbsoluteYard { get; set; }

    public int EndAbsoluteYard { get; set; }

    public bool IsOver => WhistleReason != WhistleReason.None;

    public void ResetForNewPlay(int playId, int startAbsoluteYard)
    {
        PlayId = playId;
        Phase = PlayPhase.PreSnap;
        PlayElapsedSeconds = 0f;

        WhistleReason = WhistleReason.None;
        Result = default;

        StartAbsoluteYard = Math.Clamp(startAbsoluteYard, 0, 100);
        EndAbsoluteYard = StartAbsoluteYard;
    }

    public static int ToAbsoluteYard(BallSpot spot, OffenseDirection dir)
    {
        var distFromOwnGoal = spot.Side switch
        {
            FieldSide.Midfield => 50,
            FieldSide.Own => Math.Clamp(spot.YardLine, 0, 50),
            FieldSide.Opp => 100 - Math.Clamp(spot.YardLine, 0, 50),
            _ => 50,
        };

        var absolute = dir == OffenseDirection.LeftToRight
            ? distFromOwnGoal
            : 100 - distFromOwnGoal;

        return Math.Clamp(absolute, 0, 100);
    }
}
