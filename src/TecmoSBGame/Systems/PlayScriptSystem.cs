using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSBGame.Components;
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
    private readonly PlayState _play;

    private ComponentMapper<PlayScriptComponent> _script = null!;
    private ComponentMapper<BehaviorComponent> _behavior = null!;

    public bool DebugLog { get; set; }

    public PlayScriptSystem(PlayState playState)
        : base(Aspect.All(typeof(PlayScriptComponent), typeof(BehaviorComponent)))
    {
        _play = playState ?? throw new ArgumentNullException(nameof(playState));
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _script = mapperService.GetMapper<PlayScriptComponent>();
        _behavior = mapperService.GetMapper<BehaviorComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        foreach (var id in ActiveEntities)
        {
            var s = _script.Get(id);
            if (s.Ops.Count == 0)
                continue;

            if (s.WaitSeconds > 0f)
            {
                s.WaitSeconds = Math.Max(0f, s.WaitSeconds - dt);
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

                    case PlayScriptOpKind.MoveBy:
                        // For now treat MoveBy as a nudge: we don't yet model per-play anchors here.
                        // Higher-level compiler will translate this into Behavior targets.
                        steps = 999;
                        break;

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
}
