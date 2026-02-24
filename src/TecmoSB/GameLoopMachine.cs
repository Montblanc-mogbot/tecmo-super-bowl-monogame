namespace TecmoSB;

/// <summary>
/// Minimal runner for a YAML-defined game-loop state machine.
///
/// This is intentionally simple: it provides a stable abstraction to hang future
/// systems on (menus, sim, on-field gameplay, etc.), while we incrementally port
/// banks.
/// </summary>
public sealed class GameLoopMachine
{
    private readonly GameLoopConfig _config;

    public GameLoopMachine(GameLoopConfig config)
    {
        _config = config;
        CurrentStateId = config.InitialState;
    }

    public string CurrentStateId { get; private set; }
    public int TickCount { get; private set; }

    public GameLoopState CurrentState => _config.States[CurrentStateId];

    public GameLoopSnapshot Snapshot() => new(_config.Id, CurrentStateId, TickCount);

    /// <summary>
    /// Advances the loop one tick. If the current state has a configured "next",
    /// we will transition immediately.
    /// </summary>
    public void Tick()
    {
        TickCount++;

        var next = CurrentState.Next;
        if (!string.IsNullOrWhiteSpace(next))
        {
            TransitionTo(next);
        }
    }

    /// <summary>
    /// Raises a named event, which may cause a transition based on the current
    /// state's on_event mapping.
    /// </summary>
    public bool RaiseEvent(string eventName)
    {
        if (CurrentState.OnEvent.TryGetValue(eventName, out var next))
        {
            TransitionTo(next);
            return true;
        }

        return false;
    }

    public void TransitionTo(string stateId)
    {
        if (!_config.States.ContainsKey(stateId))
            throw new InvalidOperationException($"Unknown state id '{stateId}' in game loop '{_config.Id}'");

        CurrentStateId = stateId;
    }
}
