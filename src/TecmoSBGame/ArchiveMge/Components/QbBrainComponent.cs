using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TecmoSBGame.Components;

/// <summary>
/// Deterministic, frame-based QB decision state.
///
/// This component is intended to emulate Tecmo Super Bowl's QB logic:
/// - Fixed-step dropback timing
/// - Read progression with "stare down" timers
/// - Pressure detection -> pocket movement -> scramble
/// - Throw decisions aligned to route breaks (not first-frame openness)
/// </summary>
public sealed class QbBrainComponent
{
    // Dropback state
    public DropbackType Dropback { get; set; }
    public int DropbackStep { get; set; } // Current step (0-7)
    public bool DropbackComplete { get; set; }

    /// <summary>
    /// QB position at snap (used to make dropback distance deterministic).
    /// </summary>
    public Vector2 SnapPosition { get; set; }

    /// <summary>
    /// Frames elapsed since snap while in dropback.
    /// </summary>
    public int DropbackFrame { get; set; }

    // Read progression
    public List<int> ReadOrder { get; set; } = new();
    public int CurrentReadIndex { get; set; }
    public int ReadTimer { get; set; } // frames spent on current read

    // Throw decision
    public bool ThrowDecisionMade { get; set; }
    public int TargetReceiverId { get; set; } = -1;
    public Vector2 ThrowTarget { get; set; }

    // Pressure/scramble
    public bool PressureDetected { get; set; }
    public int PressureFrameCount { get; set; }
    public bool ScrambleMode { get; set; }

    // Assembly-accurate timing (approx)
    public const int READ_TIME_LIMIT = 45; // frames before next read (~0.75s)
    public const int PRESSURE_THRESHOLD = 30; // frames of pressure to trigger scramble

    // Per-dropback tuning (approx; can be moved to YAML later)
    public const int STEP_FRAMES = 5; // 5 frames per step in Tecmo

    public void ResetForSnap(Vector2 snapPos)
    {
        SnapPosition = snapPos;
        DropbackStep = 0;
        DropbackFrame = 0;
        DropbackComplete = Dropback == DropbackType.Shotgun;

        CurrentReadIndex = 0;
        ReadTimer = 0;

        ThrowDecisionMade = false;
        TargetReceiverId = -1;
        ThrowTarget = Vector2.Zero;

        PressureDetected = false;
        PressureFrameCount = 0;
        ScrambleMode = false;
    }

    public int GetTotalStepCount() => Dropback switch
    {
        DropbackType.Shotgun => 0,
        DropbackType.ThreeStep => 3,
        DropbackType.FiveStep => 5,
        DropbackType.SevenStep => 7,
        DropbackType.RolloutLeft => 5,
        DropbackType.RolloutRight => 5,
        _ => 5,
    };
}

public enum DropbackType
{
    Shotgun,
    ThreeStep,
    FiveStep,
    SevenStep,
    RolloutLeft,
    RolloutRight
}
