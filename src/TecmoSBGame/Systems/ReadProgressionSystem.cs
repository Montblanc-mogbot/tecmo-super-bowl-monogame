using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Implements Tecmo-style QB read progression:
/// - 1st read -> 2nd -> 3rd -> scramble
/// - Stare-down timer per read
/// - Throw on route break window (not first-frame openness)
/// - Pressure -> step up/scramble
///
/// This system publishes <see cref="PassRequestedEvent"/> when the QB commits to a throw.
/// </summary>
public sealed class ReadProgressionSystem : EntityUpdateSystem
{
    private readonly GameEvents? _events;
    private readonly MatchState? _match;
    private readonly PlayState? _play;

    private ComponentMapper<QbBrainComponent> _brain = null!;
    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<VelocityComponent> _vel = null!;
    private ComponentMapper<PlayerRoleComponent> _role = null!;
    private ComponentMapper<BallCarrierComponent> _carrier = null!;
    private ComponentMapper<PlayerAttributesComponent> _attr = null!;

    private ComponentMapper<OffensiveAssignmentComponent> _offAssign = null!;
    private ComponentMapper<DefensiveAssignmentComponent> _defAssign = null!;
    private ComponentMapper<TeamComponent> _team = null!;

    // Pressure + openness parameters (NES pixels)
    private const float PRESSURE_RADIUS = 8f;
    private const float MAN_OPEN_SEPARATION = 2f;
    private const float ZONE_RADIUS = 12f;

    // Don't throw on the first frame of openness.
    private const int MIN_OPEN_FRAMES_BEFORE_THROW = 4;

    // Pocket move when pressured but not scrambling.
    private const float STEP_UP_PIXELS_PER_TICK = 0.6f;

    public ReadProgressionSystem(GameEvents? events = null, MatchState? matchState = null, PlayState? playState = null)
        : base(Aspect.All(typeof(QbBrainComponent), typeof(PositionComponent)))
    {
        _events = events;
        _match = matchState;
        _play = playState;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _brain = mapperService.GetMapper<QbBrainComponent>();
        _pos = mapperService.GetMapper<PositionComponent>();
        _vel = mapperService.GetMapper<VelocityComponent>();
        _role = mapperService.GetMapper<PlayerRoleComponent>();
        _carrier = mapperService.GetMapper<BallCarrierComponent>();
        _attr = mapperService.GetMapper<PlayerAttributesComponent>();

        _offAssign = mapperService.GetMapper<OffensiveAssignmentComponent>();
        _defAssign = mapperService.GetMapper<DefensiveAssignmentComponent>();
        _team = mapperService.GetMapper<TeamComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (_events is null)
            return;

        // Only during live play.
        if (_play is not null && _play.Phase != PlayPhase.InPlay)
            return;

        foreach (var qbId in ActiveEntities)
        {
            if (!_role.Has(qbId) || _role.Get(qbId).Role != PlayerRole.QB)
                continue;

            if (_carrier.Has(qbId) && !_carrier.Get(qbId).HasBall)
                continue;

            var brain = _brain.Get(qbId);
            if (brain.ThrowDecisionMade)
                continue;

            // Pressure detection + pocket response.
            UpdatePressureAndPocket(qbId, brain);

            if (brain.ScrambleMode)
            {
                UpdateScramble(qbId, brain);
                continue;
            }

            // Wait until dropback completes before progressing reads (shotgun completes immediately).
            if (!brain.DropbackComplete)
                continue;

            if (brain.ReadOrder.Count == 0)
                continue;

            brain.ReadTimer++;

            var readId = GetCurrentReadId(brain);
            if (readId is null)
            {
                // All reads exhausted.
                TryTriggerScramble(brain);
                continue;
            }

            // Evaluate throw window: route break + openness.
            if (_pos.Has(readId.Value) && IsRouteBreakWindow(readId.Value) && IsOpen(readId.Value, qbId))
            {
                if (brain.ReadTimer >= MIN_OPEN_FRAMES_BEFORE_THROW)
                {
                    CommitThrow(qbId, readId.Value, brain);
                    continue;
                }
            }

            // Move to next read if we stared too long.
            if (brain.ReadTimer > QbBrainComponent.READ_TIME_LIMIT)
            {
                brain.CurrentReadIndex++;
                brain.ReadTimer = 0;

                // If the next read doesn't exist, scramble check next frame.
                if (brain.CurrentReadIndex >= brain.ReadOrder.Count)
                    TryTriggerScramble(brain);
            }
        }
    }

    private int? GetCurrentReadId(QbBrainComponent brain)
    {
        if (brain.CurrentReadIndex < 0)
            brain.CurrentReadIndex = 0;

        if (brain.CurrentReadIndex >= brain.ReadOrder.Count)
            return null;

        var id = brain.ReadOrder[brain.CurrentReadIndex];
        return id <= 0 ? null : id;
    }

    private bool IsRouteBreakWindow(int receiverId)
    {
        // Approx: consider the "break" when the receiver reaches the first waypoint.
        // This is a deterministic proxy until we have per-route animation/break flags.
        if (!_offAssign.Has(receiverId))
            return true; // if we don't have route data, allow throws

        var oa = _offAssign.Get(receiverId);
        if (oa.RouteWaypoints.Count <= 0)
            return true;

        var breakPoint = oa.RouteWaypoints[0];
        var rPos = _pos.Get(receiverId).Position;
        return Vector2.DistanceSquared(rPos, breakPoint) <= (4f * 4f);
    }

