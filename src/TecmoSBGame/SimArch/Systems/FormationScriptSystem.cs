using System;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Executes FormationScript ops and converts them into Behavior targets.
///
/// Scope: pre-snap placement/motion subset.
/// - SetPosFromKick/Hike/Mid -> snap entity position to decoded anchor
/// - MoveAbsolute -> set Behavior target to absolute pos
/// - MoveRelative -> set Behavior target relative to current position
/// - Pause -> wait for seconds
/// - LoopBack -> set Ip=0
///
/// All other ops are treated as recognized no-ops (advance Ip).
/// </summary>
public sealed class FormationScriptSystem
{
    private readonly PlayState _play;

    // Anchor defaults (NES-ish coordinates)
    private static readonly Vector2 DefaultKickoffAnchor = new(56, 112);
    private static readonly Vector2 DefaultHikeAnchor = new(128, 112);
    private static readonly Vector2 DefaultMidAnchor = new(128, 112);

    public FormationScriptSystem(PlayState play)
    {
        _play = play ?? throw new ArgumentNullException(nameof(play));
    }

    public void Update(World world, float dtSeconds)
    {
        if (_play.Phase != PlayPhase.PreSnap)
            return;

        var q = new QueryDescription().WithAll<FormationScript, Position, Behavior>();

        world.Query(in q, (Entity e, ref FormationScript script, ref Position pos, ref Behavior beh) =>
        {
            // Wait
            if (script.WaitSeconds > 0f)
            {
                script.WaitSeconds = MathF.Max(0f, script.WaitSeconds - dtSeconds);
                return;
            }

            // Execute until we emit a movement/teleport or run out.
            var guard = 0;
            while (guard++ < 32)
            {
                if (script.Ops is null || script.Ops.Count == 0)
                    return;

                if (script.Ip < 0 || script.Ip >= script.Ops.Count)
                    script.Ip = 0;

                var op = script.Ops[script.Ip];
                script.Ip++;

                switch (op.Kind)
                {
                    case FormationScriptOpKind.Pause:
                        script.WaitSeconds = MathF.Max(0f, op.Seconds);
                        return;

                    case FormationScriptOpKind.LoopBack:
                        script.Ip = 0;
                        continue;

                    case FormationScriptOpKind.SetPosFromKick:
                        pos.Value = DecodeSetPos(op.Vec, DefaultKickoffAnchor);
                        beh.State = BehaviorState.Idle;
                        return;

                    case FormationScriptOpKind.SetPosFromHike:
                        pos.Value = DecodeSetPos(op.Vec, DefaultHikeAnchor);
                        beh.State = BehaviorState.Idle;
                        return;

                    case FormationScriptOpKind.SetPosFromMid:
                        pos.Value = DecodeSetPos(op.Vec, DefaultMidAnchor);
                        beh.State = BehaviorState.Idle;
                        return;

                    case FormationScriptOpKind.MoveAbsolute:
                        beh.State = BehaviorState.MovingToPosition;
                        beh.TargetEntityId = -1;
                        beh.TargetPosition = new Vector2(op.Vec.X, op.Vec.Y);
                        return;

                    case FormationScriptOpKind.MoveRelative:
                        beh.State = BehaviorState.MovingToPosition;
                        beh.TargetEntityId = -1;
                        beh.TargetPosition = pos.Value + op.Vec;
                        return;

                    default:
                        // recognized but ignored
                        continue;
                }
            }
        });
    }

    private static Vector2 DecodeSetPos(Vector2 bytes, Vector2 anchor)
    {
        // Vec is stored as two bytes in X/Y.
        var xByte = (byte)Math.Clamp((int)bytes.X, 0, 255);
        var yByte = (byte)Math.Clamp((int)bytes.Y, 0, 255);

        var x = anchor.X + unchecked((sbyte)xByte);
        var y = anchor.Y + (yByte - 0x80);
        return new Vector2(x, y);
    }
}
