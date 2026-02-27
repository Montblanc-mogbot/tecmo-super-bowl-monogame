using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Events;
using TecmoSBGame.Field;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Deterministic bridge from a completed play (PostPlay) back to the next pre-snap state.
///
/// Responsibilities:
/// - Observe <see cref="PlayEndedEvent"/> (without consuming it).
/// - Reset <see cref="PlayState"/> for the next play using <see cref="MatchState.BallSpot"/>.
/// - Clear per-play transient component state (inputs, action timers, interrupts, etc.).
/// - Ensure the dedicated ball entity is in a consistent pre-snap state (dead, no owner).
/// - Publish <see cref="ResetToPreSnapEvent"/> so <see cref="LoopMachineSystem"/> can raise "next_play".
///
/// Notes:
/// - Kickoff-after-score is handled by <see cref="KickoffAfterScoreSystem"/>; we avoid overriding scoring transitions.
/// </summary>
public sealed class NextPlayResetSystem : EntityUpdateSystem
{
    private readonly GameEvents? _events;
    private readonly LoopState? _loop;
    private readonly MatchState _match;
    private readonly PlayState _play;
    private readonly bool _log;

    private ComponentMapper<VelocityComponent> _vel = null!;
    private ComponentMapper<MovementInputComponent> _moveInput = null!;
    private ComponentMapper<MovementActionComponent> _moveAction = null!;
    private ComponentMapper<PlayerActionStateComponent> _playerAction = null!;
    private ComponentMapper<SpeedModifierComponent> _speedMod = null!;
    private ComponentMapper<EngagementComponent> _engagement = null!;
    private ComponentMapper<BehaviorStackComponent> _stack = null!;
    private ComponentMapper<BehaviorComponent> _behavior = null!;

    private ComponentMapper<BallComponent> _ballTag = null!;
    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<BallStateComponent> _ballState = null!;
    private ComponentMapper<BallOwnerComponent> _ballOwner = null!;

    private int _lastProcessedPlayId = -1;

    public NextPlayResetSystem(GameEvents? events, MatchState matchState, PlayState playState, LoopState? loopState = null, bool log = true)
        : base(Aspect.All(typeof(PositionComponent)))
    {
        _events = events;
        _match = matchState ?? throw new ArgumentNullException(nameof(matchState));
        _play = playState ?? throw new ArgumentNullException(nameof(playState));
        _loop = loopState;
        _log = log;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _vel = mapperService.GetMapper<VelocityComponent>();
        _moveInput = mapperService.GetMapper<MovementInputComponent>();
        _moveAction = mapperService.GetMapper<MovementActionComponent>();
        _playerAction = mapperService.GetMapper<PlayerActionStateComponent>();
        _speedMod = mapperService.GetMapper<SpeedModifierComponent>();
        _engagement = mapperService.GetMapper<EngagementComponent>();
        _stack = mapperService.GetMapper<BehaviorStackComponent>();
        _behavior = mapperService.GetMapper<BehaviorComponent>();

        _ballTag = mapperService.GetMapper<BallComponent>();
        _pos = mapperService.GetMapper<PositionComponent>();
        _ballState = mapperService.GetMapper<BallStateComponent>();
        _ballOwner = mapperService.GetMapper<BallOwnerComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        if (_events is null)
            return;

        var ended = _events.Read<PlayEndedEvent>();
        if (ended.Count <= 0)
            return;

        // If multiple end events somehow arrive in a tick, process the latest playId deterministically.
        var e = ended[^1];
        if (e.PlayId == _lastProcessedPlayId)
            return;

        // Scoring transitions are handled by KickoffAfterScoreSystem.
        if (e.Touchdown || e.Safety)
        {
            _lastProcessedPlayId = e.PlayId;
            return;
        }

        // Only reset once the play is actually over.
        if (!_play.IsOver)
        {
            _lastProcessedPlayId = e.PlayId;
            return;
        }

        ResetPlayModelForNextSnap();
        ClearTransientPerPlayComponents();
        ResetBallEntityForPreSnap();

        // Signal the loop driver to transition from dead_ball -> pre_snap.
        _events.Publish(new ResetToPreSnapEvent(e.PlayId));

        if (_log)
            Console.WriteLine($"[next-play] reset from playId={e.PlayId} -> playId={_play.PlayId} ballSpot={_match.BallSpot} loop={_loop?.OnFieldStateId}");

        _lastProcessedPlayId = e.PlayId;
    }

