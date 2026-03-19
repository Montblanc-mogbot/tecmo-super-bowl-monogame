using System;
using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.State;

/// <summary>
/// Converts between world X coordinates and absolute yardline (0..100).
///
/// Convention:
/// - Absolute yard 0 is the left endzone/goal line.
/// - Absolute yard 100 is the right endzone/goal line.
/// - World X increases left→right.
///
/// This is a simplified, linear mapping for SimArch scaffolding.
/// </summary>
public static class FieldMapping
{
    // These bounds should match the renderer's field width. Tune as needed.
    public const float FieldLeftX = 16f;
    public const float FieldRightX = 240f;

    public static int WorldXToAbsoluteYard(float x)
    {
        var t = (x - FieldLeftX) / (FieldRightX - FieldLeftX);
        t = Math.Clamp(t, 0f, 1f);
        return (int)MathF.Round(t * 100f);
    }

    public static float AbsoluteYardToWorldX(int yard)
    {
        yard = Math.Clamp(yard, 0, 100);
        var t = yard / 100f;
        return FieldLeftX + t * (FieldRightX - FieldLeftX);
    }

    public static int BallToAbsoluteYard(in Vector2 ballWorldPos)
        => WorldXToAbsoluteYard(ballWorldPos.X);
}
