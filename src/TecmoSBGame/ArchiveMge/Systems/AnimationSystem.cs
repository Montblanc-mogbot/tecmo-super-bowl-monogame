using System;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;

namespace TecmoSBGame.Systems;

/// <summary>
/// Advances sprite animation at a fixed 60Hz simulation rate.
///
/// Notes:
/// - The project uses a fixed-timestep driver; nevertheless, this system uses a tick accumulator
///   so it remains correct if dt drifts.
/// - Clip selection is gameplay-driven (Behavior/Velocity/BallCarrier) unless DesiredClipId is set.
/// </summary>
public sealed class AnimationSystem : EntityUpdateSystem
{
    private ComponentMapper<AnimationComponent> _anim = null!;
    private ComponentMapper<SpriteComponent> _sprite = null!;
    private ComponentMapper<VelocityComponent> _vel = null!;
    private ComponentMapper<BehaviorComponent> _behavior = null!;
    private ComponentMapper<BallCarrierComponent> _carrier = null!;

    public AnimationSystem() : base(Aspect.All(typeof(AnimationComponent), typeof(SpriteComponent)))
    {
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _anim = mapperService.GetMapper<AnimationComponent>();
        _sprite = mapperService.GetMapper<SpriteComponent>();
        _vel = mapperService.GetMapper<VelocityComponent>();
        _behavior = mapperService.GetMapper<BehaviorComponent>();
        _carrier = mapperService.GetMapper<BallCarrierComponent>();
    }

    public override void Update(Microsoft.Xna.Framework.GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (dt <= 0)
            return;

        foreach (var entityId in ActiveEntities)
        {
            var a = _anim.Get(entityId);
            var s = _sprite.Get(entityId);

            if (a.Paused)
                continue;

            var desired = ResolveDesiredClip(entityId, a);
            if (!string.Equals(desired, a.CurrentClipId, StringComparison.OrdinalIgnoreCase))
            {
                if (TrySetClip(a, desired, out var clip))
                {
                    // Apply the first frame immediately on switch.
                    s.SpriteId = clip.Frames[0].SpriteId;
                }
            }

            if (!a.Clips.TryGetValue(a.CurrentClipId, out var current))
                continue;

            // Convert elapsed seconds to 60Hz ticks.
            a.TickAccumulator += dt * 60f;
            var ticksToAdvance = (int)MathF.Floor(a.TickAccumulator);
            if (ticksToAdvance <= 0)
                continue;

            a.TickAccumulator -= ticksToAdvance;
            AdvanceTicks(a, current, ticksToAdvance);

            // Write the resolved frame sprite id.
            var frame = current.Frames[Math.Clamp(a.FrameIndex, 0, current.Frames.Count - 1)];
            if (!string.IsNullOrWhiteSpace(frame.SpriteId))
                s.SpriteId = frame.SpriteId;
        }
    }

    private string ResolveDesiredClip(int entityId, AnimationComponent a)
    {
        if (!string.IsNullOrWhiteSpace(a.DesiredClipId))
            return a.DesiredClipId!;

        // Derive from gameplay state (simple first pass).
        if (_behavior.Has(entityId))
        {
            var b = _behavior.Get(entityId);
            if (b.State is BehaviorState.Engaged or BehaviorState.Tackling or BehaviorState.Grappling)
                return "engaged";
        }

        var speed = 0f;
        if (_vel.Has(entityId))
            speed = _vel.Get(entityId).Velocity.Length();

        if (speed > 0.05f)
        {
            if (_carrier.Has(entityId) && _carrier.Get(entityId).HasBall)
                return "run_ball";

            return "run";
        }

        return "idle";
    }

    private static bool TrySetClip(AnimationComponent a, string clipId, out AnimationClip clip)
    {
        if (a.Clips.TryGetValue(clipId, out clip!))
        {
            a.CurrentClipId = clip.Id;
            a.FrameIndex = 0;
            a.TickInFrame = 0;
            return true;
        }

        // Fallback to idle if unknown.
        if (a.Clips.TryGetValue("idle", out clip!))
        {
            a.CurrentClipId = clip.Id;
            a.FrameIndex = 0;
            a.TickInFrame = 0;
            return true;
        }

        return false;
    }

    private static void AdvanceTicks(AnimationComponent a, AnimationClip clip, int ticks)
    {
        // Defensive: if clip has no frames, do nothing.
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
                // Shouldn't happen, but resync.
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
