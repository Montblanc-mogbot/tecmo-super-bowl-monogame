using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.PlayScripts;

public enum PlayScriptOpKind
{
    Loop = 0,
    WaitSeconds = 1,
    MoveBy = 2,
    HandoffToSlotAfterSeconds = 3,
    SetMs = 4,
    BoostRs = 5,
}

public readonly record struct PlayScriptOp(
    PlayScriptOpKind Kind,
    float Seconds = 0f,
    Vector2 Delta = default,
    string? Slot = null,
    float Value = 0f);
