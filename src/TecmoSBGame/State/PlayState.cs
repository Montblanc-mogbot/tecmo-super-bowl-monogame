using System;

namespace TecmoSBGame.State;

/// <summary>
/// Pure data model representing the current play state.
///
/// Keep this shallow, deterministic, and free of MonoGame/ECS dependencies so it can be shared
/// across runtime, headless simulation, tests, and future state/RAM decoders.
/// </summary>
public sealed class PlayState
{
    /// <summary>
    /// Identifier for the play within the match. By convention this can align with <see cref="MatchState.PlayNumber"/> + 1.
    /// </summary>
    public int PlayId { get; set; } = 0;

    /// <summary>
    /// Phase within the play lifecycle (pre-snap / in-play / post-play).
    /// </summary>
    public PlayPhase Phase { get; set; } = PlayPhase.PreSnap;

    /// <summary>
    /// Seconds remaining on the play clock (between plays). This is a placeholder for now.
    /// </summary>
    public int PlayClockSecondsRemaining { get; set; } = 15;

    /// <summary>
    /// Seconds elapsed since this play began (simulation time).
    /// </summary>
    public float PlayElapsedSeconds { get; set; } = 0f;

    /// <summary>
    /// Placeholder ball state (held / in air / loose / dead).
    /// </summary>
    public BallState BallState { get; set; } = BallState.Dead;

    /// <summary>
    /// Entity id of the current ball owner (ball carrier / receiver / etc). Null when the ball is not possessed.
    /// </summary>
    public int? BallOwnerEntityId { get; set; } = null;

    /// <summary>
    /// Why the whistle blew to end the play (if ended).
    /// </summary>
    public WhistleReason WhistleReason { get; set; } = WhistleReason.None;

    /// <summary>
    /// Summary of the play's outcome.
    /// </summary>
    public PlayResult Result { get; set; } = default;

    /// <summary>
    /// Absolute field yard (0..100 from the left goal line) where the play began.
    /// </summary>
    public int StartAbsoluteYard { get; set; } = 0;

    /// <summary>
    /// Absolute field yard (0..100 from the left goal line) where the play ended.
    /// </summary>
    public int EndAbsoluteYard { get; set; } = 0;

    public bool IsOver => WhistleReason != WhistleReason.None;

    public void ResetForNewPlay(int playId, int startAbsoluteYard, int playClockSecondsRemaining = 15)
    {
        PlayId = playId;
        Phase = PlayPhase.PreSnap;
        PlayClockSecondsRemaining = playClockSecondsRemaining;
        PlayElapsedSeconds = 0f;

        BallState = BallState.Dead;
        BallOwnerEntityId = null;

        WhistleReason = WhistleReason.None;
        Result = default;

        StartAbsoluteYard = Math.Clamp(startAbsoluteYard, 0, 100);
        EndAbsoluteYard = StartAbsoluteYard;
    }

    public string ToSummaryString()
    {
        var owner = BallOwnerEntityId is null ? "none" : BallOwnerEntityId.Value.ToString();
        return $"playId={PlayId} phase={Phase} playClock={PlayClockSecondsRemaining}s elapsed={PlayElapsedSeconds:0.000}s | ball={BallState} owner={owner} | whistle={WhistleReason} | result: {Result.ToSummaryString()} | start={StartAbsoluteYard} end={EndAbsoluteYard}";
    }

    public static int ToAbsoluteYard(BallSpot spot, OffenseDirection dir)
    {
        // Convert the offense-relative BallSpot into an absolute yard (0..100 from the left goal line).
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

    public static int DistFromOwnGoal(int absoluteYard0To100, OffenseDirection dir)
    {
        absoluteYard0To100 = Math.Clamp(absoluteYard0To100, 0, 100);
        return dir == OffenseDirection.LeftToRight
            ? absoluteYard0To100
            : 100 - absoluteYard0To100;
    }
}

public enum PlayPhase
{
    PreSnap = 0,
    InPlay = 1,
    PostPlay = 2,
}

public enum BallState
{
    Dead = 0,
    Held = 1,
    InAir = 2,
    Loose = 3,
}

public enum WhistleReason
{
    None = 0,
    Tackle = 1,
    OutOfBounds = 2,
    Touchdown = 3,
    Safety = 4,
    Incomplete = 5,
    Turnover = 6,
    Other = 99,
}

public readonly record struct PlayResult(
    int YardsGained,
    bool Turnover,
    bool Touchdown,
    bool Safety)
{
    public string ToSummaryString()
    {
        return $"yards={YardsGained} turnover={Turnover} TD={Touchdown} safety={Safety}";
    }
}
