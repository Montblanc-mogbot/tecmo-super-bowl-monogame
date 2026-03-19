namespace TecmoSBGame.SimArch.State;

/// <summary>
/// Per-play rules state.
///
/// Ported from: ArchiveMge/State/PlayState.cs (scaffold)
/// </summary>
public sealed class PlayState
{
    public int PlayId;

    public int StartAbsoluteYard;
    public int EndAbsoluteYard;

    public uint DeterministicSeed;

    public PlayPhase Phase;
    public float PlayElapsedSeconds;

    public WhistleReason WhistleReason;

    public PlayResult Result;

    public bool IsOver => Phase == PlayPhase.PostPlay;

    public void ResetForNewPlay(int playId, int startAbsoluteYard)
    {
        PlayId = playId;
        StartAbsoluteYard = startAbsoluteYard;
        EndAbsoluteYard = startAbsoluteYard;

        Phase = PlayPhase.PreSnap;
        PlayElapsedSeconds = 0f;

        WhistleReason = WhistleReason.None;
        Result = new PlayResult(0, false, false, false);
    }

    public static int ToAbsoluteYard(BallSpot spot, OffenseDirection dir)
    {
        // Convert offense-relative spot into absolute 0..100.
        var abs = spot.OnOwnSide ? spot.Yards : 100 - spot.Yards;
        return dir == OffenseDirection.LeftToRight ? abs : 100 - abs;
    }
}

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
    Incomplete = 5,
    Touchback = 6,
}

public readonly record struct PlayResult(int YardsGained, bool Turnover, bool Touchdown, bool Safety);
