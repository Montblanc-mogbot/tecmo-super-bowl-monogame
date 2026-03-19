using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

public enum BehaviorState
{
    Idle = 0,
    MovingToPosition = 1,
    TrackingEntity = 2,

    // Interrupt / contact states (scaffolding)
    Engaged = 3,
    Tackling = 4,
    Grappling = 5,
}

public struct Behavior
{
    public BehaviorState State;
    public int TargetEntityId; // Arch Entity.Id
    public Vector2 TargetPosition;
    public float StateTimer;
}
