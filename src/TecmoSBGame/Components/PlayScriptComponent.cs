using System;
using System.Collections.Generic;

namespace TecmoSBGame.Components;

/// <summary>
/// Per-entity play script state (instruction pointer + local runtime state).
///
/// This is the engine-native equivalent of Tecmo's per-player "play code" pointer.
/// It is attached/updated when a play is selected.
/// </summary>
public sealed class PlayScriptComponent
{
    public PlayScriptComponent(string scriptId, IReadOnlyList<PlayScriptOp> ops)
    {
        ScriptId = scriptId ?? throw new ArgumentNullException(nameof(scriptId));
        Ops = ops ?? throw new ArgumentNullException(nameof(ops));
    }

    public string ScriptId { get; }
    public IReadOnlyList<PlayScriptOp> Ops { get; }

    public int Ip;
    public float WaitSeconds;

    /// <summary>
    /// Anchor computed during pre-snap (e.g., LOS or midfield reference). Kept in component state
    /// so commands can refer to it without re-deriving from raw ROM data.
    /// </summary>
    public PlayAnchor Anchor = new();

    public override string ToString() => $"script={ScriptId} ops={Ops.Count} ip={Ip} wait={WaitSeconds:0.000}s";
}

public enum PlayAnchorKind
{
    None = 0,
    LineOfScrimmage = 1,
    Midfield = 2,
    BallCarrier = 3,
}

public sealed class PlayAnchor
{
    public PlayAnchorKind Kind;
    public float Dx;
    public float Dy;
}

public enum PlayScriptOpKind
{
    Nop = 0,

    WaitUntilSnap = 10,

    SetAnchor = 20,
    MoveBy = 21,
    MoveToAnchorOffset = 22,

    PassBlock = 40,
    PullAndBlock = 41,

    HandoffTo = 50,

    Jump = 90,
    Loop = 91,

    Unknown = 999,
}

public readonly record struct PlayScriptOp(
    PlayScriptOpKind Kind,
    float A,
    float B,
    string? S,
    string Raw);
