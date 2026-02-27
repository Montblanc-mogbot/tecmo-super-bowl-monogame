using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Field;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Keeps the dedicated ball entity snapped to the LOS during pre-snap.
///
/// This intentionally does not assign an owner; ownership is granted on snap by
/// <see cref="SnapResolutionSystem"/>.
/// </summary>
public sealed class PreSnapBallPlacementSystem : EntityUpdateSystem
{
    private readonly LoopState? _loop;
    private readonly MatchState? _match;
    private readonly PlayState? _play;

    private ComponentMapper<PositionComponent> _pos;
    private ComponentMapper<BallStateComponent> _ballState;
    private ComponentMapper<BallOwnerComponent> _ballOwner;

    public PreSnapBallPlacementSystem(LoopState? loop = null, MatchState? matchState = null, PlayState? playState = null)
        : base(Aspect.All(typeof(BallComponent), typeof(PositionComponent), typeof(BallStateComponent), typeof(BallOwnerComponent)))
    {
        _loop = loop;
        _match = matchState;
        _play = playState;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _pos = mapperService.GetMapper<PositionComponent>();
        _ballState = mapperService.GetMapper<BallStateComponent>();
        _ballOwner = mapperService.GetMapper<BallOwnerComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (_loop is not null && !_loop.IsOnField("pre_snap"))
            return;

        if (_play is not null)
        {
            if (_play.Phase != PlayPhase.PreSnap)
                return;

            if (_play.BallState != BallState.Dead)
                return;
        }

        if (_match is null)
            return;

        var losAbsYard = PlayState.ToAbsoluteYard(_match.BallSpot, _match.OffenseDirection);
        var losX = AbsoluteYardToX(losAbsYard);

        foreach (var ballId in ActiveEntities)
        {
            var p = _pos.Get(ballId);
            p.Position = new Vector2(losX, p.Position.Y);

            _ballState.Get(ballId).State = BallState.Dead;
            _ballOwner.Get(ballId).OwnerEntityId = null;
        }
    }

    private static float AbsoluteYardToX(int absoluteYard0To100)
    {
        absoluteYard0To100 = Math.Clamp(absoluteYard0To100, 0, 100);
        var t = absoluteYard0To100 / 100f;
        return FieldBounds.FieldLeftX + t * (FieldBounds.FieldRightX - FieldBounds.FieldLeftX);
    }
}
