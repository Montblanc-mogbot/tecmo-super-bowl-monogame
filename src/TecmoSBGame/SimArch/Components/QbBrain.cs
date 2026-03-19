namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Minimal QB AI state for SimArch.
///
/// This is a deterministic scaffold:
/// - drop back for N frames
/// - then attempt a pass to the current read target
/// </summary>
public struct QbBrain
{
    // ---- Dropback ----

    /// <summary>
    /// Legacy/MGE-style dropback classification.
    /// </summary>
    public DropbackType Dropback;

    /// <summary>
    /// Step index (0-7) for Tecmo-style fixed-step dropbacks.
    /// </summary>
    public int DropbackStep;

    public bool DropbackComplete;

    /// <summary>
    /// QB position at snap (used to make dropback distance deterministic).
    /// </summary>
    public Microsoft.Xna.Framework.Vector2 SnapPosition;

    /// <summary>Frames elapsed since snap while in dropback.</summary>
    public int DropbackFrame;

    /// <summary>
    /// Frames remaining before first throw attempt (used by current minimal QbAiSystem).
    /// </summary>
    public int DropbackFramesRemaining;

    // ---- Reads ----

    /// <summary>
    /// Read progression receiver entity ids (managed).
    /// </summary>
    public System.Collections.Generic.List<int> ReadOrder;

    public int CurrentReadIndex;

    /// <summary>Frames spent on current read.</summary>
    public int ReadTimer;

    /// <summary>
    /// 0-based index into the default read order list (used by current minimal QbAiSystem).
    /// </summary>
    public int ReadIndex;

    // ---- Throw decision ----

    public bool ThrowDecisionMade;
    public int TargetReceiverId;
    public Microsoft.Xna.Framework.Vector2 ThrowTarget;

    /// <summary>Whether a pass has already been requested for this play (current minimal QbAiSystem).</summary>
    public bool PassRequested;

    /// <summary>Pass type preference (bullet/lob).</summary>
    public TecmoSBGame.SimArch.PassType PassType;

    // ---- Pressure/scramble ----

    public bool PressureDetected;
    public int PressureFrameCount;
    public bool ScrambleMode;

    // Assembly-ish timing (kept as fields for easy inspection; can move to consts later)
    public int ReadTimeLimitFrames;
    public int PressureThresholdFrames;
    public int StepFrames;

    public static QbBrain Default => new()
    {
        Dropback = DropbackType.FiveStep,
        DropbackStep = 0,
        DropbackComplete = false,
        SnapPosition = Microsoft.Xna.Framework.Vector2.Zero,
        DropbackFrame = 0,
        DropbackFramesRemaining = 30,

        ReadOrder = new System.Collections.Generic.List<int>(),
        CurrentReadIndex = 0,
        ReadTimer = 0,
        ReadIndex = 0,

        ThrowDecisionMade = false,
        TargetReceiverId = -1,
        ThrowTarget = Microsoft.Xna.Framework.Vector2.Zero,
        PassRequested = false,
        PassType = TecmoSBGame.SimArch.PassType.Bullet,

        PressureDetected = false,
        PressureFrameCount = 0,
        ScrambleMode = false,

        ReadTimeLimitFrames = 45,
        PressureThresholdFrames = 30,
        StepFrames = 5,
    };
}

/// <summary>
/// Ported from: ArchiveMge/Components/QbBrainComponent.cs
/// </summary>
public enum DropbackType
{
    Shotgun,
    ThreeStep,
    FiveStep,
    SevenStep,
    RolloutLeft,
    RolloutRight,
}
