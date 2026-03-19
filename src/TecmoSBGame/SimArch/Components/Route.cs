using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Data-driven receiver/RB route definition + runtime state.
///
/// NOTE: SimArch currently uses RouteRegistry + RouteFollow for execution.
/// This component exists for parity with the legacy MGE model.
///
/// Ported from: ArchiveMge/Components/RouteComponent.cs
/// </summary>
public struct Route
{
    public RouteKind RouteKind;
    public string RouteType;

    public List<RouteNodeDef> Nodes;

    // Runtime state
    public int CurrentNodeIndex;
    public int FrameCounter;
    public bool RouteComplete;
    public bool IsSitting;

    // Timing / speed
    public int StemFrames;
    public float BaseSpeed;

    public Vector2 ManAdjustOffset;
    public Vector2 ZoneAdjustOffset;

    public bool Initialized;
    public Vector2 Origin;

    public bool SpeedApplied;
    public float OriginalMaxSpeedPerTick;

    public static Route Default => new()
    {
        RouteKind = RouteKind.Unknown,
        RouteType = string.Empty,
        Nodes = new List<RouteNodeDef>(),
        CurrentNodeIndex = 0,
        FrameCounter = 0,
        RouteComplete = false,
        IsSitting = false,
        StemFrames = 0,
        BaseSpeed = 0f,
        ManAdjustOffset = Vector2.Zero,
        ZoneAdjustOffset = Vector2.Zero,
        Initialized = false,
        Origin = Vector2.Zero,
        SpeedApplied = false,
        OriginalMaxSpeedPerTick = 0f,
    };
}

public enum RouteKind
{
    Unknown = 0,
    Go,
    Post,
    Corner,
    Out,
    In,
    Slant,
    Curl,
    Flat,
    Wheel,
    Screen,
    Block,
}

public enum RouteNodeAction
{
    Run = 0,
    Cut,
    Sit,
    Return,
}

/// <summary>
/// Legacy route node definition container (kept separate from SimArch.Routes.RouteNode).
/// </summary>
public struct RouteNodeDef
{
    public Vector2 Offset;
    public int MinFrames;

    public string Action;
    public RouteNodeAction ActionKind;
}