    private bool IsOpen(int receiverId, int qbId)
    {
        // Determine defender relationship from defensive assignments.
        // If a man defender is assigned to this receiver, use man-open separation.
        // Otherwise treat as zone: open if outside all zone radii.

        var rPos = _pos.Get(receiverId).Position;

        float nearestManDist = float.PositiveInfinity;
        bool hasMan = false;

        foreach (var id in ActiveEntities)
        {
            if (!_defAssign.Has(id) || !_pos.Has(id))
                continue;

            var da = _defAssign.Get(id);
            if (da.Kind == DefensiveAssignmentKind.ManCoverage && da.TargetEntityId == receiverId)
            {
                hasMan = true;
                var d = Vector2.Distance(_pos.Get(id).Position, rPos);
                nearestManDist = Math.Min(nearestManDist, d);
            }
        }

        if (hasMan)
        {
            // Open if defender is not "in phase".
            return nearestManDist >= MAN_OPEN_SEPARATION;
        }

        // Zone: open if no zone defender is close enough to contest.
        foreach (var id in ActiveEntities)
        {
            if (!_defAssign.Has(id) || !_pos.Has(id))
                continue;

            var da = _defAssign.Get(id);
            if (da.Kind == DefensiveAssignmentKind.ZoneCoverage)
            {
                var d = Vector2.Distance(_pos.Get(id).Position, rPos);
                if (d <= ZONE_RADIUS)
                    return false;
            }
        }

        return true;
    }

    private void CommitThrow(int qbId, int receiverId, QbBrainComponent brain)
    {
        brain.ThrowDecisionMade = true;
        brain.TargetReceiverId = receiverId;

        // Compute an explicit lead point (also used for debug). PassFlightStartSystem still
        // performs its own deterministic lead from current receiver velocity.
        var qbPos = _pos.Get(qbId).Position;
        var rPos = _pos.Get(receiverId).Position;
        var rVelTick = _vel.Has(receiverId) ? _vel.Get(receiverId).Velocity : Vector2.Zero;

        // Approx ball speed: derive from QB Pass Accuracy (PA) if available, else stable default.
        var pa = _attr.Has(qbId) ? _attr.Get(qbId).Pa : 50;
        var ballSpeedPerSec = MathHelper.Lerp(170f, 250f, MathHelper.Clamp(pa / 100f, 0f, 1f));

        var dist = Vector2.Distance(qbPos, rPos);
        var timeToArrive = dist / MathF.Max(1f, ballSpeedPerSec);

        var leadTarget = rPos + (rVelTick * 60f) * timeToArrive;
        brain.ThrowTarget = leadTarget;

        _events.Publish(new PassRequestedEvent(PasserId: qbId, TargetId: receiverId, PassType: PassType.Bullet));
    }

    private void UpdatePressureAndPocket(int qbId, QbBrainComponent brain)
    {
        var qbPos = _pos.Get(qbId).Position;

        // Rushers: defenders with pass-rush assignment (or DL role as fallback).
        bool pressure = false;

        var qbTeamIndex = _team.Has(qbId) ? _team.Get(qbId).TeamIndex : (int?)null;

        foreach (var id in ActiveEntities)
        {
            if (id == qbId)
                continue;

            if (!_team.Has(id) || !_pos.Has(id))
                continue;

            // Only opponents can pressure.
            if (qbTeamIndex is not null && _team.Get(id).TeamIndex == qbTeamIndex.Value)
                continue;

            var isRusher = _defAssign.Has(id) && _defAssign.Get(id).Kind == DefensiveAssignmentKind.PassRush;
            if (!isRusher && _role.Has(id))
                isRusher = _role.Get(id).Role == PlayerRole.DL;

            if (!isRusher)
                continue;

            var d = Vector2.Distance(_pos.Get(id).Position, qbPos);
            if (d <= PRESSURE_RADIUS)
            {
                pressure = true;
                break;
            }
        }

        brain.PressureDetected = pressure;
        brain.PressureFrameCount = pressure
            ? brain.PressureFrameCount + 1
            : Math.Max(0, brain.PressureFrameCount - 2);

        // Step up in pocket when pressured but before scramble.
        if (pressure && brain.PressureFrameCount < QbBrainComponent.PRESSURE_THRESHOLD && brain.DropbackComplete && !brain.ScrambleMode)
        {
            var dir = _match?.OffenseDirection ?? OffenseDirection.LeftToRight;
            var towardLos = dir == OffenseDirection.LeftToRight ? 1f : -1f;
            _pos.Get(qbId).Position += new Vector2(STEP_UP_PIXELS_PER_TICK * towardLos, 0f);

            // Remove analog velocity to avoid double movement.
            if (_vel.Has(qbId))
                _vel.Get(qbId).Velocity = Vector2.Zero;
        }

        if (brain.PressureFrameCount >= QbBrainComponent.PRESSURE_THRESHOLD)
            TryTriggerScramble(brain);
    }

    private void TryTriggerScramble(QbBrainComponent brain)
    {
        // Trigger scramble when reads are exhausted OR pressure is sustained.
        // Lane detection is a placeholder until blocking/lanes are modeled.
        brain.ScrambleMode = true;
    }

    private void UpdateScramble(int qbId, QbBrainComponent brain)
    {
        // Minimal deterministic scramble: run toward LOS if QB has decent MS.
        var ms = _attr.Has(qbId) ? _attr.Get(qbId).Ms : 0;
        if (ms < 25)
            return;

        var dir = _match?.OffenseDirection ?? OffenseDirection.LeftToRight;
        var towardLos = dir == OffenseDirection.LeftToRight ? 1f : -1f;

        var speed = MathHelper.Lerp(0.8f, 1.6f, MathHelper.Clamp((ms - 25) / 50f, 0f, 1f));
        _pos.Get(qbId).Position += new Vector2(speed * towardLos, 0f);

        if (_vel.Has(qbId))
            _vel.Get(qbId).Velocity = Vector2.Zero;
    }
}
