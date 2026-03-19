using System;
using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

// Ported from: src/TecmoSBGame/ArchiveMge/Systems/RushSystem.cs

/// <summary>
/// Defensive rush (SimArch scaffold).
///
/// Pattern:
/// - Each defender with <see cref="Rush"/> computes a gap landmark at play start.
/// - Move toward landmark until reached.
/// - Then transition to rushing/tracking QB.
///
/// Moves/counters are out of scope for now; this provides the backbone.
/// </summary>
public sealed class DefensiveRushSystem
{
    public float LandmarkReachRadius = 6f;

    public void Update(World world, float dtSeconds, IReadOnlyList<int> defenseEntityIds)
    {
        if (dtSeconds <= 0f)
            return;

        var qbId = FindQb(world);
        if (qbId < 0)
            return;

        if (!TryGetPosition(world, qbId, out var qbPos))
            return;

        var q = new QueryDescription().WithAll<Rush, Position, Behavior, Team>();

        // Build a fast allow-set for defense entity ids.
        var allow = new HashSet<int>(defenseEntityIds);

        var reachSq = LandmarkReachRadius * LandmarkReachRadius;

        world.Query(in q, (Entity e, ref Rush rush, ref Position pos, ref Behavior beh, ref Team team) =>
        {
            if (!allow.Contains(e.Id))
                return;
            if (team.IsOffense)
                return;

            if (!rush.HasLandmark)
            {
                rush.Landmark = ComputeLandmark(qbPos, rush.Assignment);
                rush.HasLandmark = true;
                rush.ReachedLandmark = false;
            }

            if (!rush.ReachedLandmark)
            {
                beh.State = BehaviorState.MovingToPosition;
                beh.TargetEntityId = -1;
                beh.TargetPosition = rush.Landmark;

                if (Vector2.DistanceSquared(pos.Value, rush.Landmark) <= reachSq)
                    rush.ReachedLandmark = true;

                return;
            }

            // After landmark: rush the QB.
            beh.State = BehaviorState.TrackingEntity;
            beh.TargetEntityId = qbId;
        });
    }

    private static Vector2 ComputeLandmark(Vector2 qbPos, RushAssignment a)
    {
        // Screen coords: X across field, Y downfield.
        // Put landmarks slightly "in front" of QB (positive Y) and offset laterally by gap.
        var basePos = qbPos + new Vector2(0, 14);

        var dx = a switch
        {
            RushAssignment.AGapLeft => -6,
            RushAssignment.AGapRight => 6,
            RushAssignment.BGapLeft => -14,
            RushAssignment.BGapRight => 14,
            RushAssignment.EdgeLeft => -26,
            RushAssignment.EdgeRight => 26,
            _ => 0,
        };

        return basePos + new Vector2(dx, 0);
    }

    private static int FindQb(World world)
    {
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
