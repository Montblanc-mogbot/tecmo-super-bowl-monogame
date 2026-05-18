using System;
using Arch.Core;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Events;

namespace TecmoSBGame.SimArch.Systems;

// Ported from: src/TecmoSBGame/ArchiveMge/Systems/PassFlightStartSystem.cs

/// <summary>
/// SimArch equivalent of <c>TecmoSBGame.Systems.PassFlightStartSystem</c>.
///
/// SimArch does not yet have input/AI event wiring, so this system exposes a
/// deterministic helper (<see cref="StartPass"/>) that higher-level code can call.
///
/// This keeps the flight math + state transitions in the sim layer.
/// </summary>
public static class PassFlightStartSystem
{
    // Field bounds (match the MGE system constants).
    private const float FIELD_LEFT = 16f;
    private const float FIELD_RIGHT = 240f;
    private const float FIELD_TOP = 40f;
    private const float FIELD_BOTTOM = 184f;

    public static void StartPass(World world, int ballEntityId, int passerEntityId, int? targetEntityId, PassType passType)
    {
        // Lookup passer position.
        if (!TryGetPosition(world, passerEntityId, out var passerPos))
            return;

        // Determine target position (fallback to passer pos if target missing).
        var targetNow = passerPos;
        if (targetEntityId is int tid && TryGetPosition(world, tid, out var tp))
            targetNow = tp;

        // Duration based on distance.
        var dist = Vector2.Distance(passerPos, targetNow);
        var speed = passType == PassType.Lob ? 130f : 210f; // units/sec
        var duration = dist / MathF.Max(1f, speed);
        duration = MathHelper.Clamp(duration, 0.20f, 1.75f);

        // (Optional) lead targeting if we have velocity.
        var targetVelTick = Vector2.Zero;
        if (targetEntityId is int tid2 && TryGetVelocity(world, tid2, out var velTick))
            targetVelTick = velTick;
        var targetVelPerSec = targetVelTick * 60f;

        var predicted = targetNow + targetVelPerSec * duration;

        var end = new Vector2(
            MathHelper.Clamp(predicted.X, FIELD_LEFT, FIELD_RIGHT),
            MathHelper.Clamp(predicted.Y, FIELD_TOP, FIELD_BOTTOM));

        var apex = passType == PassType.Lob ? 16f : 8f;

        var nearestDefenderId = FindNearestDefender(world, end);
        var nearestDefenderPosition = end;
        if (nearestDefenderId > 0 && TryGetPosition(world, nearestDefenderId, out var defenderPos))
            nearestDefenderPosition = defenderPos;

        var intendedReceiverRoleId = RoleId.Unknown;
        var intendedReceiverSlot = string.Empty;
        if (targetEntityId is int explicitTargetId)
            TryGetReceiverMetadata(world, explicitTargetId, out intendedReceiverRoleId, out intendedReceiverSlot);

        // Apply to the ball.
        var qBall = new QueryDescription().WithAll<Ball, Position, Velocity>();
        world.Query(in qBall, (Entity e, ref Ball b, ref Position pos, ref Velocity vel) =>
        {
            if (e.Id != ballEntityId)
                return;

            b.State = BallState.InAir;
            b.OwnerEntityId = -1;

            b.FlightKind = BallFlightKind.Pass;
            b.PasserEntityId = passerEntityId;
            b.TargetEntityId = targetEntityId ?? 0;
            b.IntendedReceiverRoleId = intendedReceiverRoleId;
            b.IntendedReceiverSlot = intendedReceiverSlot;
            b.PassType = passType;
            b.PassTargetPosition = end;
            b.NearestDefenderEntityId = nearestDefenderId > 0 ? nearestDefenderId : 0;
            b.NearestDefenderPosition = nearestDefenderPosition;
            b.StartPos = passerPos;
            b.EndPos = end;
            b.DurationSeconds = duration;
            b.ElapsedSeconds = 0f;
            b.ApexHeight = apex;
            b.Height = 0f;
            b.IsComplete = false;

            // Place ball at start for determinism.
            pos.Value = passerPos;
            vel.Value = Vector2.Zero;
        });

        var requested = new PassRequestedEvent(passerEntityId, targetEntityId, passType);
        SimEventBus.Send(ref requested);
    }

    private static bool TryGetReceiverMetadata(World world, int entityId, out RoleId roleId, out string slot)
    {
        var localRoleId = RoleId.Unknown;
        var localSlot = string.Empty;
        var foundRole = false;
        var foundSlot = false;

        var qRole = new QueryDescription().WithAll<Role>();
        world.Query(in qRole, (Entity e, ref Role role) =>
        {
            if (foundRole || e.Id != entityId)
                return;

            localRoleId = role.Id;
            foundRole = true;
        });

        var qPlayerRole = new QueryDescription().WithAll<PlayerRole>();
        world.Query(in qPlayerRole, (Entity e, ref PlayerRole playerRole) =>
        {
            if (foundSlot || e.Id != entityId)
                return;

            localSlot = playerRole.Slot ?? string.Empty;
            foundSlot = true;
        });

        roleId = localRoleId;
        slot = string.IsNullOrWhiteSpace(localSlot) && localRoleId != RoleId.Unknown
            ? localRoleId.ToString()
            : localSlot;
        return foundRole || foundSlot;
    }

    private static int FindNearestDefender(World world, Vector2 targetPos)
    {
        var nearestId = -1;
        var bestDistSq = float.PositiveInfinity;

        var q = new QueryDescription().WithAll<Position, Team>();
        world.Query(in q, (Entity e, ref Position p, ref Team t) =>
        {
            if (t.IsOffense)
                return;

            var distSq = Vector2.DistanceSquared(p.Value, targetPos);
            if (distSq < bestDistSq - 0.0001f || (MathF.Abs(distSq - bestDistSq) <= 0.0001f && (nearestId < 0 || e.Id < nearestId)))
            {
                bestDistSq = distSq;
                nearestId = e.Id;
            }
        });

        return nearestId;
    }

    private static bool TryGetPosition(World world, int entityId, out Vector2 pos)
    {
        pos = default;
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

    private static bool TryGetVelocity(World world, int entityId, out Vector2 vel)
    {
        vel = default;
        var found = false;
        var result = Vector2.Zero;

        var q = new QueryDescription().WithAll<Velocity>();
        world.Query(in q, (Entity e, ref Velocity v) =>
        {
            if (found)
                return;
            if (e.Id != entityId)
                return;
            result = v.Value;
            found = true;
        });

        if (!found)
            return false;

        vel = result;
        return true;
    }
}
