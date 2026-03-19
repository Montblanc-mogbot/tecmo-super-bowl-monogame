using System;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Events;

namespace TecmoSBGame.SimArch.Systems;

/// <summary>
/// Consumes high-level action intents (from input/AI) and resolves them into:
/// - short-lived movement actions (Dive/Burst/Cut)
/// - gameplay requests via SimEventBus (Pass/Pitch/TackleAttempt/Snap)
///
/// Ported from: src/TecmoSBGame/ArchiveMge/Systems/ActionResolutionSystem.cs
/// </summary>
public sealed class ActionResolutionSystem
{
    public void Update(World world, float dtSeconds)
    {
        _ = dtSeconds;

        var q = new QueryDescription().WithAll<PlayerActionState, Team, Position>();
        world.Query(in q, (Entity e, ref PlayerActionState a, ref Team team, ref Position pos) =>
        {
            if (a.PendingCommand == PlayerActionCommand.None)
                return;

            var cmd = a.PendingCommand;
            a.PendingCommand = PlayerActionCommand.None;

            a.LastAppliedCommand = cmd;
            a.LastAppliedTargetEntityId = -1;

            switch (cmd)
            {
                case PlayerActionCommand.Dive:
                    TryApplyMovementAction(e, MovementActionState.Dive);
                    break;

                case PlayerActionCommand.SprintBurst:
                case PlayerActionCommand.Scramble:
                    TryApplyMovementAction(e, MovementActionState.Burst);
                    break;

                case PlayerActionCommand.JukeCut:
                    TryApplyMovementAction(e, MovementActionState.Cut);
                    break;

                case PlayerActionCommand.Tackle:
                    ResolveTackleAttempt(world, tacklerId: e.Id, tacklerTeam: team.TeamIndex);
                    break;

                case PlayerActionCommand.Snap:
                {
                    var ev = new SnapEvent(OffenseTeam: team.TeamIndex, DefenseTeam: team.TeamIndex == 0 ? 1 : 0);
                    SimEventBus.Send(ref ev);
                    break;
                }

                case PlayerActionCommand.Pass:
                {
                    var ev = new PassRequestedEvent(PasserId: e.Id, TargetId: a.PendingTargetEntityId >= 0 ? a.PendingTargetEntityId : null);
                    SimEventBus.Send(ref ev);
                    break;
                }

                case PlayerActionCommand.Pitch:
                {
                    var ev = new PitchRequestedEvent(BallCarrierId: e.Id);
                    SimEventBus.Send(ref ev);
                    break;
                }

                default:
                    break;
            }
        });
    }

    private static void TryApplyMovementAction(Entity e, MovementActionState desired)
    {
        if (!e.Has<MovementAction>())
            return;

        var m = e.Get<MovementAction>();
        if (m.CooldownTimer > 0f)
        {
            e.Set(m);
            return;
        }

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

        e.Set(m);
    }

    private static void ResolveTackleAttempt(World world, int tacklerId, int tacklerTeam)
    {
        // Find current ball carrier (best-effort: first HasBall on opposing team).
        int carrier = -1;
        Vector2 carrierPos = default;

        var q = new QueryDescription().WithAll<BallCarrier, Team, Position>();
        world.Query(in q, (Entity e, ref BallCarrier bc, ref Team t, ref Position p) =>
        {
            if (carrier != -1)
                return;
            if (!bc.HasBall)
                return;
            if (t.TeamIndex == tacklerTeam)
                return;

            carrier = e.Id;
            carrierPos = p.Value;
        });

        if (carrier == -1)
            return;

        var ev = new TackleAttemptEvent(tacklerId, carrier, carrierPos);
        SimEventBus.Send(ref ev);
    }
}
