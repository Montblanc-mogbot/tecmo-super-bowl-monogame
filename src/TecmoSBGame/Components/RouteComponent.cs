using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TecmoSBGame.Components;

/// <summary>
/// Data-driven receiver/RB route definition + runtime state.
///
/// IMPORTANT: Tecmo routes are timing-based (frame counts), not distance/arrival based.
/// This component is intentionally "frame-accurate" friendly:
/// - Each node advances after MinFrames have elapsed.
/// - Direction changes are instantaneous at node boundaries (Tecmo-style cuts).
///
/// Offsets are relative to the route origin captured at route start (typically LOS / snap position).
/// </summary>
public sealed class RouteComponent
{
    // Route definition from YAML/play data
    // Prefer RouteKind for code; keep RouteType string for YAML friendliness.
    public RouteKind RouteKind { get; set; } = RouteKind.Unknown;
    public string RouteType { get; set; } = ""; // GO, POST, CORNER, OUT, IN, SLANT, CURL, etc.

    /// <summary>
    /// Frame-timed route nodes (offset from Origin).
    /// </summary>
    public List<RouteNode> Nodes { get; set; } = new();

    // Runtime state
    public int CurrentNodeIndex { get; set; }
    public int FrameCounter { get; set; } // Frames spent on current node
    public bool RouteComplete { get; set; }
    public bool IsSitting { get; set; } // Waiting for throw

    // Assembly-accurate timing
    public int StemFrames { get; set; } // Frames before break (heuristic until real ROM tables are imported)
    public float BaseSpeed { get; set; } // "base route speed" (units per 60Hz tick) at MS=69 (TSB max)

    // Internal runtime (captured once)
    public bool Initialized { get; set; }
    public Vector2 Origin { get; set; } // captured starting position

    // Speed bookkeeping so we can restore per-entity tuning when the route ends.
    public bool SpeedApplied { get; set; }
    public float OriginalMaxSpeedPerTick { get; set; }
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

public struct RouteNode
{
    /// <summary>Relative to LOS/origin.</summary>
    public Vector2 Offset;

    /// <summary>Minimum frames before transitioning to the next node.</summary>
    public int MinFrames;

    /// <summary>
    /// Legacy/string form used by scaffold YAML.
    /// Prefer ActionKind in code.
    /// </summary>
    public string Action;

    /// <summary>Strongly typed action for engine code.</summary>
    public RouteNodeAction ActionKind;
}
