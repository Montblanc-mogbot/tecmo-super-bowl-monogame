using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Simple loose-ball recovery:
/// - When the ball is Loose and unowned, find the nearest player within a small radius.
/// - Deterministic: distance first, then entity id tie-break.
/// </summary>
public sealed class LooseBallPickupSystem : EntityUpdateSystem
{
    private readonly GameEvents _events;
    private readonly PlayState _play;

    private ComponentMapper<BallComponent> _ballTag = null!;
    private ComponentMapper<BallStateComponent> _ballState = null!;
    private ComponentMapper<BallOwnerComponent> _ballOwner = null!;
    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<VelocityComponent> _vel = null!;
    private ComponentMapper<TeamComponent> _team = null!;
    private ComponentMapper<BallCarrierComponent> _carrier = null!;

    public const float PICKUP_RADIUS = 10f;

    public LooseBallPickupSystem(GameEvents events, PlayState playState) : base(Aspect.All(typeof(PositionComponent)))
    {
        _events = events;
        _play = playState;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _ballTag = mapperService.GetMapper<BallComponent>();
        _ballState = mapperService.GetMapper<BallStateComponent>();
        _ballOwner = mapperService.GetMapper<BallOwnerComponent>();
        _pos = mapperService.GetMapper<PositionComponent>();
        _vel = mapperService.GetMapper<VelocityComponent>();
        _team = mapperService.GetMapper<TeamComponent>();
        _carrier = mapperService.GetMapper<BallCarrierComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        var ballId = FindBallEntityId();
        if (ballId is null)
            return;

        var bid = ballId.Value;
        if (_ballState.Get(bid).State != BallState.Loose)
            return;

        if (_ballOwner.Get(bid).OwnerEntityId is not null)
            return;

        var ballPos = _pos.Get(bid).Position;
        var radiusSq = PICKUP_RADIUS * PICKUP_RADIUS;

        int? bestId = null;
        float bestDistSq = float.PositiveInfinity;

        foreach (var id in ActiveEntities)
        {
            if (id == bid)
                continue;

            // Only players (have team + ballcarrier components).
            if (!_team.Has(id) || !_carrier.Has(id) || !_pos.Has(id))
                continue;

            var d = _pos.Get(id).Position - ballPos;
            var distSq = d.LengthSquared();
            if (distSq > radiusSq)
                continue;

            if (distSq < bestDistSq - 0.0001f || (MathF.Abs(distSq - bestDistSq) <= 0.0001f && (bestId is null || id < bestId.Value)))
            {
                bestId = id;
                bestDistSq = distSq;
            }
        }

        if (bestId is null)
            return;

        var pickerId = bestId.Value;

        _ballOwner.Get(bid).OwnerEntityId = pickerId;
        _ballState.Get(bid).State = BallState.Held;
        if (_vel.Has(bid))
            _vel.Get(bid).Velocity = Vector2.Zero;

        _carrier.Get(pickerId).HasBall = true;

        _play.BallState = BallState.Held;
        _play.BallOwnerEntityId = pickerId;

        _events.Publish(new LooseBallPickupEvent(pickerId, ballPos));
    }

    private int? FindBallEntityId()
    {
        foreach (var id in ActiveEntities)
        {
            if (_ballTag.Has(id))
                return id;
        }

        return null;
    }
}
