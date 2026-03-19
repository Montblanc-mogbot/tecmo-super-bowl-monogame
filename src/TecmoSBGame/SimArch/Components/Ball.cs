using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

public struct Ball
{
    public BallState State;

    // 0 = none (Arch may allocate ids starting at 0; Sim bootstrap ensures real entities start at 1+)
    public int OwnerEntityId;

    // Flight (kickoff/punt/pass)
    public BallFlightKind FlightKind;

    public int PasserEntityId; // 0 = none
    public int TargetEntityId; // 0 = none
    public PassType PassType;

    public Vector2 StartPos;
    public Vector2 EndPos;

    public float ElapsedSeconds;
    public float DurationSeconds;

    public float ApexHeight;
    public float Height;

    public bool IsComplete;
}
