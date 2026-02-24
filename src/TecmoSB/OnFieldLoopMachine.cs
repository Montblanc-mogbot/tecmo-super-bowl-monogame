namespace TecmoSB;

/// <summary>
/// Minimal runner for a YAML-defined on-field loop.
///
/// Later we can attach systems (input, AI, physics/collision, refs/whistle, camera, animations)
/// by reacting to state transitions + events.
/// </summary>
public sealed class OnFieldLoopMachine
{
    private readonly OnFieldLoopConfig _config;

    public OnFieldLoopMachine(OnFieldLoopConfig config)
    {
        _config = config;
        CurrentStateId = config.InitialState;
    }

    public string CurrentStateId { get; private set; }
    public int TickCount { get; private set; }

    public OnFieldState CurrentState => _config.States[CurrentStateId];

    public OnFieldLoopSnapshot Snapshot() => new(_config.Id, CurrentStateId, TickCount);

    public void Tick()
    {
        TickCount++;

        var next = CurrentState.Next;
        if (!string.IsNullOrWhiteSpace(next))
        {
            TransitionTo(next);
        }
    }

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
            throw new InvalidOperationException($"Unknown state id '{stateId}' in on-field loop '{_config.Id}'");

        CurrentStateId = stateId;
    }
}
