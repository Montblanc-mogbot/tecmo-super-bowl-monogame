using TecmoSBGame.State;

namespace TecmoSBGame.Components;

/// <summary>
/// Tag + state for the dedicated ball entity.
///
/// The intent is that the ball is an entity with its own position/velocity and a small amount of
/// ball-specific state (held / in air / loose / dead) plus an optional owner entity id.
/// </summary>
public sealed class BallComponent
{
}

public sealed class BallStateComponent
{
    public BallState State;

    public BallStateComponent(BallState state)
    {
        State = state;
    }
}

public sealed class BallOwnerComponent
{
    public int? OwnerEntityId;

    public BallOwnerComponent(int? ownerEntityId)
    {
        OwnerEntityId = ownerEntityId;
    }
}
