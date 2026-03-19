using System;
using System.Collections.Generic;
using System.Linq;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Combined man/zone coverage (SimArch scaffold).
///
/// Ported (approx) from legacy MGE ManCoverageSystem + ZoneCoverageSystem.
///
/// Notes:
/// - Uses Coverage.Type to choose man vs zone logic.
/// - When ball is in-air: defenders break toward the ball end-point.
/// - Reaction delay uses REC proxy; in SimArch we map to Ratings.RS for now.
/// </summary>
public sealed class CoverageSystem
{
    // Field bounds (keep in sync with other systems).
    private const float FIELD_LEFT = 16f;
    private const float FIELD_RIGHT = 240f;
    private const float FIELD_TOP = 40f;
    private const float FIELD_BOTTOM = 184f;

    private const int CARRY_FRAMES = 15;

    public void Update(World world, float dtSeconds, int ballEntityId, IReadOnlyList<int> defenseEntityIds)
    {
        if (dtSeconds <= 0f)
            return;

        // ball in-air and end-point
        var (ballState, endPos) = GetBallState(world, ballEntityId);
        var inAir = ballState == BallState.InAir;

        // Allow-list for defense ids (deterministic)
        var allow = new HashSet<int>(defenseEntityIds);

        var q = new QueryDescription().WithAll<Coverage, Position, Behavior, Team>();
        world.Query(in q, (Entity e, ref Coverage cov, ref Position pos, ref Behavior beh, ref Team team) =>
        {
            if (!allow.Contains(e.Id))
                return;
            if (team.IsOffense)
                return;

            if (cov.ReactionDelay <= 0)
                cov.ReactionDelay = ComputeReactionDelayFrames(world, e.Id);

            if (cov.Type == CoverageType.ManToMan)
            {
                UpdateMan(world, inAir, endPos, e.Id, ref cov, in pos, ref beh);
            }
            else
            {
                UpdateZone(world, inAir, endPos, e.Id, ref cov, in pos, ref beh, team.TeamIndex);
            }
        });
    }

    private void UpdateMan(World world, bool inAir, Vector2 ballEnd, int defenderId, ref Coverage c, in Position defenderPos0, ref Behavior beh)
    {
        var targetId = c.AssignmentTargetId;
        if (targetId < 0)
            return;

        if (!TryGetPosition(world, targetId, out var receiverPos))
            return;

        var defenderPos = defenderPos0.Value;

        if (c.ReactionDelay <= 0)
            c.ReactionDelay = ComputeReactionDelayFrames(world, defenderId);

        if (inAir)
        {
            SetMoveTarget(ref beh, ballEnd);
            c.InPursuit = true;
            c.PursuitTargetId = targetId;
            return;
        }

        if (!c.HasReacted)
        {
            c.ReactionTimer++;
            if (c.ReactionTimer >= c.ReactionDelay)
            {
                c.HasReacted = true;
                c.ReactionTimer = 0;
            }
        }

        if (!c.HasReacted)
            return;

        var cushion = ComputeCushion(world, defenderId, targetId);
        if (receiverPos.X > defenderPos.X)
            cushion = MathF.Max(3f, cushion * 0.65f);

        var desired = new Vector2(receiverPos.X - cushion, receiverPos.Y);

        // inside leverage
        var centerY = (FIELD_TOP + FIELD_BOTTOM) * 0.5f;
        var insideSign = receiverPos.Y < centerY ? 1f : -1f;
        desired.Y += insideSign * 2.5f;

        desired = ClampToField(desired);

        SetMoveTarget(ref beh, desired);
        c.InPursuit = true;
        c.PursuitTargetId = targetId;
        c.HasReacted = false;
    }

