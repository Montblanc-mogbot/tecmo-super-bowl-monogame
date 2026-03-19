using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Tecmo-inspired zone coverage.
///
/// Rules (approximate, deterministic):
/// - Defender drops to a landmark.
/// - If an eligible receiver enters the zone, defender matches/pursues after a reaction delay.
/// - Defender does not chase far outside the zone boundary; if threat leaves, return to landmark.
/// - When the ball is thrown (PassRequestedEvent / ball in-air), break toward the ball end-point.
///
/// Pattern-matching (approx):
/// - If a receiver leaves the zone shortly after entry, defender carries for a short window (15 frames)
///   before passing off and returning.
/// </summary>
public sealed class ZoneCoverageSystem : EntityUpdateSystem
{
    private readonly GameEvents? _events;
    private readonly PlayState? _play;

    private ComponentMapper<CoverageComponent> _cov = null!;
    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<BehaviorComponent> _behavior = null!;
    private ComponentMapper<PlayerAttributesComponent> _attr = null!;
    private ComponentMapper<TeamComponent> _team = null!;


    // Field bounds (keep in sync with other systems).
    private const float FIELD_LEFT = 16f;
    private const float FIELD_RIGHT = 240f;
    private const float FIELD_TOP = 40f;
    private const float FIELD_BOTTOM = 184f;

    // Pattern-match carry window (frames).
    private const int CARRY_FRAMES = 15;

    public ZoneCoverageSystem(GameEvents? events = null, PlayState? playState = null)
        : base(Aspect.All(typeof(PositionComponent), typeof(BehaviorComponent), typeof(TeamComponent)))
    {
        _events = events;
        _play = playState;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _cov = mapperService.GetMapper<CoverageComponent>();
        _pos = mapperService.GetMapper<PositionComponent>();
        _behavior = mapperService.GetMapper<BehaviorComponent>();
        _attr = mapperService.GetMapper<PlayerAttributesComponent>();
        _team = mapperService.GetMapper<TeamComponent>();

    }

