using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// After <see cref="BallPhysicsSystem"/> updates the parametric flight, this system resolves
/// a completed pass flight into deterministic possession (auto-catch for now).
/// </summary>
public sealed class PassFlightCompleteSystem : EntityUpdateSystem
{
    private readonly GameEvents? _events;
    private readonly PlayState? _play;

    private ComponentMapper<BallComponent> _ballTag = null!;
    private ComponentMapper<BallStateComponent> _ballState = null!;
    private ComponentMapper<BallOwnerComponent> _ballOwner = null!;
    private ComponentMapper<BallFlightComponent> _flight = null!;
    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<BallCarrierComponent> _carrier = null!;

    public PassFlightCompleteSystem(GameEvents? events = null, PlayState? playState = null)
        : base(Aspect.All(typeof(BallComponent), typeof(BallStateComponent), typeof(BallOwnerComponent), typeof(PositionComponent)))
    {
        _events = events;
        _play = playState;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _ballTag = mapperService.GetMapper<BallComponent>();
        _ballState = mapperService.GetMapper<BallStateComponent>();
        _ballOwner = mapperService.GetMapper<BallOwnerComponent>();
        _flight = mapperService.GetMapper<BallFlightComponent>();
        _pos = mapperService.GetMapper<PositionComponent>();
        _carrier = mapperService.GetMapper<BallCarrierComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        foreach (var ballId in ActiveEntities)
        {
            if (!_ballTag.Has(ballId))
                continue;

            if (!_flight.Has(ballId))
                continue;

            var f = _flight.Get(ballId);
            if (f.Kind != BallFlightKind.Pass)
                continue;

            if (!f.IsComplete)
                continue;

            if (f.TargetId is not int receiverId)
                continue;

            // Deterministic auto-catch: give ball to the intended receiver.
            if (_carrier.Has(receiverId))
                _carrier.Get(receiverId).HasBall = true;

            if (f.PasserId is int passerId && _carrier.Has(passerId))
                _carrier.Get(passerId).HasBall = false;

            _ballState.Get(ballId).State = BallState.Held;
            _ballOwner.Get(ballId).OwnerEntityId = receiverId;

            if (_play is not null)
            {
                _play.BallState = BallState.Held;
                _play.BallOwnerEntityId = receiverId;
            }

            // Publish a caught event (used by kickoff slice + useful for logging).
            if (_events is not null && _pos.Has(receiverId))
                _events.Publish(new BallCaughtEvent(receiverId, _pos.Get(receiverId).Position));
        }
    }
}
