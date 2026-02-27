using Microsoft.Xna.Framework;

namespace TecmoSBGame.Components;

public enum BallFlightKind
{
    Kickoff,
    Punt,
    Pass,
}

/// <summary>
/// Simple deterministic "ball in flight" model.
///
/// XY moves linearly from StartPos -> EndPos over Duration seconds.
/// A separate Height scalar is computed as a parabola for future rendering:
///   h(s) = 4 * ApexHeight * s * (1-s), where s in [0,1].
///
/// Game rules (catching/ownership) are handled elsewhere.
/// </summary>
public sealed class BallFlightComponent
{
    public BallFlightKind Kind;

    public Vector2 StartPos;
    public Vector2 EndPos;

    public float ElapsedSeconds;
    public float DurationSeconds;

    public float ApexHeight;

    /// <summary>
    /// Visual-only height scalar (same units as ApexHeight).
    /// </summary>
    public float Height;

    public bool IsComplete;

    public BallFlightComponent(BallFlightKind kind, Vector2 startPos, Vector2 endPos, float durationSeconds, float apexHeight)
    {
        Kind = kind;
        StartPos = startPos;
        EndPos = endPos;
        DurationSeconds = durationSeconds;
        ApexHeight = apexHeight;
        ElapsedSeconds = 0f;
        Height = 0f;
        IsComplete = false;
    }
}
