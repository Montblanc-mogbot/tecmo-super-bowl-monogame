using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Routes;

public readonly record struct RouteNode(Vector2 Delta, int Frames);

public sealed class RoutePlan
{
    public required RouteNode[] Nodes { get; init; }
}
