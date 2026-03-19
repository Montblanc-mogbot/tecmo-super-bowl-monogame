using System;
using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Deterministic player control selection rules (SimArch).
///
/// Rules (current):
/// - If user-side is offense: control ballcarrier when ball is held; otherwise control QB/passer.
/// - If user-side is defense: control the nearest defender to the pursuit target
///   (ballcarrier when held; ball end-position when in-air).
///
/// Note: "user-side" is inferred from current ball possession (ball owner team).
/// This keeps things deterministic until a UI allows explicit side selection.
/// </summary>
public sealed class PlayerControlSystem
{
    public void Update(World world, float dtSeconds, int ballEntityId, ref Control control)
    {
        _ = dtSeconds;

        if (!TryGetBall(world, ballEntityId, out var ball))
            return;

        // If something already forced control this tick (handoff etc), respect it.
        if (control.PendingForcedEntityId > 0)
        {
            control.ControlledEntityId = control.PendingForcedEntityId;
            control.PendingForcedEntityId = -1;
            return;
        }

        // Determine who is "user side" based on ball ownership.
        var owner = ball.OwnerEntityId;
        var ownerIsOffense = owner > 0 && TryGetTeam(world, owner, out var ownerTeam) && ownerTeam.IsOffense;

        // Offense control.
        if (ownerIsOffense)
        {
            var desired = PickOffenseControlledEntity(world, in ball);
            if (desired > 0)
                control.ControlledEntityId = desired;
            return;
        }

        // Defense control (or no owner yet): pick nearest defender to target.
        var targetPos = PickDefenseTargetPosition(world, in ball);
        var defenderId = FindNearestDefender(world, targetPos);
        if (defenderId > 0)
            control.ControlledEntityId = defenderId;
    }

    private static int PickOffenseControlledEntity(World world, in Ball ball)
    {
        if (ball.State == BallState.Held && ball.OwnerEntityId > 0)
            return ball.OwnerEntityId;

        if (ball.State == BallState.InAir && ball.PasserEntityId > 0)
            return ball.PasserEntityId;

        // Fallback: QB.
        var qb = -1;
        var q = new QueryDescription().WithAll<Role, Team>();
        world.Query(in q, (Entity e, ref Role r, ref Team t) =>
        {
            if (qb != -1)
                return;
            if (!t.IsOffense)
                return;
            if (r.Id != RoleId.QB)
                return;
            qb = e.Id;
        });

        return qb;
    }

    private static Vector2 PickDefenseTargetPosition(World world, in Ball ball)
    {
        if (ball.State == BallState.Held && ball.OwnerEntityId > 0)
        {
            if (TryGetPosition(world, ball.OwnerEntityId, out var p))
                return p;
        }

        if (ball.State == BallState.InAir)
            return ball.EndPos;

        // Default: center field.
        return new Vector2(128, 112);
    }

    private static int FindNearestDefender(World world, Vector2 targetPos)
    {
        var bestId = -1;
        var best = float.PositiveInfinity;

        var q = new QueryDescription().WithAll<Position, Team>();
        world.Query(in q, (Entity e, ref Position p, ref Team t) =>
        {
            if (t.IsOffense)
                return;

            var d = Vector2.DistanceSquared(p.Value, targetPos);
            var score = d + (e.Id * 0.0001f); // deterministic tie-break
            if (score < best)
            {
                best = score;
                bestId = e.Id;
            }
        });

        return bestId;
    }

    private static bool TryGetBall(World world, int ballEntityId, out Ball ball)
    {
        ball = default;
        var found = false;
        var local = default(Ball);

        var q = new QueryDescription().WithAll<Ball>();
        world.Query(in q, (Entity e, ref Ball b) =>
        {
            if (found)
                return;
            if (e.Id != ballEntityId)
                return;
            local = b;
            found = true;
        });

        if (!found)
            return false;

        ball = local;
        return true;
    }

    private static bool TryGetTeam(World world, int entityId, out Team team)
    {
        team = default;
        var found = false;
        var local = default(Team);

        var q = new QueryDescription().WithAll<Team>();
        world.Query(in q, (Entity e, ref Team t) =>
        {
            if (found)
                return;
            if (e.Id != entityId)
                return;
            local = t;
            found = true;
        });

        if (!found)
            return false;

        team = local;
        return true;
    }

    private static bool TryGetPosition(World world, int entityId, out Vector2 pos)
    {
        pos = default;
        var found = false;
        var local = Vector2.Zero;

        var q = new QueryDescription().WithAll<Position>();
        world.Query(in q, (Entity e, ref Position p) =>
        {
            if (found)
                return;
            if (e.Id != entityId)
                return;
            local = p.Value;
            found = true;
        });

        if (!found)
            return false;

        pos = local;
        return true;
    }
}
