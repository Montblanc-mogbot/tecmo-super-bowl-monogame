using System;
using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// QB dropback + read progression + pass decision (SimArch).
///
/// Current scope:
/// - Only runs when ball is held by the QB.
/// - Drop back for a fixed number of frames.
/// - Then start a pass flight toward the current read target.
///
/// This is intentionally minimal; fuller ROM-accurate QB logic comes later.
/// </summary>
public sealed class QbAiSystem
{
    // Default read order (slot names mirrored by RoleId).
    private static readonly RoleId[] ReadOrder =
    [
        RoleId.WR1,
        RoleId.WR2,
        RoleId.TE,
        RoleId.HB,
        RoleId.FB,
    ];

    public void Update(World world, float dtSeconds, int ballEntityId)
    {
        if (dtSeconds <= 0f)
            return;

        // Determine current ball owner.
        var (ballState, ownerId) = GetBallOwner(world, ballEntityId);
        if (ballState != BallState.Held || ownerId < 0)
            return;

        // Must be the QB.
        if (!TryGetRole(world, ownerId, out var role) || role.Id != RoleId.QB)
            return;

        // Tick QB brain + possibly request a pass.
        var q = new QueryDescription().WithAll<QbBrain>();
        world.Query(in q, (Entity e, ref QbBrain qb) =>
        {
            if (e.Id != ownerId)
                return;

            if (qb.PassRequested)
                return;

            // Tick dropback frames.
            var frames = Math.Max(0, (int)MathF.Round(dtSeconds * 60f));
            qb.DropbackFramesRemaining = Math.Max(0, qb.DropbackFramesRemaining - frames);
            if (qb.DropbackFramesRemaining > 0)
            {
                ApplyDropbackSteer(world, ownerId);
                return;
            }

            // Select a target based on read index.
            var targetId = PickTarget(world, qb.ReadIndex);
            if (targetId < 0)
            {
                // Advance to next read if possible.
                qb.ReadIndex = Math.Min(qb.ReadIndex + 1, ReadOrder.Length - 1);
                return;
            }

            PassFlightStartSystem.StartPass(world, ballEntityId, passerEntityId: ownerId, targetEntityId: targetId, qb.PassType);
            qb.PassRequested = true;

            Console.WriteLine($"[sim-arch] qb pass requested target={targetId} readIndex={qb.ReadIndex} type={qb.PassType}");
        });
    }

    private static int PickTarget(World world, int readIndex)
    {
        var idx = Math.Clamp(readIndex, 0, ReadOrder.Length - 1);
        var wanted = ReadOrder[idx];

        // Find first offense entity with matching RoleId.
        var found = -1;
        var q = new QueryDescription().WithAll<Role, Team>();
        world.Query(in q, (Entity e, ref Role r, ref Team t) =>
        {
            if (found != -1)
                return;
            if (!t.IsOffense)
                return;
            if (r.Id != wanted)
                return;

            found = e.Id;
        });

        return found;
    }

    private static void ApplyDropbackSteer(World world, int qbId)
    {
        // Deterministic gentle dropback: move a bit "upfield" in screen coords (negative Y).
        var q = new QueryDescription().WithAll<Behavior, Position>();
        world.Query(in q, (Entity e, ref Behavior b, ref Position p) =>
        {
            if (e.Id != qbId)
                return;

            b.State = BehaviorState.MovingToPosition;
            b.TargetPosition = p.Value + new Vector2(-12, -20);
        });
    }

    private static (BallState State, int OwnerId) GetBallOwner(World world, int ballEntityId)
    {
        var state = BallState.Dead;
        var owner = -1;
        var found = false;

        var q = new QueryDescription().WithAll<Ball>();
        world.Query(in q, (Entity e, ref Ball b) =>
        {
            if (found)
                return;
            if (e.Id != ballEntityId)
                return;

            state = b.State;
            owner = b.OwnerEntityId;
            found = true;
        });

        return (state, owner);
    }

    private static bool TryGetRole(World world, int entityId, out Role role)
    {
        role = default;
        var found = false;
        var local = default(Role);

        var q = new QueryDescription().WithAll<Role>();
        world.Query(in q, (Entity e, ref Role r) =>
        {
            if (found)
                return;
            if (e.Id != entityId)
                return;
            local = r;
            found = true;
        });

        if (!found)
            return false;

        role = local;
        return true;
    }
}
