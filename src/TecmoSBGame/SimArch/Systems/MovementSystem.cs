using System;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

// Ported from: src/TecmoSBGame/ArchiveMge/Systems/MovementSystem.cs

/// <summary>
/// Minimal movement system for Arch sim.
///
/// - Applies a max turn rate per tick (Tecmo feel).
/// - Integrates position by velocity.
///
/// NOTE: This is an intentionally small first version; acceleration curves, burst, speed modifiers,
/// and behavior-driven targeting will be layered in as separate systems.
/// </summary>
public sealed class MovementSystem
{
    // Defaults used if an entity has no MovementTuning component.
    public float DefaultMaxTurnDegreesPerTick = 9f;
    public float DefaultMaxSpeedPerTick = 1.5f;

    public void Update(World world, float dtSeconds, int controlledEntityId, Vector2 inputDir)
    {
        // Tick scale assumes 60Hz sim. We'll unify this later.
        var tickScale = dtSeconds * 60f;

        // For now: if Behavior says TrackingEntity, steer velocity toward target.
        // Later we will separate steering/desired direction from integration.
        var query = new QueryDescription().WithAll<Position, Velocity, Behavior>();

        var qTuning = new QueryDescription().WithAll<MovementTuning>();
        var tuningById = new System.Collections.Generic.Dictionary<int, MovementTuning>();
        world.Query(in qTuning, (Entity e, ref MovementTuning t) =>
        {
            tuningById[e.Id] = t;
        });

        var inputNorm = SafeNormalize(inputDir);

        world.Query(in query, (Entity e, ref Position pos, ref Velocity vel, ref Behavior b) =>
        {
            Vector2 desiredDir;

            // Controlled player: input overrides AI steering.
            if (e.Id == controlledEntityId && inputNorm != Vector2.Zero)
            {
                desiredDir = inputNorm;
            }
            else if (b.State == BehaviorState.TrackingEntity && b.TargetEntityId >= 0)
            {
                if (TryGetPosition(world, b.TargetEntityId, out var targetPos))
                {
                    var toTarget = targetPos - pos.Value;
                    desiredDir = SafeNormalize(toTarget);
                }
                else
                {
                    desiredDir = SafeNormalize(vel.Value);
                }
            }
            else if (b.State == BehaviorState.MovingToPosition)
            {
                desiredDir = SafeNormalize(b.TargetPosition - pos.Value);
            }
            else
            {
                desiredDir = SafeNormalize(vel.Value);
            }

            var tuning = tuningById.TryGetValue(e.Id, out var t)
                ? t
                : new MovementTuning
                {
                    MaxSpeedPerTick = DefaultMaxSpeedPerTick,
                    MaxTurnDegreesPerTick = DefaultMaxTurnDegreesPerTick,
                    AccelPerTick = 0f,
                    DecelPerTick = 0f,
                };

            var maxTurnRad = MathHelper.ToRadians(tuning.MaxTurnDegreesPerTick * tickScale);

            var currentDir = SafeNormalize(vel.Value);
            var newDir = ApplyTurnLimit(currentDir, desiredDir, maxTurnRad);

            // For now: always drive toward desired direction when not idle.
            if (b.State == BehaviorState.Idle)
                vel.Value = Vector2.Zero;
            else
                vel.Value = newDir * tuning.MaxSpeedPerTick;

            pos.Value += vel.Value * tickScale;
        });
    }


    private static bool TryGetPosition(World world, int entityId, out Vector2 pos)
    {
        pos = default;
        if (entityId < 0)
            return false;

        var found = false;
        var result = Vector2.Zero;

        var q = new QueryDescription().WithAll<Position>();
        world.Query(in q, (Entity e, ref Position p) =>
        {
            if (found)
                return;
            if (e.Id != entityId)
                return;

            result = p.Value;
            found = true;
        });

        if (!found)
            return false;

        pos = result;
        return true;
    }

    private static Vector2 ApplyTurnLimit(Vector2 lastDir, Vector2 desiredDir, float maxTurnRad)
    {
        if (desiredDir == Vector2.Zero)
            return Vector2.Zero;

        if (lastDir == Vector2.Zero)
            return desiredDir;

        if (maxTurnRad <= 0.000001f)
            return lastDir;

        var a0 = MathF.Atan2(lastDir.Y, lastDir.X);
        var a1 = MathF.Atan2(desiredDir.Y, desiredDir.X);
        var delta = WrapAngle(a1 - a0);
        var clamped = MathHelper.Clamp(delta, -maxTurnRad, maxTurnRad);
        var a = a0 + clamped;

        return new Vector2(MathF.Cos(a), MathF.Sin(a));
    }

    private static float WrapAngle(float radians)
    {
        while (radians > MathF.PI) radians -= MathF.Tau;
        while (radians < -MathF.PI) radians += MathF.Tau;
        return radians;
    }

    private static Vector2 SafeNormalize(Vector2 v)
    {
        var lenSq = v.LengthSquared();
        if (lenSq <= 0.000001f)
            return Vector2.Zero;
        return v / MathF.Sqrt(lenSq);
    }
}
