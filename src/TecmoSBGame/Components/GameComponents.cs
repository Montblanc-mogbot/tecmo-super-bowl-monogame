using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;

namespace TecmoSBGame.Components;

/// <summary>
/// Position component for entities on the field.
/// Uses NES-resolution coordinates (256x224 field).
/// </summary>
public class PositionComponent
{
    public Vector2 Position;
    
    public PositionComponent(float x, float y)
    {
        Position = new Vector2(x, y);
    }
    
    public PositionComponent(Vector2 position)
    {
        Position = position;
    }
}

/// <summary>
/// Velocity component for movement.
/// Tecmo-style: instant direction changes, speed ramps to max.
/// </summary>
public class VelocityComponent
{
    public Vector2 Velocity;
    public float MaxSpeed;
    public float Acceleration;
    
    public VelocityComponent(float maxSpeed, float acceleration = 0.4f)
    {
        Velocity = Vector2.Zero;
        MaxSpeed = maxSpeed;
        Acceleration = acceleration;
    }
}

/// <summary>
/// Player attributes from team data.
/// </summary>
public class PlayerAttributesComponent
{
    public int Hp;  // Hitting Power
    public int Rs;  // Running Speed
    public int Ms;  // Maximum Speed
    public int Rp;  // Running Power
    public int Bc;  // Ball Control
    public int Rec; // Receiving
    public int Pa;  // Pass Accuracy
    public int Ar;  // Avoid Rush
    public int Kp;  // Kicking Power
    public int Kab; // Kicking Accuracy
    
    public string Position = "";
    public string Name = "";
    public int JerseyNumber;
}

/// <summary>
/// Team affiliation for rendering and gameplay.
/// </summary>
public class TeamComponent
{
    public int TeamIndex;
    public bool IsPlayerControlled;
    public bool IsOffense;
}

/// <summary>
/// Sprite rendering component.
/// </summary>
public class SpriteComponent
{
    public string SpriteId;
    public Color Tint;
    public float Rotation;
    public Vector2 Scale;
    public bool FlipHorizontal;
    
    public SpriteComponent(string spriteId)
    {
        SpriteId = spriteId;
        Tint = Color.White;
        Rotation = 0f;
        Scale = Vector2.One;
        FlipHorizontal = false;
    }
}

/// <summary>
/// Ball carrier state.
/// </summary>
public class BallCarrierComponent
{
    public bool HasBall;
    public float YardsAfterCatch;
}

/// <summary>
/// Behavior/AI state for players.
/// </summary>
public class BehaviorComponent
{
    public BehaviorState State;
    public float StateTimer;
    public Vector2 TargetPosition;
    public int TargetEntityId;
}

public enum BehaviorState
{
    Idle,
    MovingToPosition,
    TrackingPlayer,
    RushingQB,
    Blocking,
    RunningRoute,

    // Interrupt-style states (typically restored via BehaviorStackComponent).
    Engaged,
    Tackling,
    Grappling,
}