    private void UpdateZone(World world, bool inAir, Vector2 ballEnd, int defenderId, ref Coverage c, in Position defenderPos0, ref Behavior beh, int defenderTeamIndex)
    {
        if (c.LandmarkPosition == Vector2.Zero)
            c.LandmarkPosition = ComputeLandmark(world, defenderId, c.Zone);

        if (inAir)
        {
            SetMoveTarget(ref beh, ballEnd);
            c.InPursuit = true;
            return;
        }

        var (threatId, threatPos) = FindThreatInZone(world, defenderId, defenderTeamIndex, c);
        if (threatId >= 0)
        {
            if (!c.InPursuit)
            {
                c.ReactionTimer++;
                if (c.ReactionTimer >= c.ReactionDelay)
                {
                    c.InPursuit = true;
                    c.PursuitTargetId = threatId;
                    c.ReactionTimer = 0;
                }
            }

            if (c.InPursuit)
            {
                var maxChase = GetMaxChaseRadius(c.Type);
                var distFromLandmark = Vector2.Distance(threatPos, c.LandmarkPosition);
                if (distFromLandmark <= maxChase)
                    SetMoveTarget(ref beh, threatPos);
                else
                {
                    c.InPursuit = false;
                    c.PursuitTargetId = -1;
                    SetMoveTarget(ref beh, c.LandmarkPosition);
                }
            }

            return;
        }

        // No threat.
        if (c.InPursuit && c.PursuitTargetId >= 0 && TryGetPosition(world, c.PursuitTargetId, out var carryPos))
        {
            c.ReactionTimer++;
            if (c.ReactionTimer <= CARRY_FRAMES)
            {
                SetMoveTarget(ref beh, carryPos);
                return;
            }

            c.InPursuit = false;
            c.PursuitTargetId = -1;
            c.ReactionTimer = 0;
        }

        c.InPursuit = false;
        c.PursuitTargetId = -1;
        c.ReactionTimer = 0;

        var defenderPos = defenderPos0.Value;
        if (Vector2.Distance(defenderPos, c.LandmarkPosition) > 2.5f)
            SetMoveTarget(ref beh, c.LandmarkPosition);
    }

    private static void SetMoveTarget(ref Behavior b, Vector2 target)
    {
        b.State = BehaviorState.MovingToPosition;
        b.TargetEntityId = -1;
        b.TargetPosition = ClampToField(target);
    }

    private static Vector2 ClampToField(Vector2 p)
        => new(
            MathHelper.Clamp(p.X, FIELD_LEFT, FIELD_RIGHT),
            MathHelper.Clamp(p.Y, FIELD_TOP, FIELD_BOTTOM));

    private static float GetMaxChaseRadius(CoverageType type)
    {
        return type switch
        {
            CoverageType.ZoneDeep or CoverageType.DeepHalf or CoverageType.DeepThird or CoverageType.DeepQuarter => 80f,
            CoverageType.ZoneHook or CoverageType.ZoneCurl => 45f,
            CoverageType.ZoneFlat => 30f,
            _ => 40f,
        };
    }

    private static int ComputeReactionDelayFrames(World world, int defenderId)
    {
        // Tecmo used REC as a proxy for reaction; we map to Ratings.RS for now.
        var rc = 50;
        if (TryGetRatings(world, defenderId, out var r) && r.RS > 0)
            rc = r.RS;

        var delay = (100 - Math.Clamp(rc, 0, 100)) / 5;
        return Math.Clamp(delay, 0, 20);
    }

    private static float ComputeCushion(World world, int defenderId, int receiverId)
    {
        var defMs = TryGetRatings(world, defenderId, out var dr) ? dr.MS : 50;
        var recMs = TryGetRatings(world, receiverId, out var rr) ? rr.MS : 50;

        var diff = Math.Clamp(defMs - recMs, -75, 75);
        var cushion = 10f - diff * 0.05f;
        return MathHelper.Clamp(cushion, 4f, 14f);
    }

    private static (BallState state, Vector2 endPos) GetBallState(World world, int ballEntityId)
    {
        var state = BallState.Dead;
        var endPos = Vector2.Zero;
        var found = false;

        var q = new QueryDescription().WithAll<Ball>();
        world.Query(in q, (Entity e, ref Ball b) =>
        {
            if (found)
                return;
            if (e.Id != ballEntityId)
                return;

            state = b.State;
            endPos = b.EndPos;
            found = true;
        });

        return (state, endPos);
    }

