using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.Field;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Detects ball out-of-bounds and simple end zone outcomes (touchback/safety).
///
/// This is intentionally conservative and deterministic; Tecmo-accurate edge cases
/// (momentum, possession changes in end zone, etc.) can be refined later.
/// </summary>
public sealed class BallBoundsSystem : EntityUpdateSystem
{
    private readonly GameEvents? _events;
    private readonly MatchState _match;
    private readonly PlayState _play;

    private ComponentMapper<BallComponent> _ballTag = null!;
    private ComponentMapper<BallStateComponent> _ballState = null!;
    private ComponentMapper<BallOwnerComponent> _ballOwner = null!;
    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<TeamComponent> _team = null!;

    public BallBoundsSystem(GameEvents? events, MatchState matchState, PlayState playState)
        : base(Aspect.All(typeof(BallComponent), typeof(PositionComponent), typeof(BallStateComponent), typeof(BallOwnerComponent)))
    {
        _events = events;
        _match = matchState;
        _play = playState;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _ballTag = mapperService.GetMapper<BallComponent>();
        _ballState = mapperService.GetMapper<BallStateComponent>();
        _ballOwner = mapperService.GetMapper<BallOwnerComponent>();
        _pos = mapperService.GetMapper<PositionComponent>();
        _team = mapperService.GetMapper<TeamComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (_play.IsOver)
            return;

        foreach (var ballId in ActiveEntities)
        {
            if (!_ballTag.Has(ballId))
                continue;

            var state = _ballState.Get(ballId).State;
            if (state is not (BallState.InAir or BallState.Loose))
                continue;

            var pos = _pos.Get(ballId).Position;

            // Sidelines/top/bottom: always out-of-bounds.
            if (FieldBounds.IsOutOfBoundsSidelines(pos))
            {
                EndPlay(WhistleReason.OutOfBounds, "bounds:oob", pos);
                return;
            }

            // Goal-line / end zone checks. (X is downfield.)
            var beyondLeft = FieldBounds.IsBeyondLeftGoalLine(pos);
            var beyondRight = FieldBounds.IsBeyondRightGoalLine(pos);
            if (!beyondLeft && !beyondRight)
            {
                // Still between goal lines.
                // If it somehow left the overall rect (shouldn't happen unless far out), treat as OOB.
                if (FieldBounds.IsOutOfAllField(pos))
                {
                    EndPlay(WhistleReason.OutOfBounds, "bounds:oob", pos);
                    return;
                }

                continue;
            }

            var endZoneIsLeft = beyondLeft;
            var ownEndZoneIsLeft = _match.OffenseDirection == OffenseDirection.LeftToRight;
            var isOwnEndZone = endZoneIsLeft == ownEndZoneIsLeft;

            // Decide safety vs touchback.
            // - If offense still possesses the ball and it ends up behind its own goal line => safety.
            // - Otherwise (e.g. kickoff into end zone) => touchback.
            var ownerId = _play.BallOwnerEntityId;
            var offensePossesses = false;
            if (ownerId is int oid && _team.Has(oid))
            {
                // We treat MatchState.PossessionTeam as the offensive possession team.
                offensePossesses = _team.Get(oid).TeamIndex == _match.PossessionTeam;
            }

            if (isOwnEndZone && ownerId is not null && offensePossesses)
            {
                _play.Result = _play.Result with { Safety = true };
                EndPlay(WhistleReason.Safety, "bounds:safety", pos);
                return;
            }

            EndPlay(WhistleReason.Touchback, "bounds:touchback", pos);
            return;
        }
    }

    private void EndPlay(WhistleReason reason, string whistleEventReason, Vector2 ballPos)
    {
        if (_play.WhistleReason != WhistleReason.None)
            return;

        _play.WhistleReason = reason;
        _play.Phase = PlayPhase.PostPlay;
        _play.BallState = BallState.Dead;

        // Record an end spot for logging; clamp to the 0..100 interval.
        _play.EndAbsoluteYard = FieldBounds.XToAbsoluteYard(ballPos.X);

        _events?.Publish(new WhistleEvent(whistleEventReason));
    }
}
