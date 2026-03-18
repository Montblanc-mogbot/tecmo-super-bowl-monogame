using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Consumes <see cref="PassRequestedEvent"/> and converts it into a deterministic in-flight ball trajectory.
///
/// This system is intentionally simple:
/// - Chooses a deterministic default target when none is provided.
/// - Computes a constant-speed flight duration from distance.
/// - "Leads" the receiver using constant-velocity prediction (receiverVel * duration).
///
/// Catch/intercept resolution will be handled by a later system.
/// </summary>
public sealed class PassFlightStartSystem : EntityUpdateSystem
{
    private readonly GameEvents? _events;
    private readonly PlayState? _play;

    private ComponentMapper<BallComponent> _ball = null!;
    private ComponentMapper<PositionComponent> _pos = null!;

    private ComponentMapper<TeamComponent> _team = null!;
    private ComponentMapper<VelocityComponent> _vel = null!;
    private ComponentMapper<BallCarrierComponent> _carrier = null!;

    // Field bounds (shared with kickoff + collision systems).
    private const float FIELD_LEFT = 16f;
    private const float FIELD_RIGHT = 240f;
    private const float FIELD_TOP = 40f;
    private const float FIELD_BOTTOM = 184f;

    public PassFlightStartSystem(GameEvents? events = null, PlayState? playState = null)
        : base(Aspect.All(typeof(PositionComponent)))
    {
        _events = events;
        _play = playState;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _ball = mapperService.GetMapper<BallComponent>();
        _pos = mapperService.GetMapper<PositionComponent>();

        _team = mapperService.GetMapper<TeamComponent>();
        _vel = mapperService.GetMapper<VelocityComponent>();
        _carrier = mapperService.GetMapper<BallCarrierComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (_events is null)
            return;

        // There should be exactly one dedicated ball entity; we treat the first one as authoritative.
        // (Kickoff slice spawns one; later slices should do the same.)
        int? ballEntityId = null;
        foreach (var id in ActiveEntities)
        {
            if (_ball.Has(id))
            {
                ballEntityId = id;
                break;
            }
        }

        if (ballEntityId is null)
            return;

        var ballId = ballEntityId.Value;

        _events.Drain<PassRequestedEvent>(e =>
        {
            if (!_pos.Has(ballId))
                return;

            var passerId = e.PasserId;
            if (!_pos.Has(passerId) || !_team.Has(passerId))
                return;

            var passerTeam = _team.Get(passerId);

            var chosenTarget = e.TargetId;
            if (chosenTarget is null)
                chosenTarget = ChooseDefaultTarget(passerId, passerTeam.TeamIndex);

            if (chosenTarget is null || !_pos.Has(chosenTarget.Value))
                return;

            // Clear any player-held ball state (best-effort).
            if (_carrier.Has(passerId))
                _carrier.Get(passerId).HasBall = false;

            // Start at passer position for determinism (do not depend on the ball entity being perfectly synced).
            var start = _pos.Get(passerId).Position;

            // Compute duration from the *current* target position (no lead yet).
            var targetNow = _pos.Get(chosenTarget.Value).Position;
            var dist = Vector2.Distance(start, targetNow);

            var passType = e.PassType;
            var speed = passType == PassType.Lob ? 130f : 210f; // units/sec
            var duration = dist / MathF.Max(1f, speed);
            duration = MathHelper.Clamp(duration, 0.20f, 1.75f);

            // Lead targeting using constant-velocity prediction.
            var targetVelTick = _vel.Has(chosenTarget.Value) ? _vel.Get(chosenTarget.Value).Velocity : Vector2.Zero;
            var targetVelPerSec = targetVelTick * 60f;
            var predicted = targetNow + targetVelPerSec * duration;

            var end = new Vector2(
                MathHelper.Clamp(predicted.X, FIELD_LEFT, FIELD_RIGHT),
                MathHelper.Clamp(predicted.Y, FIELD_TOP, FIELD_BOTTOM));

            var apex = passType == PassType.Lob ? 16f : 8f;

            // Put ball into in-air state immediately.
            if (_play is not null)
            {
                _play.BallState = BallState.InAir;
                _play.BallOwnerEntityId = null;
            }

            var b = _ball.Get(ballId);
            b.State = BallState.InAir;
            b.OwnerEntityId = null;

            // Place ball at the start now.
            _pos.Get(ballId).Position = start;

            // Overwrite flight model.
            b.FlightKind = BallFlightKind.Pass;
            b.PasserId = passerId;
            b.TargetId = chosenTarget;
            b.PassType = passType;
            b.StartPos = start;
            b.EndPos = end;
            b.DurationSeconds = duration;
            b.ApexHeight = apex;
            b.ElapsedSeconds = 0f;
            b.Height = 0f;
            b.IsComplete = false;
        });
    }

    private int? ChooseDefaultTarget(int passerId, int passerTeamIndex)
    {
        // Deterministic rule: nearest entity on the same offense team (exclude passer).
        // Tie-break: smallest entity id.
        var passerPos = _pos.Get(passerId).Position;

        float bestDistSq = float.PositiveInfinity;
        int bestId = -1;

        foreach (var id in ActiveEntities)
        {
            if (id == passerId)
                continue;

            if (!_team.Has(id) || !_pos.Has(id))
                continue;

            var t = _team.Get(id);
            if (t.TeamIndex != passerTeamIndex)
                continue;

            if (!t.IsOffense)
                continue;

            var d = _pos.Get(id).Position - passerPos;
            var distSq = d.LengthSquared();

            if (distSq < bestDistSq - 0.0001f)
            {
                bestDistSq = distSq;
                bestId = id;
            }
            else if (MathF.Abs(distSq - bestDistSq) <= 0.0001f && id < bestId)
            {
                bestId = id;
            }
        }

        return bestId == -1 ? null : bestId;
    }
}
