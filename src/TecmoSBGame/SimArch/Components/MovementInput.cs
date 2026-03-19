using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Movement input intent (only meaningful for the single controlled entity).
///
/// Ported from: ArchiveMge/Components/MovementComponents.cs
/// </summary>
public struct MovementInput
{
    /// <summary>Normalized desired direction.</summary>
    public Vector2 Direction;
}
