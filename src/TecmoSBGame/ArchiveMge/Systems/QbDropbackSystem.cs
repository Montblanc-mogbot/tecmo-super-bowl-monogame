using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Fixed-step, frame-based QB dropback/rollout.
///
/// This runs after <see cref="MovementSystem"/> and directly adjusts QB position to preserve
/// step timing and deterministic distance (matching the original game's feel more than the
/// current analog movement model).
/// </summary>
public sealed class QbDropbackSystem : EntityUpdateSystem
{
    private readonly GameEvents? _events;
    private readonly MatchState? _match;
    private readonly PlayState? _play;

    private ComponentMapper<QbBrainComponent> _brain = null!;
    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<VelocityComponent> _vel = null!;
    private ComponentMapper<PlayerRoleComponent> _role = null!;
    private ComponentMapper<BallCarrierComponent> _carrier = null!;
    private ComponentMapper<TeamComponent> _team = null!;

    // Approx Tecmo distances per step (NES pixels). Kept small: step timing matters more than yardage.
    private const float STEP_BACK_PIXELS = 2.0f;
    private const float ROLLOUT_LATERAL_PIXELS = 1.5f;

    public QbDropbackSystem(GameEvents? events = null, MatchState? matchState = null, PlayState? playState = null)
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
        _team = mapperService.GetMapper<TeamComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        // Reset brains on snap so dropback starts immediately and deterministically.
        if (_events is not null)
        {
            var snaps = _events.Read<SnapEvent>();
            if (snaps.Count > 0)
            {
                foreach (var id in ActiveEntities)
                {
                    if (!_role.Has(id) || _role.Get(id).Role != PlayerRole.QB)
                        continue;

                    var b = _brain.Get(id);
                    b.ResetForSnap(_pos.Get(id).Position);
                }
            }
        }

        // Only run during in-play.
        if (_play is not null && _play.Phase != PlayPhase.InPlay)
            return;

        foreach (var id in ActiveEntities)
        {
            if (!_role.Has(id) || _role.Get(id).Role != PlayerRole.QB)
                continue;

            if (_carrier.Has(id) && !_carrier.Get(id).HasBall)
                continue;

            var brain = _brain.Get(id);
            if (brain.DropbackComplete || brain.ScrambleMode)
                continue;

            var totalSteps = brain.GetTotalStepCount();
            if (totalSteps <= 0)
            {
                brain.DropbackComplete = true;
                continue;
            }

            // Tecmo timing: 5 frames per step.
            brain.DropbackFrame++;
            var stepIndex = Math.Clamp((brain.DropbackFrame - 1) / QbBrainComponent.STEP_FRAMES, 0, totalSteps);
            brain.DropbackStep = stepIndex;

            if (brain.DropbackFrame % QbBrainComponent.STEP_FRAMES != 0)
                continue;

            // Apply one "step" of movement.
            var p = _pos.Get(id);

            // Offense direction: dropback is away from LOS.
            // LeftToRight: offense moves +X, so dropback is -X.
            // RightToLeft: offense moves -X, so dropback is +X.
            var dir = _match?.OffenseDirection ?? OffenseDirection.LeftToRight;
            var backSign = dir == OffenseDirection.LeftToRight ? -1f : 1f;

            var delta = new Vector2(STEP_BACK_PIXELS * backSign, 0f);

            if (brain.Dropback is DropbackType.RolloutLeft or DropbackType.RolloutRight)
            {
                var lateral = brain.Dropback == DropbackType.RolloutLeft ? -1f : 1f;
                delta += new Vector2(0f, ROLLOUT_LATERAL_PIXELS * lateral);
            }

            p.Position += delta;

            // Freeze analog movement during scripted dropback.
            if (_vel.Has(id))
                _vel.Get(id).Velocity = Vector2.Zero;

            // Completed?
            if (brain.DropbackStep >= totalSteps - 1)
                brain.DropbackComplete = true;
        }
    }
}
