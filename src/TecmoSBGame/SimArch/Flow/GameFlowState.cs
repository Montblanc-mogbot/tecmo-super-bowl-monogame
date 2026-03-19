namespace TecmoSBGame.SimArch.Flow;

/// <summary>
/// High-level screen/state flow.
///
/// Ported from: ArchiveMge/Flow/GameFlowState.cs
/// </summary>
public enum GameFlowState
{
    Title = 0,
    MainMenu = 1,
    TeamSelect = 2,
    CoinToss = 3,
    Kickoff = 4,
    OnField = 5,
    PostPlay = 6,
}
