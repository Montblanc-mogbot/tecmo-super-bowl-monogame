using System;
using Arch.Core;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

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
    public float MaxTurnDegreesPerTick = 9f;
    public float MaxSpeedPerTick = 1.5f;

    public void Update(World world, float dtSeconds)
    {
        // Tick scale assumes 60Hz sim. We'll unify this later.
        var tickScale = dtSeconds * 60f;
        var maxTurnRad = MathHelper.ToRadians(MaxTurnDegreesPerTick * tickScale);

        // For now: if Behavior says TrackingEntity, steer velocity toward target.
        // Later we will separate steering/desired direction from integration.
        var query = new QueryDescription().WithAll<Position, Velocity, Behavior>();

        world.Query(in query, (Entity e, ref Position pos, ref Velocity vel, ref Behavior b) =>
        {
            Vector2 desiredDir;

            if (b.State == BehaviorState.TrackingEntity && b.TargetEntityId != 0)
            {
                var te = new Entity(world, b.TargetEntityId);
                if (te.IsAlive() && te.Has<Position>())
                {
                    var toTarget = te.Get<Position>().Value - pos.Value;
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

            var currentDir = SafeNormalize(vel.Value);
            var newDir = ApplyTurnLimit(currentDir, desiredDir, maxTurnRad);

            // For now: always drive toward desired direction when not idle.
            if (b.State == BehaviorState.Idle)
                vel.Value = Vector2.Zero;
            else
                vel.Value = newDir * MaxSpeedPerTick;

            pos.Value += vel.Value * tickScale;
        });
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
