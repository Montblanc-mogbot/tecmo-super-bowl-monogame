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
    Nop = 0,
    MoveAbsolute = 1,
    MoveRelative = 2,
    Pause = 3,
    LoopBack = 4,
    WaitForSnap = 5,
    TakeControl = 6,
    ComputerTakeControl = 7,
    SetToBlock = 8,
    PassBlock = 9,
    Unknown = 99,
}

public readonly record struct FormationScriptOp(
    FormationScriptOpKind Kind,
    Vector2 Vec,
    float Seconds,
    string Raw)
{
    public static FormationScriptOp Nop(string raw) => new(FormationScriptOpKind.Nop, Vector2.Zero, 0f, raw);
}
