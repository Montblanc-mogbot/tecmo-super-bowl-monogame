using Microsoft.Xna.Framework;

namespace TecmoSBGame.Field;

/// <summary>
/// Central definition of the football field coordinate system.
///
/// Coordinates are in the game's virtual 256x224 space.
/// X is along the length of the field (goal line to goal line).
/// Y is across the width (sideline to sideline).
/// </summary>
public static class FieldBounds
{
    // Playable field rectangle (between goal lines and sidelines).
    // These must match the renderer's field mapping.
    public const int FieldLeftX = 16;
    public const int FieldRightX = 240;
    public const int FieldTopY = 40;
    public const int FieldBottomY = 184;

    // Simple end zone depth (pixels) rendered beyond the goal lines.
    public const int EndZoneDepth = 8;

    public static readonly Rectangle PlayableRect = new(FieldLeftX, FieldTopY, FieldRightX - FieldLeftX, FieldBottomY - FieldTopY);

    /// <summary>
    /// Horizontal band including end zones (used for touchback/safety checks).
    /// </summary>
    public static readonly Rectangle InBoundsWithEndZonesRect = new(FieldLeftX - EndZoneDepth, FieldTopY, (FieldRightX - FieldLeftX) + 2 * EndZoneDepth, FieldBottomY - FieldTopY);

    public static float LeftGoalLineX => FieldLeftX;
    public static float RightGoalLineX => FieldRightX;

    public static bool IsOutOfBoundsSidelines(Vector2 pos)
        => pos.Y < FieldTopY || pos.Y > FieldBottomY;

    public static bool IsBeyondLeftGoalLine(Vector2 pos) => pos.X < LeftGoalLineX;
    public static bool IsBeyondRightGoalLine(Vector2 pos) => pos.X > RightGoalLineX;

    public static bool IsOutOfAllField(Vector2 pos)
        => pos.X < (FieldLeftX - EndZoneDepth) || pos.X > (FieldRightX + EndZoneDepth) || IsOutOfBoundsSidelines(pos);

    public static int XToAbsoluteYard(float x)
    {
        var t = (x - FieldLeftX) / (FieldRightX - FieldLeftX);
        var yard = (int)System.MathF.Round(t * 100f);
        return System.Math.Clamp(yard, 0, 100);
    }

    public static float AbsoluteYardToX(int absoluteYard0To100)
    {
        absoluteYard0To100 = System.Math.Clamp(absoluteYard0To100, 0, 100);
        var t = absoluteYard0To100 / 100f;
        return FieldLeftX + t * (FieldRightX - FieldLeftX);
    }
}
