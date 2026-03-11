using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
using TecmoSBGame.Field;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Executes engine-native play scripts attached to entities via <see cref="PlayScriptComponent"/>.
///
/// This is intentionally incremental: we start with snap-gating + movement intents.
/// Blocking/engagement commands will expand over time.
/// </summary>
public sealed class PlayScriptSystem : EntityUpdateSystem
{
    private const float FramesPerSecond = 60f;

    private readonly PlayState _play;
    private readonly MatchState _match;
    private readonly ControlState _control;

    private ComponentMapper<PlayScriptComponent> _script = null!;
    private ComponentMapper<BehaviorComponent> _behavior = null!;
    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<TeamComponent> _team = null!;
    private ComponentMapper<PlayerRoleComponent> _role = null!;
    private ComponentMapper<BallCarrierComponent> _carrier = null!;
    private ComponentMapper<BallComponent> _ballTag = null!;
    private ComponentMapper<BallStateComponent> _ballState = null!;
    private ComponentMapper<BallOwnerComponent> _ballOwner = null!;

    public bool DebugLog { get; set; }

    public PlayScriptSystem(PlayState playState, MatchState matchState, ControlState control)
        : base(Aspect.All(typeof(PositionComponent)))
    {
        _play = playState ?? throw new ArgumentNullException(nameof(playState));
        _match = matchState ?? throw new ArgumentNullException(nameof(matchState));
        _control = control ?? throw new ArgumentNullException(nameof(control));
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _script = mapperService.GetMapper<PlayScriptComponent>();
        _behavior = mapperService.GetMapper<BehaviorComponent>();
        _pos = mapperService.GetMapper<PositionComponent>();
        _team = mapperService.GetMapper<TeamComponent>();
        _role = mapperService.GetMapper<PlayerRoleComponent>();
        _carrier = mapperService.GetMapper<BallCarrierComponent>();
        _ballTag = mapperService.GetMapper<BallComponent>();
        _ballState = mapperService.GetMapper<BallStateComponent>();
        _ballOwner = mapperService.GetMapper<BallOwnerComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        foreach (var id in ActiveEntities)
        {
            if (!_script.Has(id))
                continue;

            var s = _script.Get(id);
            if (s.Ops.Count == 0)
                continue;

            if (s.WaitSeconds > 0f)
            {
                s.WaitSeconds = Math.Max(0f, s.WaitSeconds - dt);
                if (s.WaitSeconds <= 0f && s.PendingHandoffSlot is not null)
                {
                    TryExecuteHandoffToSlot(s.PendingHandoffSlot);
                    s.PendingHandoffSlot = null;
                }
                continue;
            }

            // Execute until we yield.
            for (var steps = 0; steps < 32; steps++)
            {
                if (s.Ip < 0 || s.Ip >= s.Ops.Count)
                    s.Ip = 0;

                var op = s.Ops[s.Ip];

                if (DebugLog && steps == 0)
                    Console.WriteLine($"[playscript] id={id} ip={s.Ip}/{s.Ops.Count} op={op.Kind} raw='{op.Raw}'");

                s.Ip++;

                switch (op.Kind)
                {
                    case PlayScriptOpKind.Nop:
                    case PlayScriptOpKind.Unknown:
                        continue;

                    case PlayScriptOpKind.WaitUntilSnap:
                        if (_play.Phase != PlayPhase.InPlay)
                        {
                            s.Ip = Math.Max(0, s.Ip - 1);
                            steps = 999;
                            break;
                        }
                        continue;

                    case PlayScriptOpKind.SetAnchor:
                        {
                            var kind = (op.S ?? string.Empty).Trim().ToLowerInvariant();
                            s.Anchor.Kind = kind switch
                            {
                                "los" or "line_of_scrimmage" => PlayAnchorKind.LineOfScrimmage,
                                "mid" or "midfield" => PlayAnchorKind.Midfield,
                                "ballcarrier" or "ball_carrier" => PlayAnchorKind.BallCarrier,
                                _ => PlayAnchorKind.None,
                            };
                            s.Anchor.Dx = op.A;
                            s.Anchor.Dy = op.B;
                            continue;
                        }

                    case PlayScriptOpKind.MoveBy:
                        {
                            var b = _behavior.Get(id);
                            b.State = BehaviorState.MovingToPosition;
                            b.TargetPosition = _pos.Get(id).Position + new Vector2(op.A, op.B);
                            steps = 999;
                            break;
                        }

                    case PlayScriptOpKind.MoveToAnchorOffset:
                        {
                            var b = _behavior.Get(id);
                            b.State = BehaviorState.MovingToPosition;
                            b.TargetPosition = ResolveAnchorPosition(id, s.Anchor) + new Vector2(op.A, op.B);
                            steps = 999;
                            break;
                        }

                    case PlayScriptOpKind.PullAndBlock:
                        {
                            // Phase 1: move to a point (we'll add engagement selection next).
                            var b = _behavior.Get(id);
                            b.State = BehaviorState.MovingToPosition;
                            b.TargetPosition = ResolveAnchorPosition(id, s.Anchor) + new Vector2(op.A, op.B);
                            steps = 999;
                            break;
                        }

                    case PlayScriptOpKind.PursueBallCarrier:
                        {
                            if (_play.Phase != PlayPhase.InPlay)
                                continue;

                            var target = FindBallCarrierEntityId();
                            if (target is null)
                                continue;

                            var b = _behavior.Get(id);
                            b.State = BehaviorState.TrackingPlayer;
                            b.TargetEntityId = target.Value;
                            steps = 999;
                            break;
                        }

                    case PlayScriptOpKind.RushQb:
                        {
                            if (_play.Phase != PlayPhase.InPlay)
                                continue;

                            var target = FindOffenseQbEntityId();
                            if (target is null)
                                continue;

                            var b = _behavior.Get(id);
                            b.State = BehaviorState.TrackingPlayer;
                            b.TargetEntityId = target.Value;
                            steps = 999;
                            break;
                        }

                    case PlayScriptOpKind.PassBlock:
                        {
                            // Placeholder: hold position while in-play.
                            var b = _behavior.Get(id);
                            b.State = BehaviorState.Idle;
                            steps = 999;
                            break;
                        }

                    case PlayScriptOpKind.HandoffTo:
                        {
                            if (_play.Phase != PlayPhase.InPlay)
                                continue;

                            var slot = (op.S ?? string.Empty).Trim();
                            if (slot.Length == 0)
                                continue;

                            var delayFrames = op.A;
                            if (delayFrames > 0f)
                            {
                                // Yield until delay expires, then execute handoff once.
                                s.PendingHandoffSlot = slot;
                                s.WaitSeconds = delayFrames / FramesPerSecond;
                                steps = 999;
                                break;
                            }

                            TryExecuteHandoffToSlot(slot);
                            steps = 999;
                            break;
                        }

                    case PlayScriptOpKind.Jump:
                    case PlayScriptOpKind.Loop:
                        // Label-based control flow will be added next; for now loop to start.
                        s.Ip = 0;
                        steps = 999;
                        break;

                    default:
                        continue;
                }
            }

            // Note: the actual movement is driven by BehaviorComponent and MovementSystem.
            // This system will expand to set BehaviorState/TargetPosition/TargetEntityId.
        }
    }

