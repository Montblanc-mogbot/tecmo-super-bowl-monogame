using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Punt coverage logic (gunners downfield) — scaffold.
///
/// Behavior:
/// - While punt is in flight: sprint to lane landmarks.
/// - Once returner has ball: break on returner (gunner bias).
///
/// This system is safe to include even if punts are not fully wired yet.
/// </summary>
public sealed class PuntCoverageSystem : EntityUpdateSystem
{
    private readonly PlayState _play;

    private ComponentMapper<PuntCoverageComponent> _cov = null!;
    private ComponentMapper<BehaviorComponent> _behavior = null!;
    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<TeamComponent> _team = null!;
    private ComponentMapper<BallCarrierComponent> _carrier = null!;

    public PuntCoverageSystem(PlayState play)
        : base(Aspect.All(typeof(PuntCoverageComponent), typeof(BehaviorComponent), typeof(PositionComponent), typeof(TeamComponent)))
    {
        _play = play ?? throw new ArgumentNullException(nameof(play));
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _cov = mapperService.GetMapper<PuntCoverageComponent>();
        _behavior = mapperService.GetMapper<BehaviorComponent>();
        _pos = mapperService.GetMapper<PositionComponent>();
        _team = mapperService.GetMapper<TeamComponent>();
        _carrier = mapperService.GetMapper<BallCarrierComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (_play.Phase != PlayPhase.InPlay || _play.IsOver)
            return;

        foreach (var id in ActiveEntities)
        {
            var t = _team.Get(id);
            if (t.IsOffense)
                continue; // coverage team is defense

            var c = _cov.Get(id);
            var b = _behavior.Get(id);

            var returnerId = c.ReturnerEntityId;
            var returnerHasBall = returnerId != -1 && _carrier.Has(returnerId) && _carrier.Get(returnerId).HasBall;

            if (returnerHasBall)
                c.BreakOnReturner = true;

            Vector2 target;
            if (c.BreakOnReturner && returnerId != -1 && _pos.Has(returnerId))
            {
                // Gunner bias: aim slightly ahead in X towards the returner.
                var rp = _pos.Get(returnerId).Position;
                var my = _pos.Get(id).Position;
                var aheadX = MathHelper.Lerp(my.X, rp.X, c.IsGunner ? 0.85f : 0.65f);
                target = new Vector2(aheadX, rp.Y);
            }
            else
            {
                target = c.LaneLandmark;
            }

            b.State = BehaviorState.MovingToPosition;
            b.TargetPosition = target;
        }
    }
}
