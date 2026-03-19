using System;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// SimArch pre-snap placement scaffold.
///
/// MonoGame.Extended source:
/// - <c>PreSnapSystem</c>
/// - <c>PreSnapBallPlacementSystem</c>
///
/// SimArch does not yet model MatchState/PlayState/field coordinate transforms, so this implementation is
/// intentionally minimal and deterministic:
/// - When the ball is Dead (and not in flight), we treat the ball's current X as the line of scrimmage.
/// - We align all players by preserving their local-X offset from an offensive anchor (prefer center role).
/// - Defenders get a small separation offset.
///
/// This keeps "formation relative geometry" stable when other systems mutate absolute X.
/// </summary>
public sealed class PreSnapSystems
{
    // Rough 1-yard cushion analogue (pixels/units) to separate defenders from the LOS.
    private const float DEFENSE_SEPARATION = 2.0f;

    public void Update(World world, System.Collections.Generic.IReadOnlyList<int> offenseIds, System.Collections.Generic.IReadOnlyList<int> defenseIds, int ballEntityId, float offenseDirSign = 1f)
    {
        if (!TryGetBallLosX(world, ballEntityId, out var losX))
            return;

        if (!TryFindOffenseAnchorX(world, offenseIds, out var anchorX))
            return;

        // Align all players to LOS.
        var q = new QueryDescription().WithAll<Position, Team>();
        world.Query(in q, (Entity e, ref Position pos, ref Team team) =>
        {
            // Only apply to known roster entities (avoid affecting debug entities in the world).
            var isOff = Contains(offenseIds, e.Id);
            var isDef = !isOff && Contains(defenseIds, e.Id);
            if (!isOff && !isDef)
                return;

            var localX = pos.Value.X - anchorX;
            var x = losX + localX * offenseDirSign;

            if (!team.IsOffense)
                x += offenseDirSign * DEFENSE_SEPARATION;

            pos.Value = new Vector2(x, pos.Value.Y);
        });

        // Snap ball to LOS and clear any ownership/flight state.
        var qb = new QueryDescription().WithAll<Ball, Position, Velocity>();
        world.Query(in qb, (Entity e, ref Ball b, ref Position pos, ref Velocity vel) =>
        {
            if (e.Id != ballEntityId)
                return;

            pos.Value = new Vector2(losX, pos.Value.Y);

            b.State = TecmoSBGame.State.BallState.Dead;
            b.OwnerEntityId = -1;
            b.FlightKind = BallFlightKind.None;
            b.DurationSeconds = 0f;
            b.ElapsedSeconds = 0f;
            b.Height = 0f;
            b.IsComplete = false;

            vel.Value = Vector2.Zero;
        });
    }

    private static bool TryGetBallLosX(World world, int ballEntityId, out float losX)
    {
        losX = 0f;
        var found = false;
        var result = 0f;

        var q = new QueryDescription().WithAll<Ball, Position>();
        world.Query(in q, (Entity e, ref Ball b, ref Position p) =>
        {
            if (found)
                return;
            if (e.Id != ballEntityId)
                return;

            // Only run during a "dead ball" situation.
            if (b.State != TecmoSBGame.State.BallState.Dead)
                return;
            if (b.FlightKind != BallFlightKind.None)
                return;

            result = p.Value.X;
            found = true;
        });

        if (!found)
            return false;

        losX = result;
        return true;
    }

    private static bool TryFindOffenseAnchorX(World world, System.Collections.Generic.IReadOnlyList<int> offenseIds, out float anchorX)
    {
        anchorX = 0f;

        var fallbackSet = false;
        var fallbackX = 0f;

        var foundCenter = false;
        var centerX = 0f;

        var q = new QueryDescription().WithAll<Position, Team>();
        world.Query(in q, (Entity e, ref Position pos, ref Team team) =>
        {
            if (!Contains(offenseIds, e.Id))
                return;
            if (!team.IsOffense)
                return;

            if (!fallbackSet)
            {
                fallbackSet = true;
                fallbackX = pos.Value.X;
            }

            // Prefer Role == Center if present.
            if (!e.Has<Role>())
                return;

            var r = e.Get<Role>();
            if (r.Id == RoleId.OC)
            {
                foundCenter = true;
                centerX = pos.Value.X;
            }
        });

        if (foundCenter)
        {
            anchorX = centerX;
            return true;
        }

        if (fallbackSet)
        {
            anchorX = fallbackX;
            return true;
        }

        return false;
    }

    private static bool Contains(System.Collections.Generic.IReadOnlyList<int> ids, int id)
    {
        for (var i = 0; i < ids.Count; i++)
            if (ids[i] == id)
                return true;
        return false;
    }
}
