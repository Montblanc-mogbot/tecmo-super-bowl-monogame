using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Sprite rendering component.
///
/// Ported from: ArchiveMge/Components/GameComponents.cs (SpriteComponent)
/// </summary>
public struct Sprite
{
    public string SpriteId;
    public Color Tint;
    public float Rotation;
    public Vector2 Scale;
    public bool FlipHorizontal;

    public static Sprite Create(string spriteId) => new()
    {
        SpriteId = spriteId,
        Tint = Color.White,
        Rotation = 0f,
        Scale = Vector2.One,
        FlipHorizontal = false,
    };
}
