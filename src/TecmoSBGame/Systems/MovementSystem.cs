using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;

namespace TecmoSBGame.Systems;

/// <summary>
/// Tecmo-style movement system.
///
/// Goals:
/// - Instant direction changes (velocity direction snaps immediately)
/// - Acceleration curve up to max speed
/// - Near-instant stop when no direction is desired
/// - Deterministic under a fixed 60Hz dt
///
/// Notes:
/// - This project currently expresses speed in "units per 60Hz tick".
///   We scale accel/decel when dt drifts from 60Hz so behavior remains stable.
/// </summary>
public sealed class MovementSystem : EntityUpdateSystem
{
    private ComponentMapper<PositionComponent> _positionMapper = null!;
    private ComponentMapper<VelocityComponent> _velocityMapper = null!;
    private ComponentMapper<BehaviorComponent> _behaviorMapper = null!;
    private ComponentMapper<MovementTuningComponent> _tuningMapper = null!;
    private ComponentMapper<PlayerControlComponent> _controlMapper = null!;
    private ComponentMapper<MovementInputComponent> _inputMapper = null!;
    private ComponentMapper<MovementActionComponent> _actionMapper = null!;

    public MovementSystem() : base(Aspect.All(typeof(PositionComponent), typeof(VelocityComponent)))
    {
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _positionMapper = mapperService.GetMapper<PositionComponent>();
        _velocityMapper = mapperService.GetMapper<VelocityComponent>();
        _behaviorMapper = mapperService.GetMapper<BehaviorComponent>();
        _tuningMapper = mapperService.GetMapper<MovementTuningComponent>();
        _controlMapper = mapperService.GetMapper<PlayerControlComponent>();
        _inputMapper = mapperService.GetMapper<MovementInputComponent>();
        _actionMapper = mapperService.GetMapper<MovementActionComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (dt <= 0)
            return;

        // Existing gameplay uses "per-tick" speeds; normalize accel/decel if dt isn't exactly 1/60.
        var tickScale = dt * 60f;

        foreach (var entityId in ActiveEntities)
        {
            var position = _positionMapper.Get(entityId);
            var velocity = _velocityMapper.Get(entityId);

            var tuning = GetTuning(entityId, velocity);
            UpdateActionTimers(entityId, dt);

            // Controlled entity uses explicit movement input direction.
            // Everyone else uses behavior-driven direction.
            var desiredDirection = GetDesiredDirection(entityId);

            var currentSpeed = velocity.Velocity.Length();
            if (desiredDirection == Vector2.Zero)
            {
                // Near-instant stop: drain speed aggressively.
                currentSpeed = MoveTowards(currentSpeed, 0f, tuning.DecelPerTick * tickScale);
                if (currentSpeed <= 0.0001f)
                {
                    velocity.Velocity = Vector2.Zero;
                }
                else
                {
                    // Keep last heading while braking.
                    var lastDir = SafeNormalize(velocity.Velocity);
                    velocity.Velocity = lastDir * currentSpeed;
                }
            }
            else
            {
                // Instant direction change allowed.
                var lastDir = SafeNormalize(velocity.Velocity);
                var newDir = desiredDirection;

                // Apply "cut" penalty when changing direction sharply.
                // Tecmo feel: you can reverse instantly, but you lose speed.
                if (lastDir != Vector2.Zero)
                {
                    var dot = Vector2.Dot(lastDir, newDir); // [-1..1]
                    if (dot < 0.35f)
                    {
                        currentSpeed *= (1f - MathHelper.Clamp(tuning.CutPenalty, 0f, 1f));
                    }
                }

                var maxSpeed = tuning.MaxSpeedPerTick;
                if (_actionMapper.Has(entityId) && _actionMapper.Get(entityId).State == MovementActionState.Burst)
                    maxSpeed *= tuning.BurstMultiplier;

                var accel = tuning.AccelPerTick;
                if (tuning.UseAccelCurve && maxSpeed > 0.0001f)
                {
                    // Curve: fast off the line, taper near max.
                    var t = MathHelper.Clamp(currentSpeed / maxSpeed, 0f, 1f);
                    var curve = 0.30f + 0.70f * (1f - t);
                    accel *= curve;
                }

                currentSpeed = MoveTowards(currentSpeed, maxSpeed, accel * tickScale);
                velocity.Velocity = newDir * currentSpeed;
            }

            // Apply velocity (velocity is already per-tick at 60Hz).
            position.Position += velocity.Velocity * tickScale;
        }
    }

    private MovementTuningComponent GetTuning(int entityId, VelocityComponent velocity)
    {
        if (_tuningMapper.Has(entityId))
            return _tuningMapper.Get(entityId);

        // Back-compat: derive minimal tuning from VelocityComponent.
        // (VelocityComponent.Acceleration historically acted like a per-tick lerp factor, not a real accel.)
        var maxSpeed = velocity.MaxSpeed;
        var accel = MathHelper.Clamp(velocity.Acceleration, 0.01f, 1f) * maxSpeed;
        return new MovementTuningComponent(
            maxSpeedPerTick: maxSpeed,
            accelPerTick: accel,
            decelPerTick: maxSpeed * 4f,
            cutPenalty: 0.25f,
            burstMultiplier: 1.20f);
    }

    private void UpdateActionTimers(int entityId, float dt)
    {
        if (!_actionMapper.Has(entityId))
            return;

        var a = _actionMapper.Get(entityId);
        if (a.CooldownTimer > 0f)
            a.CooldownTimer = Math.Max(0f, a.CooldownTimer - dt);

        if (a.State != MovementActionState.None)
        {
            a.StateTimer -= dt;
            if (a.StateTimer <= 0f)
            {
                a.State = MovementActionState.None;
                a.StateTimer = 0f;
            }
        }
    }

    private Vector2 GetDesiredDirection(int entityId)
    {
        // If this entity is the single selected controlled entity, prefer MovementInputComponent.
        if (_controlMapper.Has(entityId) && _controlMapper.Get(entityId).IsControlled && _inputMapper.Has(entityId))
        {
            var dir = _inputMapper.Get(entityId).Direction;
            return SafeNormalize(dir);
        }

        return GetBehaviorDirection(entityId);
    }

    private Vector2 GetBehaviorDirection(int entityId)
    {
        if (!_behaviorMapper.Has(entityId))
            return Vector2.Zero;

        var behavior = _behaviorMapper.Get(entityId);

        switch (behavior.State)
        {
            case BehaviorState.Idle:
                return Vector2.Zero;

            case BehaviorState.MovingToPosition:
            case BehaviorState.RushingQB:
            case BehaviorState.RunningRoute:
                var direction = behavior.TargetPosition - _positionMapper.Get(entityId).Position;
                if (direction.LengthSquared() > 1f)
                    return SafeNormalize(direction);
                return Vector2.Zero;

            default:
                return Vector2.Zero;
        }
    }

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (current < target)
            return Math.Min(current + maxDelta, target);
        if (current > target)
            return Math.Max(current - maxDelta, target);
        return target;
    }

    private static Vector2 SafeNormalize(Vector2 v)
    {
        if (v == Vector2.Zero)
            return Vector2.Zero;
        var len2 = v.LengthSquared();
        if (len2 <= 0.000001f)
            return Vector2.Zero;
        var inv = 1.0f / (float)Math.Sqrt(len2);
        return v * inv;
    }
}
