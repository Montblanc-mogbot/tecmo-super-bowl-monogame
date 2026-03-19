using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

public enum BallFlightKind
{
    None = 0,
    Kickoff = 1,
    Punt = 2,
    Pass = 3,
}

public struct Ball
{
    public BallState State;
    public int OwnerEntityId; // 0 = none

    public BallFlightKind FlightKind;
    public Vector2 StartPos;
    public Vector2 EndPos;
    public float ElapsedSeconds;
    public float DurationSeconds;
    public float ApexHeight;
    public float Height;
    public bool IsComplete;
}
