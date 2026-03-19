namespace TecmoSBGame.Flow;

/// <summary>
/// High-level screen/state flow for the MonoGame runtime.
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
