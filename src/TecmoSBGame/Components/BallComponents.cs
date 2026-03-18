using Microsoft.Xna.Framework;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Components;

/// <summary>
/// Ball entity state.
///
/// NOTE: We intentionally consolidate ball-related state into a single component to avoid
/// hitting MonoGame.Extended.Entities' 32 component-type limit.
/// </summary>
public sealed class BallComponent
{
    public BallState State;

    public int? OwnerEntityId;

    // Flight (kickoff/punt/pass)
    public BallFlightKind FlightKind;

    public int? PasserId;
    public int? TargetId;
    public PassType PassType;

    public Vector2 StartPos;
    public Vector2 EndPos;

    public float ElapsedSeconds;
    public float DurationSeconds;

    public float ApexHeight;
    public float Height;

    public bool IsComplete;

    public BallComponent(BallState state)
    {
        State = state;
        OwnerEntityId = null;
        FlightKind = BallFlightKind.None;
        PasserId = null;
        TargetId = null;
        PassType = PassType.Bullet;
        StartPos = Vector2.Zero;
        EndPos = Vector2.Zero;
        ElapsedSeconds = 0f;
        DurationSeconds = 0f;
        ApexHeight = 0f;
        Height = 0f;
        IsComplete = false;
    }
}
