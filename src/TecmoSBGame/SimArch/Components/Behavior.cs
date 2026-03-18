using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

public enum BehaviorState
{
    Idle = 0,
    MovingToPosition = 1,
    TrackingEntity = 2,
}

public struct Behavior
{
    public BehaviorState State;
    public int TargetEntityId; // Arch Entity.Id
    public Vector2 TargetPosition;
    public float StateTimer;
}
