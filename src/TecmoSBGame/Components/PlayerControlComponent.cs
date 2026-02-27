namespace TecmoSBGame.Components;

/// <summary>
/// Per-entity marker/state for the currently user-controlled player.
///
/// Note: <see cref="TeamComponent.IsPlayerControlled"/> indicates the human-controlled TEAM,
/// while this component indicates which single entity currently receives movement input.
/// </summary>
public sealed class PlayerControlComponent
{
    public bool IsControlled;
}
