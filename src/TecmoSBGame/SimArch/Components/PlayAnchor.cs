namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Ported from: ArchiveMge/Components/PlayScriptComponent.cs (PlayAnchor)
/// </summary>
public enum PlayAnchorKind
{
    None = 0,
    LineOfScrimmage = 1,
    Midfield = 2,
    BallCarrier = 3,
}

/// <summary>
/// Simple anchor used by play scripts.
///
/// Ported from: ArchiveMge/Components/PlayScriptComponent.cs
/// </summary>
public struct PlayAnchor
{
    public PlayAnchorKind Kind;
    public float Dx;
    public float Dy;

    public static PlayAnchor Default => new() { Kind = PlayAnchorKind.None, Dx = 0f, Dy = 0f };
}
