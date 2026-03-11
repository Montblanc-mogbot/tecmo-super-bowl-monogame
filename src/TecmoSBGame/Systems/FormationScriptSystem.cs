using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;

namespace TecmoSBGame.Systems;

/// <summary>
/// Advances FormationScriptComponent instructions and drives BehaviorComponent.
///
/// This mirrors Tecmo's script-driven on-field behavior model:
/// data (YAML commands) -> per-entity instruction stream -> movement/blocking directives.
/// </summary>
public sealed class FormationScriptSystem : EntityUpdateSystem
{
    private readonly State.PlayState? _play;

    private ComponentMapper<FormationScriptComponent> _script = null!;
    private ComponentMapper<BehaviorComponent> _behavior = null!;
    private ComponentMapper<PositionComponent> _pos = null!;
    private ComponentMapper<PlayerControlComponent> _control = null!;

    public bool DebugLog { get; set; }

    public FormationScriptSystem(State.PlayState? playState = null) : base(Aspect.All(
        typeof(FormationScriptComponent),
        typeof(BehaviorComponent),
        typeof(PositionComponent)))
    {
        _play = playState;
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _script = mapperService.GetMapper<FormationScriptComponent>();
        _behavior = mapperService.GetMapper<BehaviorComponent>();
        _pos = mapperService.GetMapper<PositionComponent>();
        _control = mapperService.GetMapper<PlayerControlComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        foreach (var id in ActiveEntities)
        {
            var s = _script.Get(id);
            if (s.Ops.Count == 0)
                continue;

            // If player is controlling this entity, don't fight MovementInput.
            s.SuspendMovement = _control.Has(id) && _control.Get(id).IsControlled;

            if (s.WaitSeconds > 0f)
            {
                s.WaitSeconds = Math.Max(0f, s.WaitSeconds - dt);
                continue;
            }

            // Execute until we hit a yielding op (movement or pause) or run out of ops.
            // Hard cap to avoid infinite loops with bad scripts.
            for (var steps = 0; steps < 32; steps++)
            {
                if (s.Ip < 0 || s.Ip >= s.Ops.Count)
                    s.Ip = 0;

                var op = s.Ops[s.Ip];

                if (DebugLog && steps == 0)
                    Console.WriteLine($"[script] id={id} ip={s.Ip}/{s.Ops.Count} op={op.Kind} raw='{op.Raw}'");

                s.Ip++;

                switch (op.Kind)
                {
                    case FormationScriptOpKind.Nop:
                    case FormationScriptOpKind.Unknown:
                    case FormationScriptOpKind.SetToBlock:
                    case FormationScriptOpKind.PassBlock:
                    case FormationScriptOpKind.TakeControl:
                    case FormationScriptOpKind.ComputerTakeControl:
                        // Not yet modeled in ECS. Keep advancing.
                        continue;

                    case FormationScriptOpKind.WaitForSnap:
                        // Yield until the play transitions into live play.
                        // Keep IP pinned on this op to preserve deterministic behavior.
                        if (_play is null || _play.Phase != State.PlayPhase.InPlay)
                        {
                            s.Ip = Math.Max(0, s.Ip - 1);
                            steps = 999;
                            break;
                        }
                        continue;

                    case FormationScriptOpKind.Pause:
                        s.WaitSeconds = Math.Max(0f, op.Seconds);
                        steps = 999; // yield
                        break;

                    case FormationScriptOpKind.LoopBack:
                        // For now, loop to script start.
                        s.Ip = 0;
                        // yield so we don't spin all tick
                        steps = 999;
                        break;

                    case FormationScriptOpKind.MoveAbsolute:
                        if (!s.SuspendMovement)
                        {
                            var b = _behavior.Get(id);
                            b.State = BehaviorState.MovingToPosition;
                            b.TargetPosition = op.Vec;
                        }
                        steps = 999; // yield
                        break;

                    case FormationScriptOpKind.MoveRelative:
                        if (!s.SuspendMovement)
                        {
                            var b = _behavior.Get(id);
                            b.State = BehaviorState.MovingToPosition;
                            b.TargetPosition = _pos.Get(id).Position + op.Vec;
                        }
                        steps = 999; // yield
                        break;

                    default:
                        continue;
                }
            }
        }
    }
}