    public override void Update(GameTime gameTime)
    {
        int? breakTargetEntityId = null;

        if (_events is not null)
        {
            var passes = _events.Read<PassRequestedEvent>();
            for (var i = 0; i < passes.Count; i++)
                breakTargetEntityId = passes[i].TargetId ?? breakTargetEntityId;
        }

        foreach (var defenderId in ActiveEntities)
        {
            if (!_cov.Has(defenderId))
                continue;

            var c = _cov.Get(defenderId);
            if (!IsZoneType(c.Type))
                continue;

            if (c.ReactionDelay <= 0)
                c.ReactionDelay = ComputeReactionDelayFrames(defenderId);

            // Initialize landmark deterministically (first update for this play).
            if (c.LandmarkPosition == Vector2.Zero)
                c.LandmarkPosition = ComputeLandmark(defenderId, c.Zone);

            // Ball in air -> break on throw.
            if ((_play is not null && _play.BallState == BallState.InAir) || breakTargetEntityId is not null)
            {
                // Break toward intended target (Tecmo collapses toward the throw).
                var point = breakTargetEntityId is not null && _pos.Has(breakTargetEntityId.Value)
                    ? _pos.Get(breakTargetEntityId.Value).Position
                    : c.LandmarkPosition;

                SetMoveTarget(defenderId, point);
                c.InPursuit = true;
                continue;
            }

            var defenderPos = _pos.Get(defenderId).Position;

            // Find best threat in zone.
            var (threatId, threatPos) = FindThreatInZone(defenderId, c);

            if (threatId >= 0)
            {
                // Reaction gate before starting pursuit.
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
                    // Pursue the threat but cap chase distance from landmark.
                    var maxChase = GetMaxChaseRadius(c.Type);
                    var distFromLandmark = Vector2.Distance(threatPos, c.LandmarkPosition);
                    if (distFromLandmark <= maxChase)
                    {
                        SetMoveTarget(defenderId, threatPos);
                    }
                    else
                    {
                        // Threat too far outside zone; pass off.
                        c.InPursuit = false;
                        c.PursuitTargetId = -1;
                        SetMoveTarget(defenderId, c.LandmarkPosition);
                    }
                }

                continue;
            }

            // No threat currently in zone.
            if (c.InPursuit && c.PursuitTargetId >= 0 && _pos.Has(c.PursuitTargetId))
            {
                // Pattern matching: carry briefly even after receiver exits.
                c.ReactionTimer++;
                if (c.ReactionTimer <= CARRY_FRAMES)
                {
                    var carryPos = _pos.Get(c.PursuitTargetId).Position;
                    SetMoveTarget(defenderId, carryPos);
                    continue;
                }

                c.InPursuit = false;
                c.PursuitTargetId = -1;
                c.ReactionTimer = 0;
            }

            // Return to landmark.
            c.InPursuit = false;
            c.PursuitTargetId = -1;
            c.ReactionTimer = 0;

            // Maintain landmark with small drift tolerance.
            if (Vector2.Distance(defenderPos, c.LandmarkPosition) > 2.5f)
                SetMoveTarget(defenderId, c.LandmarkPosition);
        }
    }

    private static bool IsZoneType(CoverageType t)
        => t is CoverageType.ZoneDeep or CoverageType.ZoneFlat or CoverageType.ZoneHook or CoverageType.ZoneCurl
            or CoverageType.DeepHalf or CoverageType.DeepThird or CoverageType.DeepQuarter;

    private (int threatId, Vector2 threatPos) FindThreatInZone(int defenderId, CoverageComponent c)
    {
        var defenderTeam = _team.Get(defenderId);

        int bestId = -1;
        var bestDist = float.MaxValue;
        var bestPos = Vector2.Zero;

        var zoneRect = GetZoneRect(c);

        foreach (var otherId in ActiveEntities)
        {
            if (otherId == defenderId)
                continue;

            // Only consider offensive-eligible targets (same tick deterministic).
            if (!_team.Has(otherId) || !_pos.Has(otherId))
                continue;

            var t = _team.Get(otherId);
            if (t.TeamIndex == defenderTeam.TeamIndex)
                continue;

            // Heuristic: only treat offensive players as threats.
            if (!t.IsOffense)
                continue;

            var p2 = _pos.Get(otherId).Position;
            if (!zoneRect.Contains(p2))
                continue;

            var d = Vector2.DistanceSquared(p2, _pos.Get(defenderId).Position);
            if (d < bestDist)
            {
                bestDist = d;
                bestId = otherId;
                bestPos = p2;
            }
        }

        return (bestId, bestPos);
    }


    private Rect GetZoneRect(CoverageComponent c)
    {
        // Rectangles are expressed in field coords.
        // Approximate Tecmo zones as boxes centered on landmark.
        var lm = c.LandmarkPosition == Vector2.Zero ? ComputeLandmarkFallback(c.Zone) : c.LandmarkPosition;

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

    private Vector2 ComputeLandmarkFallback(ZoneLandmark z)
    {
        // If LandmarkPosition hasn't been initialized yet, use a stable default near midfield.
        var mid = new Vector2((FIELD_LEFT + FIELD_RIGHT) * 0.5f, (FIELD_TOP + FIELD_BOTTOM) * 0.5f);
        return z switch
        {
            ZoneLandmark.DeepMiddle => mid + new Vector2(40, 0),
            ZoneLandmark.DeepLeft => mid + new Vector2(40, -30),
            ZoneLandmark.DeepRight => mid + new Vector2(40, 30),
            ZoneLandmark.FlatLeft => mid + new Vector2(10, -50),
            ZoneLandmark.FlatRight => mid + new Vector2(10, 50),
            ZoneLandmark.HookLeft => mid + new Vector2(20, -20),
            ZoneLandmark.HookRight => mid + new Vector2(20, 20),
            ZoneLandmark.CurlLeft => mid + new Vector2(28, -28),
            ZoneLandmark.CurlRight => mid + new Vector2(28, 28),
            _ => mid,
        };
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

    private Vector2 ComputeLandmark(int defenderId, ZoneLandmark z)
    {
        // For now, landmark coordinates are derived from the defender's starting position.
        // This is deterministic and works with formation-aligned spawns.
        var start = _pos.Get(defenderId).Position;

        // Nudge by zone type so defenders actually "drop".
        return z switch
        {
            ZoneLandmark.DeepMiddle => ClampToField(start + new Vector2(-24, 0)),
            ZoneLandmark.DeepLeft => ClampToField(start + new Vector2(-22, -18)),
            ZoneLandmark.DeepRight => ClampToField(start + new Vector2(-22, 18)),

            ZoneLandmark.FlatLeft => ClampToField(start + new Vector2(-10, -16)),
            ZoneLandmark.FlatRight => ClampToField(start + new Vector2(-10, 16)),

            ZoneLandmark.HookLeft => ClampToField(start + new Vector2(-14, -10)),
            ZoneLandmark.HookRight => ClampToField(start + new Vector2(-14, 10)),

            ZoneLandmark.CurlLeft => ClampToField(start + new Vector2(-18, -14)),
            ZoneLandmark.CurlRight => ClampToField(start + new Vector2(-18, 14)),

            _ => ClampToField(start),
        };
    }

    private static float GetMaxChaseRadius(CoverageType type)
    {
        // Approximate zone "box" as a circle radius in NES coords.
        return type switch
        {
            CoverageType.ZoneDeep or CoverageType.DeepHalf or CoverageType.DeepThird or CoverageType.DeepQuarter => 80f,
            CoverageType.ZoneHook or CoverageType.ZoneCurl => 45f,
            CoverageType.ZoneFlat => 30f,
            _ => 40f,
        };
    }

    private int ComputeReactionDelayFrames(int defenderId)
    {
        var rc = 50;
        if (_attr.Has(defenderId))
        {
            var a = _attr.Get(defenderId);
            if (a.Rec > 0)
                rc = a.Rec;
        }

        var delay = (100 - Math.Clamp(rc, 0, 100)) / 5;
        return Math.Clamp(delay, 0, 20);
    }

    private static Vector2 ClampToField(Vector2 p)
        => new(
            MathHelper.Clamp(p.X, FIELD_LEFT, FIELD_RIGHT),
            MathHelper.Clamp(p.Y, FIELD_TOP, FIELD_BOTTOM));

    private void SetMoveTarget(int entityId, Vector2 target)
    {
        var b = _behavior.Get(entityId);
        b.State = BehaviorState.MovingToPosition;
        b.TargetPosition = ClampToField(target);
    }
}
