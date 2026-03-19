namespace TecmoSBGame.SimArch.State;

/// <summary>
/// Coordinates MatchState + PlayState + loop transitions.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/State/GameStateManager.cs
/// </summary>
public sealed class GameStateManager
{
    public MatchState Match { get; }
    public PlayState Play { get; }

    public GameStateManager(MatchState match, PlayState play)
    {
        Match = match;
        Play = play;
    }
}
