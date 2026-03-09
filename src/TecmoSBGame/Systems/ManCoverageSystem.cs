using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Assignment-based man coverage.
///
/// Tecmo-inspired rules (approximate, deterministic):
/// - Defenders trail their assigned receiver with an RC-based delay.
/// - Maintain a small cushion; bias depth so they don't get beat deep.
/// - When the ball is thrown (PassRequestedEvent / ball in-air), break toward the ball's target/end.
/// </summary>
public sealed class ManCoverageSystem : EntityUpdateSystem
{
    private readonly GameEvents? _events;
    private readonly PlayState? _play;

    private ComponentMapper<CoverageComponent> _cov = null!;
    private ComponentMapper<DefensiveAssignmentComponent> _defAssign = null!;
    private ComponentMapper<OffensiveAssignmentComponent> _offAssign = null!;

    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<VelocityComponent> _vel = null!;
    private ComponentMapper<BehaviorComponent> _behavior = null!;
    private ComponentMapper<PlayerAttributesComponent> _attr = null!;
    private ComponentMapper<TeamComponent> _team = null!;


    // Field bounds (keep in sync with other systems).
    private const float FIELD_LEFT = 16f;
    private const float FIELD_RIGHT = 240f;
    private const float FIELD_TOP = 40f;
    private const float FIELD_BOTTOM = 184f;

    public ManCoverageSystem(GameEvents? events = null, PlayState? playState = null)
        : base(Aspect.All(typeof(CoverageComponent), typeof(PositionComponent), typeof(BehaviorComponent)))
    {
        _events = events;
        _play = playState;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _cov = mapperService.GetMapper<CoverageComponent>();
        _defAssign = mapperService.GetMapper<DefensiveAssignmentComponent>();
        _offAssign = mapperService.GetMapper<OffensiveAssignmentComponent>();

        _pos = mapperService.GetMapper<PositionComponent>();
        _vel = mapperService.GetMapper<VelocityComponent>();
        _behavior = mapperService.GetMapper<BehaviorComponent>();
        _attr = mapperService.GetMapper<PlayerAttributesComponent>();
        _team = mapperService.GetMapper<TeamComponent>();

    }

    public override void Update(GameTime gameTime)
    {
        // If a pass is requested this tick, allow an immediate global "break".
        int? breakTargetEntityId = null;

        if (_events is not null)
        {
            var passes = _events.Read<PassRequestedEvent>();
            for (var i = 0; i < passes.Count; i++)
            {
                var e = passes[i];
                breakTargetEntityId = e.TargetId ?? breakTargetEntityId;
            }

            // Prefer the ball end-point when available (after PassFlightStartSystem).
            if (breakTargetEntityId is not null)
            {
                var ballId = FindBallEntityId();
                if (ballId is not null && _flight.Has(ballId.Value))
                    breakTargetPoint = _flight.Get(ballId.Value).EndPos;
            }
        }

        foreach (var defenderId in ActiveEntities)
        {
            var c = _cov.Get(defenderId);
            if (c.Type != CoverageType.ManToMan)
                continue;

            // Resolve assignment from CoverageComponent first, fall back to DefensiveAssignmentComponent.
            var targetId = c.AssignmentTargetId;
            if (targetId < 0 && _defAssign.Has(defenderId))
                targetId = _defAssign.Get(defenderId).TargetEntityId;

            if (targetId < 0 || !_pos.Has(targetId))
                continue;

            var defenderPos = _pos.Get(defenderId).Position;
            var receiverPos = _pos.Get(targetId).Position;

            // Initialize reaction timing once.
            if (c.ReactionDelay <= 0)
                c.ReactionDelay = ComputeReactionDelayFrames(defenderId);

            // If ball is in air, everyone can break (Tecmo: coverage collapses toward the throw).
            if ((_play is not null && _play.BallState == BallState.InAir) || breakTargetEntityId is not null)
            {
                var point = receiverPos;
                SetMoveTarget(defenderId, point);
                c.InPursuit = true;
                c.PursuitTargetId = targetId;
                continue;
            }

            // Reaction delay gating: defender updates his mirror point only after delay frames.
            // Before that, he continues to pursue the last computed target (or stays put).
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
                continue;

            // Mirror logic (approx): trail slightly "behind" the receiver in X (offense assumed +X).
            var cushion = ComputeCushion(defenderId, targetId);

            // Depth priority: don't let WR cross your face deep.
            // If the receiver is beyond you in X, tighten cushion.
            if (receiverPos.X > defenderPos.X)
                cushion = MathF.Max(3f, cushion * 0.65f);

            var desired = new Vector2(receiverPos.X - cushion, receiverPos.Y);

            // Optional: bias inside leverage toward field center.
            var centerY = (FIELD_TOP + FIELD_BOTTOM) * 0.5f;
            var insideSign = receiverPos.Y < centerY ? 1f : -1f;
            desired.Y += insideSign * 2.5f;

            desired.X = MathHelper.Clamp(desired.X, FIELD_LEFT, FIELD_RIGHT);
            desired.Y = MathHelper.Clamp(desired.Y, FIELD_TOP, FIELD_BOTTOM);

            SetMoveTarget(defenderId, desired);
            c.InPursuit = true;
            c.PursuitTargetId = targetId;

            // After we update once, force another delay before the next cut reaction.
            c.HasReacted = false;
        }
    }

    private void SetMoveTarget(int entityId, Vector2 target)
    {
        var b = _behavior.Get(entityId);
        b.State = BehaviorState.MovingToPosition;
        b.TargetPosition = target;
    }

    private int ComputeReactionDelayFrames(int defenderId)
    {
        // Assembly-inspired approximation: Delay = (100 - RC) / 5.
        // We treat "REC" as a proxy for reaction/coverage skill (DBs use REC heavily in Tecmo).
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

    private float ComputeCushion(int defenderId, int receiverId)
    {
        // Cushion scales with MS differential.
        // Faster defender -> can play closer.
        var defMs = _attr.Has(defenderId) ? _attr.Get(defenderId).Ms : 50;
        var recMs = _attr.Has(receiverId) ? _attr.Get(receiverId).Ms : 50;

        var diff = Math.Clamp(defMs - recMs, -75, 75);
        // Base 10, adjust by ~0.05 per MS point.
        var cushion = 10f - diff * 0.05f;
        return MathHelper.Clamp(cushion, 4f, 14f);
    }
}