    private void ResetPlayModelForNextSnap()
    {
        var startAbs = PlayState.ToAbsoluteYard(_match.BallSpot, _match.OffenseDirection);

        // Convention: MatchState.PlayNumber was advanced by DownDistanceSystem.
        var nextPlayId = _match.PlayNumber + 1;

        _play.ResetForNewPlay(playId: nextPlayId, startAbsoluteYard: startAbs);
        _play.Phase = PlayPhase.PreSnap;
        _play.BallState = BallState.Dead;
        _play.WhistleReason = WhistleReason.None;
    }

    private void ClearTransientPerPlayComponents()
    {
        foreach (var entityId in ActiveEntities)
        {
            if (_moveInput.Has(entityId))
                _moveInput.Get(entityId).Direction = Vector2.Zero;

            if (_moveAction.Has(entityId))
            {
                var a = _moveAction.Get(entityId);
                a.State = MovementActionState.None;
                a.StateTimer = 0f;
                a.CooldownTimer = 0f;
            }

            if (_playerAction.Has(entityId))
            {
                var a = _playerAction.Get(entityId);
                a.PendingCommand = PlayerActionCommand.None;
                a.PendingTargetEntityId = null;
                a.LastAppliedCommand = PlayerActionCommand.None;
                a.LastAppliedTargetEntityId = null;

                a.PrevActionDown = false;
                a.PrevPitchDown = false;
                a.PrevSprintDown = false;
                a.PrevJukeDown = false;
            }

            if (_speedMod.Has(entityId))
            {
                var m = _speedMod.Get(entityId);
                m.MaxSpeedMultiplier = 1.0f;
                m.TimerSeconds = 0.0f;
            }

            if (_engagement.Has(entityId))
            {
                var e = _engagement.Get(entityId);
                e.PartnerEntityId = -1;
                e.CooldownSeconds = 0f;
            }

            if (_stack.Has(entityId))
            {
                _stack.Get(entityId).Stack.Clear();
            }

            if (_behavior.Has(entityId))
            {
                var b = _behavior.Get(entityId);
                if (b.State is BehaviorState.Engaged or BehaviorState.Tackling or BehaviorState.Grappling)
                {
                    b.State = BehaviorState.Idle;
                    b.StateTimer = 0f;
                    b.TargetEntityId = 0;
                    b.TargetPosition = Vector2.Zero;
                }
            }

            if (_vel.Has(entityId))
            {
                // Avoid carry-over drift into the next pre-snap alignment.
                _vel.Get(entityId).Velocity = Vector2.Zero;
            }
        }
    }

    private void ResetBallEntityForPreSnap()
    {
        // Keep the dedicated ball entity consistent immediately; PreSnapBallPlacementSystem will keep it snapped.
        var losAbs = PlayState.ToAbsoluteYard(_match.BallSpot, _match.OffenseDirection);
        var losX = FieldBounds.AbsoluteYardToX(losAbs);

        foreach (var ballId in ActiveEntities)
        {
            if (!_ballTag.Has(ballId))
                continue;

            if (_pos.Has(ballId))
            {
                var p = _pos.Get(ballId);
                p.Position = new Vector2(losX, p.Position.Y);
            }

            if (_ballState.Has(ballId))
                _ballState.Get(ballId).State = BallState.Dead;

            if (_ballOwner.Has(ballId))
                _ballOwner.Get(ballId).OwnerEntityId = null;
        }

        _play.BallOwnerEntityId = null;
    }
}
