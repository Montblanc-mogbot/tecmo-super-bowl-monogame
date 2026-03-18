using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Current directional input for the controlled player.
/// Stored on Sim (not as an entity component) but defined here for shared typing.
/// </summary>
public struct Input
{
    public Vector2 Direction;
}
