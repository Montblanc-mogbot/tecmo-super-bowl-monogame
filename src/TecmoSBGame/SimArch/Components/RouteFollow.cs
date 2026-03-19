using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Runtime state for following a route plan.
/// Route plans live in <see cref="TecmoSBGame.SimArch.Routes.RouteRegistry"/>.
/// </summary>
public struct RouteFollow
{
    public int RouteId;
    public int NodeIndex;
    public int FramesRemainingInNode;

    public bool HasAnchor;
    public Vector2 AnchorPosition;

    public bool Completed;
}
