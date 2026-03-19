using System;
using System.Collections.Generic;

namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// A single frame in a sprite animation.
/// Duration is expressed in 60Hz simulation ticks.
///
/// Ported from: ArchiveMge/Components/AnimationComponents.cs
/// </summary>
public readonly record struct AnimationFrame(string SpriteId, int DurationTicks);

/// <summary>
/// A clip is an ordered set of frames that optionally loops.
///
/// Ported from: ArchiveMge/Components/AnimationComponents.cs
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
/// - the current frame typically writes into a Sprite component (or snapshot)
///
/// The clip may be selected explicitly (DesiredClipId) or derived by systems
/// from gameplay state (Behavior/Velocity/BallCarrier).
///
/// Ported from: ArchiveMge/Components/AnimationComponents.cs
/// </summary>
public struct AnimationState
{
    // NOTE: We keep clips as managed references. This component is intended
    // to be used on a small number of entities and is not performance critical.
    public Dictionary<string, AnimationClip> Clips;

    /// <summary>
    /// If set, animation systems will try to play this clip.
    /// If null/empty, animation systems may derive a clip from gameplay state.
    /// </summary>
    public string? DesiredClipId;

    /// <summary>
    /// Currently playing clip id (resolved to an entry in Clips).
    /// </summary>
    public string CurrentClipId;

    public int FrameIndex;
    public int TickInFrame;

    // Accumulator to handle dt drift while keeping integer-tick frame timing.
    public float TickAccumulator;

    public bool Paused;

    public static AnimationState CreateWithDefaultPlayerClips(string spriteId)
    {
        // Until we have real multi-frame atlases, keep clips 1-frame.
        // The system is still valuable because it centralizes clip switching logic.
        var clips = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

        var frame = new AnimationFrame(spriteId, DurationTicks: 8);
        clips["idle"] = new AnimationClip("idle", new[] { frame }, loop: true);
        clips["run"] = new AnimationClip("run", new[] { frame }, loop: true);
        clips["run_ball"] = new AnimationClip("run_ball", new[] { frame }, loop: true);
        clips["engaged"] = new AnimationClip("engaged", new[] { frame }, loop: true);

        return new AnimationState
        {
            Clips = clips,
            DesiredClipId = null,
            CurrentClipId = "idle",
            FrameIndex = 0,
            TickInFrame = 0,
            TickAccumulator = 0f,
            Paused = false,
        };
    }
}
