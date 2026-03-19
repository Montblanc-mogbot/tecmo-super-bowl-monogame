using System;
using Arch.Core;
using Arch.Core.Extensions;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Advances sprite animation at a fixed 60Hz simulation rate.
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/AnimationSystem.cs
/// </summary>
public sealed class AnimationSystem
{
    public void Update(World world, float dtSeconds)
    {
        if (dtSeconds <= 0f)
            return;

        var q = new QueryDescription().WithAll<AnimationState, Sprite>();
        world.Query(in q, (Entity e, ref AnimationState a, ref Sprite s) =>
        {
            if (a.Paused)
                return;

            var desired = ResolveDesiredClip(e, a);
            if (!string.IsNullOrWhiteSpace(desired) && !string.Equals(desired, a.CurrentClipId, StringComparison.OrdinalIgnoreCase))
            {
                if (TrySetClip(ref a, desired!, out var clip))
                {
                    s.SpriteId = clip.Frames[0].SpriteId;
                }
            }

            if (!a.Clips.TryGetValue(a.CurrentClipId, out var current))
                return;

            a.TickAccumulator += dtSeconds * 60f;
            var ticksToAdvance = (int)MathF.Floor(a.TickAccumulator);
            if (ticksToAdvance <= 0)
                return;

            a.TickAccumulator -= ticksToAdvance;
            AdvanceTicks(ref a, current, ticksToAdvance);

            var frame = current.Frames[Math.Clamp(a.FrameIndex, 0, current.Frames.Count - 1)];
            if (!string.IsNullOrWhiteSpace(frame.SpriteId))
                s.SpriteId = frame.SpriteId;
        });
    }

    private static string ResolveDesiredClip(Entity e, in AnimationState a)
    {
        if (!string.IsNullOrWhiteSpace(a.DesiredClipId))
            return a.DesiredClipId!;

        if (e.Has<Behavior>())
        {
            var b = e.Get<Behavior>();
            if (b.State is BehaviorState.Engaged or BehaviorState.Tackling)
                return "engaged";
        }

        var speed = 0f;
        if (e.Has<Velocity>())
            speed = e.Get<Velocity>().Value.Length();

        if (speed > 0.05f)
        {
            if (e.Has<BallCarrier>() && e.Get<BallCarrier>().HasBall)
                return "run_ball";
            return "run";
        }

        return "idle";
    }

    private static bool TrySetClip(ref AnimationState a, string clipId, out AnimationClip clip)
    {
        if (a.Clips.TryGetValue(clipId, out clip!))
        {
            a.CurrentClipId = clip.Id;
            a.FrameIndex = 0;
            a.TickInFrame = 0;
            return true;
        }

        if (a.Clips.TryGetValue("idle", out clip!))
        {
            a.CurrentClipId = clip.Id;
            a.FrameIndex = 0;
            a.TickInFrame = 0;
            return true;
        }

        return false;
    }

    private static void AdvanceTicks(ref AnimationState a, AnimationClip clip, int ticks)
    {
        if (clip.Frames.Count == 0)
            return;

        var remaining = ticks;
        while (remaining > 0)
        {
            var frame = clip.Frames[Math.Clamp(a.FrameIndex, 0, clip.Frames.Count - 1)];
            var duration = Math.Max(1, frame.DurationTicks);

            var ticksLeftInFrame = duration - a.TickInFrame;
            if (ticksLeftInFrame <= 0)
            {
                a.TickInFrame = 0;
                ticksLeftInFrame = duration;
            }

            var step = Math.Min(remaining, ticksLeftInFrame);
            a.TickInFrame += step;
            remaining -= step;

            if (a.TickInFrame >= duration)
            {
                a.TickInFrame = 0;
                a.FrameIndex++;

                if (a.FrameIndex >= clip.Frames.Count)
                {
                    a.FrameIndex = clip.Loop ? 0 : clip.Frames.Count - 1;
                    if (!clip.Loop)
                        break;
                }
            }
        }
    }
}