    private static Vector2 ComputeLandmark(World world, int defenderId, ZoneLandmark z)
    {
        var start = Vector2.Zero;
        if (!TryGetPosition(world, defenderId, out start))
            start = new Vector2((FIELD_LEFT + FIELD_RIGHT) * 0.5f, (FIELD_TOP + FIELD_BOTTOM) * 0.5f);

        Vector2 p = z switch
        {
            ZoneLandmark.DeepMiddle => start + new Vector2(-24, 0),
            ZoneLandmark.DeepLeft => start + new Vector2(-22, -18),
            ZoneLandmark.DeepRight => start + new Vector2(-22, 18),

            ZoneLandmark.FlatLeft => start + new Vector2(-10, -16),
            ZoneLandmark.FlatRight => start + new Vector2(-10, 16),

            ZoneLandmark.HookLeft => start + new Vector2(-14, -10),
            ZoneLandmark.HookRight => start + new Vector2(-14, 10),

            ZoneLandmark.CurlLeft => start + new Vector2(-18, -14),
            ZoneLandmark.CurlRight => start + new Vector2(-18, 14),

            _ => start,
        };

        return ClampToField(p);
    }

    private static (int threatId, Vector2 threatPos) FindThreatInZone(World world, int defenderId, int defenderTeamIndex, Coverage c)
    {
        var zoneRect = GetZoneRect(c);
        var bestId = -1;
        var bestDist = float.MaxValue;
        var bestPos = Vector2.Zero;

        // Iterate all offense positions.
        var q = new QueryDescription().WithAll<Position, Team>();
        world.Query(in q, (Entity e, ref Position p, ref Team t) =>
        {
            if (!t.IsOffense)
                return;
            if (t.TeamIndex == defenderTeamIndex)
                return;

            var pos = p.Value;
            if (!zoneRect.Contains(pos))
                return;

            if (!TryGetPosition(world, defenderId, out var defPos))
                return;

            var d = Vector2.DistanceSquared(pos, defPos);
            if (d < bestDist)
            {
                bestDist = d;
                bestId = e.Id;
                bestPos = pos;
            }
        });

        return (bestId, bestPos);
    }

    private readonly record struct Rect(float Left, float Top, float Right, float Bottom)
    {
        public static Rect FromCenter(Vector2 c, float width, float height)
        {
            var hw = width * 0.5f;
            var hh = height * 0.5f;
            return new Rect(c.X - hw, c.Y - hh, c.X + hw, c.Y + hh);
        }

        public bool Contains(Vector2 p)
            => p.X >= Left && p.X <= Right && p.Y >= Top && p.Y <= Bottom;
    }

    private static Rect GetZoneRect(Coverage c)
    {
        var lm = c.LandmarkPosition == Vector2.Zero
            ? new Vector2((FIELD_LEFT + FIELD_RIGHT) * 0.5f, (FIELD_TOP + FIELD_BOTTOM) * 0.5f)
            : c.LandmarkPosition;

        var (w, h) = c.Type switch
        {
            CoverageType.ZoneDeep or CoverageType.DeepHalf or CoverageType.DeepThird or CoverageType.DeepQuarter => (80f, 70f),
            CoverageType.ZoneHook => (60f, 45f),
            CoverageType.ZoneCurl => (70f, 50f),
            CoverageType.ZoneFlat => (55f, 35f),
            _ => (60f, 45f),
        };

        return Rect.FromCenter(lm, w, h);
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

    private static bool TryGetRatings(World world, int entityId, out Ratings ratings)
    {
        ratings = default;
        var found = false;
        var local = default(Ratings);

        var q = new QueryDescription().WithAll<Ratings>();
        world.Query(in q, (Entity e, ref Ratings r) =>
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

        ratings = local;
        return true;
    }
}