    private int? FindBallCarrierEntityId()
    {
        // Deterministic rule: first entity in id order with BallCarrierComponent.HasBall.
        // (We don't rely on ActiveEntities containing only scripted players.)
        int best = int.MaxValue;
        foreach (var id in ActiveEntities)
        {
            if (!_carrier.Has(id))
                continue;
            if (!_carrier.Get(id).HasBall)
                continue;
            if (id < best)
                best = id;
        }

        return best == int.MaxValue ? null : best;
    }

    private int? FindOffenseQbEntityId()
    {
        var offenseTeam = _match.PossessionTeam;

        int best = int.MaxValue;
        foreach (var id in ActiveEntities)
        {
            if (!_team.Has(id) || !_role.Has(id))
                continue;

            var t = _team.Get(id);
            if (!t.IsOffense || t.TeamIndex != offenseTeam)
                continue;

            var slot = (_role.Get(id).Slot ?? string.Empty).Trim();
            if (!string.Equals(slot, "QB", StringComparison.OrdinalIgnoreCase))
                continue;

            if (id < best)
                best = id;
        }

        return best == int.MaxValue ? null : best;
    }

    private void TryExecuteHandoffToSlot(string slot)
    {
        var offenseTeam = _match.PossessionTeam;

        int? targetId = null;
        foreach (var pid in ActiveEntities)
        {
            if (!_team.Has(pid) || !_role.Has(pid) || !_pos.Has(pid))
                continue;

            var t = _team.Get(pid);
            if (!t.IsOffense || t.TeamIndex != offenseTeam)
                continue;

            if (string.Equals(_role.Get(pid).Slot, slot, StringComparison.OrdinalIgnoreCase))
            {
                targetId = pid;
                break;
            }
        }

        if (targetId is null)
            return;

        // Update play model.
        _play.BallState = BallState.Held;
        _play.BallOwnerEntityId = targetId.Value;

        // Tecmo intent: once the handoff completes, control should deterministically switch to the new ball carrier.
        // We do this as a one-shot override consumed by PlayerControlSystem.
        _control.PendingForcedEntityId = targetId.Value;

        // Update carrier flags.
        foreach (var pid in ActiveEntities)
        {
            if (_carrier.Has(pid))
                _carrier.Get(pid).HasBall = pid == targetId.Value;
        }

        // Sync dedicated ball entity.
        foreach (var bid in ActiveEntities)
        {
            if (!_ballTag.Has(bid) || !_ballState.Has(bid) || !_ballOwner.Has(bid) || !_pos.Has(bid))
                continue;

            _ballState.Get(bid).State = BallState.Held;
            _ballOwner.Get(bid).OwnerEntityId = targetId.Value;
            _pos.Get(bid).Position = _pos.Get(targetId.Value).Position;
            break;
        }
    }

    private Vector2 ResolveAnchorPosition(int entityId, PlayAnchor anchor)
    {
        // Default: current position.
        var basePos = _pos.Get(entityId).Position;

        switch (anchor.Kind)
        {
            case PlayAnchorKind.LineOfScrimmage:
            {
                var abs = PlayState.ToAbsoluteYard(_match.BallSpot, _match.OffenseDirection);
                var x = FieldBounds.AbsoluteYardToX(abs);
                return new Vector2(x + anchor.Dx, 112 + anchor.Dy);
            }

            case PlayAnchorKind.Midfield:
                return new Vector2(FieldBounds.AbsoluteYardToX(50) + anchor.Dx, 112 + anchor.Dy);

            case PlayAnchorKind.BallCarrier:
            {
                foreach (var id in ActiveEntities)
                {
                    if (_carrier.Has(id) && _carrier.Get(id).HasBall && _pos.Has(id))
                        return _pos.Get(id).Position + new Vector2(anchor.Dx, anchor.Dy);
                }
                return basePos;
            }

            default:
                return basePos;
        }
    }
}
