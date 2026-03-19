using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Stores a parsed, deterministic formation "script" derived from YAML command strings.
///
/// Ported from: ArchiveMge/Components/FormationScriptComponent.cs
/// </summary>
public struct FormationScript
{
    public IReadOnlyList<FormationScriptOp> Ops;

    /// <summary>Instruction pointer into <see cref="Ops"/>.</summary>
    public int Ip;

    /// <summary>Seconds remaining for Pause operations.</summary>
    public float WaitSeconds;

    /// <summary>If true, interpreter will not drive movement ops (e.g., when player is controlling this entity).</summary>
    public bool SuspendMovement;

    public static FormationScript Create(IReadOnlyList<FormationScriptOp> ops)
    {
        if (ops is null)
            throw new ArgumentNullException(nameof(ops));

        return new FormationScript
        {
            Ops = ops,
            Ip = 0,
            WaitSeconds = 0f,
            SuspendMovement = false,
        };
    }

    public override readonly string ToString() => $"ops={Ops?.Count ?? 0} ip={Ip} wait={WaitSeconds:0.000}s";
}

public enum FormationScriptOpKind
{
    // Generic recognized-but-ignored token (keeps parser from emitting NOP/Unknown fallbacks).
    NoOp = 0,

    // Placement/motion subset
    SetPosFromKick = 1,
    SetPosFromHike = 2,
    SetPosFromMid = 3,

    MoveAbsolute = 10,
    MoveRelative = 11,
    Pause = 12,
    LoopBack = 13,
    WaitForSnap = 14,

    // Control / blocking markers (may be ignored by interpreter for now)
    TakeControl = 20,
    ComputerTakeControl = 21,
    SetToBlock = 22,
    PassBlock = 23,

    // Common opaque opcodes appearing in YAML (recognized, currently ignored)
    Fc = 30,
    Cd = 31,
    Cf = 32,
    JumpTo = 33,
    BoostMs = 34,
    BoostHp = 35,
    SwitchIcon = 36,
    FaceDirection = 37,
    ShotgunHike = 38,

    // Special teams markers
    Punt = 50,
    FieldGoal = 51,
    FieldGoalTakeSnap = 52,
}

public readonly record struct FormationScriptOp(
    FormationScriptOpKind Kind,
    Vector2 Vec,
    float Seconds,
    string Raw)
{
    public static FormationScriptOp NoOp(string raw) => new(FormationScriptOpKind.NoOp, Vector2.Zero, 0f, raw);
}
