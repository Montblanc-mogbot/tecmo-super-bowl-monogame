using System;
using System.Collections.Generic;

namespace TecmoSBGame.Components;

/// <summary>
/// A single frame in a sprite animation.
/// Duration is expressed in 60Hz simulation ticks.
/// </summary>
public readonly record struct AnimationFrame(string SpriteId, int DurationTicks);

/// <summary>
/// A clip is an ordered set of frames that optionally loops.
/// </summary>
public sealed class AnimationClip
{
    public string Id { get; }
    public IReadOnlyList<AnimationFrame> Frames { get; }
    public bool Loop { get; }

    public AnimationClip(string id, IReadOnlyList<AnimationFrame> frames, bool loop = true)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Clip id required", nameof(id));

        if (frames is null || frames.Count == 0)
            throw new ArgumentException("Clip requires at least one frame", nameof(frames));

        Id = id;
        Frames = frames;
        Loop = loop;
    }
}

/// <summary>
/// Drives sprite selection over time.
///
/// This is a simple, assembly-style 60Hz tick animation controller:
/// - frame durations are integer ticks
/// - clip switches reset frame index
/// - the current frame writes into SpriteComponent.SpriteId
///
/// The clip may be selected explicitly (DesiredClipId) or derived by AnimationSystem
/// from gameplay state (Behavior/Velocity/BallCarrier).
/// </summary>
public sealed class AnimationComponent
{
    public readonly Dictionary<string, AnimationClip> Clips = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// If set, AnimationSystem will try to play this clip.
    /// If null/empty, AnimationSystem derives a clip from gameplay state.
    /// </summary>
    public string? DesiredClipId;

    /// <summary>
    /// Currently playing clip id (resolved to an entry in Clips).
    /// </summary>
    public string CurrentClipId = "idle";

    public int FrameIndex;
    public int TickInFrame;

    // Accumulator to handle dt drift while keeping integer-tick frame timing.
    public float TickAccumulator;

    public bool Paused;

    public AnimationComponent()
    {
    }

    public static AnimationComponent CreateWithDefaultPlayerClips(string spriteId)
    {
        // Until we have real multi-frame atlases, keep clips 1-frame.
        // The system is still valuable because it centralizes clip switching logic.
        var c = new AnimationComponent();

        var frame = new AnimationFrame(spriteId, DurationTicks: 8);
        c.Clips["idle"] = new AnimationClip("idle", new[] { frame }, loop: true);
        c.Clips["run"] = new AnimationClip("run", new[] { frame }, loop: true);
        c.Clips["run_ball"] = new AnimationClip("run_ball", new[] { frame }, loop: true);
        c.Clips["engaged"] = new AnimationClip("engaged", new[] { frame }, loop: true);

        c.CurrentClipId = "idle";
        c.FrameIndex = 0;
        c.TickInFrame = 0;
        return c;
    }
}
