using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

public enum RushAssignment
{
    AGapLeft = 0,
    AGapRight = 1,
    BGapLeft = 2,
    BGapRight = 3,
    EdgeLeft = 4,
    EdgeRight = 5,
}

/// <summary>
/// Defensive rush assignment + state for SimArch.
/// </summary>
public struct Rush
{
    public RushAssignment Assignment;

    public bool HasLandmark;
    public Vector2 Landmark;

    public bool ReachedLandmark;
}
