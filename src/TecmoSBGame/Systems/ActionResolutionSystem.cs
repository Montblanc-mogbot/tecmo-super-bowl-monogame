using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Consumes high-level action intents (from input/AI) and resolves them into:
/// - short-lived movement actions (Dive/Burst/Cut)
/// - gameplay requests via <see cref="GameEvents"/> (Pass/Pitch/TackleAttempt)
///
/// This is deliberately minimal: it wires intent -> state transitions without ball physics/animations yet.
/// </summary>
public sealed class ActionResolutionSystem : EntityUpdateSystem
{
    private readonly GameEvents? _events;
    private readonly MatchState? _match;
    private readonly PlayState? _play;

    private ComponentMapper<PlayerActionStateComponent> _actionMapper;
    private ComponentMapper<MovementActionComponent> _moveActionMapper;
    private ComponentMapper<TeamComponent> _teamMapper;
    private ComponentMapper<PositionComponent> _posMapper;
    private ComponentMapper<BallCarrierComponent> _ballMapper;

    public ActionResolutionSystem(GameEvents? events = null, MatchState? matchState = null, PlayState? playState = null)
        : base(Aspect.All(typeof(PlayerActionStateComponent), typeof(TeamComponent), typeof(PositionComponent)))
    {
        _events = events;
        _match = matchState;
        _play = playState;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _actionMapper = mapperService.GetMapper<PlayerActionStateComponent>();
        _moveActionMapper = mapperService.GetMapper<MovementActionComponent>();
        _teamMapper = mapperService.GetMapper<TeamComponent>();
        _posMapper = mapperService.GetMapper<PositionComponent>();
        _ballMapper = mapperService.GetMapper<BallCarrierComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        foreach (var entityId in ActiveEntities)
        {
            var a = _actionMapper.Get(entityId);
            if (a.PendingCommand == PlayerActionCommand.None)
                continue;

            var cmd = a.PendingCommand;
            a.PendingCommand = PlayerActionCommand.None;

            a.LastAppliedCommand = cmd;
            a.LastAppliedTargetEntityId = null;

            switch (cmd)
            {
                case PlayerActionCommand.Dive:
                    TryApplyMovementAction(entityId, MovementActionState.Dive);
                    break;

                case PlayerActionCommand.SprintBurst:
                case PlayerActionCommand.Scramble:
                    // For now, scramble is just a burst intent (while we don't have QB pass states).
                    TryApplyMovementAction(entityId, MovementActionState.Burst);
                    break;

                case PlayerActionCommand.JukeCut:
                    TryApplyMovementAction(entityId, MovementActionState.Cut);
                    break;

                case PlayerActionCommand.Tackle:
                    ResolveTackleAttempt(entityId);
                    break;

                case PlayerActionCommand.Snap:
                    ResolveSnap(entityId);
                    break;

                case PlayerActionCommand.Pass:
                    ResolvePassRequested(entityId);
                    break;

                case PlayerActionCommand.Pitch:
                    ResolvePitchRequested(entityId);
                    break;

                default:
                    break;
            }
        }
    }

    private void TryApplyMovementAction(int entityId, MovementActionState desired)
    {
        if (!_moveActionMapper.Has(entityId))
            return;

        var m = _moveActionMapper.Get(entityId);
        if (m.CooldownTimer > 0f)
            return;

        switch (desired)
        {
            case MovementActionState.Burst:
                m.State = MovementActionState.Burst;
                m.StateTimer = m.BurstDurationSeconds;
                m.CooldownTimer = m.BurstCooldownSeconds;
                break;

            case MovementActionState.Dive:
                m.State = MovementActionState.Dive;
                m.StateTimer = m.DiveDurationSeconds;
                m.CooldownTimer = m.DiveCooldownSeconds;
                break;

            case MovementActionState.Cut:
                m.State = MovementActionState.Cut;
                m.StateTimer = m.CutDurationSeconds;
                m.CooldownTimer = m.CutCooldownSeconds;
                break;
        }
    }

    private void ResolveTackleAttempt(int tacklerId)
    {
        if (_events is null)
            return;

        var tacklerTeam = _teamMapper.Get(tacklerId);
        if (tacklerTeam.IsOffense)
            return; // only meaningful on defense

        // Find current ball carrier (best-effort: first HasBall we see on the opposing team).
        int? carrier = null;
        foreach (var id in ActiveEntities)
        {
            if (id == tacklerId)
                continue;

            if (!_ballMapper.Has(id) || !_ballMapper.Get(id).HasBall)
                continue;

            var team = _teamMapper.Get(id);
            if (team.TeamIndex == tacklerTeam.TeamIndex)
                continue;

            carrier = id;
            break;
        }

        if (carrier is null)
            return;

        var pos = _posMapper.Get(carrier.Value).Position;
        _events.Publish(new TackleAttemptEvent(tacklerId, carrier.Value, pos));

        // Record for headless snapshots.
        var a = _actionMapper.Get(tacklerId);
        a.LastAppliedTargetEntityId = carrier.Value;
    }

    private void ResolveSnap(int qbEntityId)
    {
        if (_events is null)
            return;

        // Only meaningful when the play is pre-snap.
        if (_play is not null && _play.Phase != PlayPhase.PreSnap)
            return;

        var offenseTeam = _match?.PossessionTeam ?? _teamMapper.Get(qbEntityId).TeamIndex;
        var defenseTeam = offenseTeam == 0 ? 1 : 0;

        _events.Publish(new SnapEvent(OffenseTeam: offenseTeam, DefenseTeam: defenseTeam));
    }

    private void ResolvePassRequested(int qbEntityId)
    {
        var a = _actionMapper.Get(qbEntityId);
        var targetId = a.PendingTargetEntityId;
        a.PendingTargetEntityId = null;

        if (_events is not null)
            _events.Publish(new PassRequestedEvent(PasserId: qbEntityId, TargetId: targetId, PassType: PassType.Bullet));

        if (_play is null)
            return;

        // Minimal placeholder: when a pass is requested, ball is considered "in air".
        // Ownership/trajectory will be handled by later dedicated systems.
        if (_ballMapper.Has(qbEntityId))
            _ballMapper.Get(qbEntityId).HasBall = false;

        _play.BallState = BallState.InAir;
        _play.BallOwnerEntityId = null;
    }

    private void ResolvePitchRequested(int ballCarrierId)
    {
        if (_events is not null)
            _events.Publish(new PitchRequestedEvent(ballCarrierId));

        // Placeholder: no ball transfer yet.
        // In the future this will select a target receiver and put the ball "in air" with a short arc.
    }
}
