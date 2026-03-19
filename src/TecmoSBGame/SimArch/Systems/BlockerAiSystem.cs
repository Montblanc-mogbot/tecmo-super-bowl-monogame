using System;
using System.Collections.Generic;
using System.Linq;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Assignment-based blocking AI (SimArch scaffold).
///
/// Current scope:
/// - For each offensive non-carrier entity with <see cref="BlockTarget"/>, pick a defender to block.
/// - Drive Behavior toward that defender (EngagementSystem turns contact into Engaged interrupts).
/// - Basic second-level release after engagement frames threshold.
/// </summary>
public sealed class BlockerAiSystem
{
    private const int RELEASE_TO_SECOND_LEVEL_AFTER_FRAMES = 28;

    public void Update(World world, float dtSeconds, IReadOnlyList<int> offenseEntityIds, IReadOnlyList<int> defenseEntityIds, int ballEntityId)
    {
        var carrierId = FindBallOwner(world, ballEntityId);
        var defenders = new HashSet<int>(defenseEntityIds);
        if (defenders.Count == 0)
            return;

        var frames = Math.Max(1, (int)MathF.Round(dtSeconds * 60f));

        foreach (var blockerId in offenseEntityIds.OrderBy(i => i))
        {
            if (blockerId == carrierId)
                continue;

            if (!TryGet(world, blockerId, out var bt, out var beh, out var pos, out var role))
                continue;

            SyncEngagementFlags(ref bt, in beh, frames);

            if (bt.IsEngaged)
                continue;

            if (bt.EngagementFrame >= RELEASE_TO_SECOND_LEVEL_AFTER_FRAMES)
            {
                bt.Assignment = BlockAssignmentType.SecondLevel;
                bt.TargetEntityId = -1;
            }

            if (bt.TargetEntityId == -1 || !defenders.Contains(bt.TargetEntityId))
                bt.TargetEntityId = ChooseTargetDefender(world, pos.Value, role.Id, bt.Assignment, defenders);

            if (bt.TargetEntityId == -1)
            {
                beh.State = BehaviorState.Idle;
                continue;
            }

            if (TryGetPosition(world, bt.TargetEntityId, out var targetPos))
            {
                beh.State = BehaviorState.MovingToPosition;
                beh.TargetEntityId = bt.TargetEntityId;
                beh.TargetPosition = targetPos;
            }
        }
    }

    private static void SyncEngagementFlags(ref BlockTarget bt, in Behavior beh, int frames)
    {
        if (beh.State == BehaviorState.Engaged)
        {
            if (!bt.IsEngaged)
            {
                bt.IsEngaged = true;
                bt.EngagedEntityId = beh.TargetEntityId;
                bt.EngagementFrame = 0;
            }
            else
            {
                bt.EngagementFrame += frames;
                bt.EngagedEntityId = beh.TargetEntityId;
            }

            return;
        }

        if (bt.IsEngaged)
        {
            bt.IsEngaged = false;
            bt.EngagedEntityId = -1;
            bt.IsDoubleTeam = false;
        }
    }

    private static int ChooseTargetDefender(World world, Vector2 blockerPos, RoleId roleId, BlockAssignmentType assignment, HashSet<int> defenders)
    {
        var laneBiasY = roleId switch
        {
            RoleId.LG or RoleId.LT => -12f,
            RoleId.RG or RoleId.RT => +12f,
            RoleId.OC => 0f,
            _ => 0f,
        };

        var desired = blockerPos;
        desired.Y += assignment switch
        {
            BlockAssignmentType.GapLeft => -10f,
            BlockAssignmentType.GapRight => +10f,
            BlockAssignmentType.PullLeft => -18f,
            BlockAssignmentType.PullRight => +18f,
            BlockAssignmentType.SecondLevel => laneBiasY * 1.2f,
            _ => laneBiasY,
        };

        var bestId = -1;
        var bestScore = float.PositiveInfinity;

        foreach (var defId in defenders)
        {
            if (!TryGetPosition(world, defId, out var defPos))
                continue;

            var d = defPos - desired;
            var distSq = d.LengthSquared();
            var score = distSq + (defId * 0.0001f);

            if (score < bestScore)
            {
                bestScore = score;
                bestId = defId;
            }
        }

        return bestId;
    }

    private static int FindBallOwner(World world, int ballEntityId)
    {
        var owner = -1;
        var found = false;

        var q = new QueryDescription().WithAll<Ball>();
        world.Query(in q, (Entity e, ref Ball b) =>
        {
            if (found)
                return;
            if (e.Id != ballEntityId)
                return;

            if (b.State == BallState.Held)
                owner = b.OwnerEntityId;

            found = true;
        });

        return owner;
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

    private static bool TryGet(World world, int entityId, out BlockTarget bt, out Behavior beh, out Position pos, out Role role)
    {
        bt = default;
        beh = default;
        pos = default;
        role = default;

        var found = false;
        var btLocal = default(BlockTarget);
        var bLocal = default(Behavior);
        var pLocal = default(Position);
        var rLocal = default(Role);

        var q = new QueryDescription().WithAll<BlockTarget, Behavior, Position, Role>();
        world.Query(in q, (Entity e, ref BlockTarget bt0, ref Behavior b0, ref Position p0, ref Role r0) =>
        {
            if (found)
                return;
            if (e.Id != entityId)
                return;

            btLocal = bt0;
            bLocal = b0;
            pLocal = p0;
            rLocal = r0;
            found = true;
        });

        if (!found)
            return false;

        bt = btLocal;
        beh = bLocal;
        pos = pLocal;
        role = rLocal;
        return true;
    }
}
