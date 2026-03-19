using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Kickoff coverage AI (lane -> pursue returner).
///
/// This is a deterministic scaffold:
/// - Coverage players run to a lane landmark first.
/// - Once the returner possesses the ball (BallCarrierComponent.HasBall), coverage breaks on the returner.
/// - Contain players bias their target slightly outside the returner (simple leverage).
///
/// Entities are expected to be tagged with <see cref="KickoffCoverageComponent"/> during kickoff spawns.
/// </summary>
public sealed class KickoffCoverageSystem : EntityUpdateSystem
{
    private readonly PlayState _play;
    private readonly MatchState _match;

    private ComponentMapper<KickoffCoverageComponent> _cov = null!;
    private ComponentMapper<BehaviorComponent> _behavior = null!;
    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<TeamComponent> _team = null!;
    private ComponentMapper<BallCarrierComponent> _carrier = null!;

    public KickoffCoverageSystem(PlayState play, MatchState match)
        : base(Aspect.All(typeof(KickoffCoverageComponent), typeof(BehaviorComponent), typeof(PositionComponent), typeof(TeamComponent)))
    {
        _play = play ?? throw new ArgumentNullException(nameof(play));
        _match = match ?? throw new ArgumentNullException(nameof(match));
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _cov = mapperService.GetMapper<KickoffCoverageComponent>();
        _behavior = mapperService.GetMapper<BehaviorComponent>();
        _pos = mapperService.GetMapper<PositionComponent>();
        _team = mapperService.GetMapper<TeamComponent>();
        _carrier = mapperService.GetMapper<BallCarrierComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (_play.Phase != PlayPhase.InPlay || _play.IsOver)
            return;

        // Kickoff slice: no passing. Once scrimmage starts, allow-pass becomes true.
        if (_play.AllowPass)
            return;

        foreach (var id in ActiveEntities)
        {
            var t = _team.Get(id);
            if (t.IsOffense)
                continue; // coverage team acts as defense during kickoff

            var c = _cov.Get(id);
            var b = _behavior.Get(id);

            var returnerId = c.ReturnerEntityId;
            var returnerPos = (returnerId != -1 && _pos.Has(returnerId))
                ? _pos.Get(returnerId).Position
                : Vector2.Zero;

            // If returner has the ball, break immediately.
            if (returnerId != -1 && _carrier.Has(returnerId) && _carrier.Get(returnerId).HasBall)
                c.BreakOnReturner = true;

            var target = c.BreakOnReturner
                ? ComputePursuitTarget(id, c, returnerId, returnerPos)
                : c.LaneLandmark;

            b.State = BehaviorState.MovingToPosition;
            b.TargetPosition = target;
        }
    }

    private Vector2 ComputePursuitTarget(int defenderId, KickoffCoverageComponent cov, int returnerId, Vector2 returnerPos)
    {
        if (returnerId == -1)
            return cov.LaneLandmark;

        if (!cov.IsContain)
            return returnerPos;

        // Simple contain leverage: push slightly outside the returner relative to field center.
        // Field bounds constants (keep in sync with other systems).
        const float fieldTop = 40f;
        const float fieldBottom = 184f;
        var centerY = (fieldTop + fieldBottom) * 0.5f;
        var side = returnerPos.Y < centerY ? -1f : 1f;
        var offsetY = 6f * side;

        // Don't get too far ahead of the returner in X; keep near their depth.
        var myPos = _pos.Get(defenderId).Position;
        var x = MathHelper.Lerp(myPos.X, returnerPos.X, 0.65f);

        return new Vector2(x, returnerPos.Y + offsetY);
    }
}
