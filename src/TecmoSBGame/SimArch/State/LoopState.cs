namespace TecmoSBGame.SimArch.State;

/// <summary>
/// Loop machine state (game loop + on-field loop).
///
/// Ported from: src/TecmoSBGame/ArchiveMge/State/LoopState.cs
/// </summary>
public sealed class LoopState
{
    public int GameStateId;
    public int OnFieldStateId;
}
