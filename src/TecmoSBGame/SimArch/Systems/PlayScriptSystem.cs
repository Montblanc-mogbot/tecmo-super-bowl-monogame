using System;
using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.PlayScripts;

namespace TecmoSBGame.SimArch.Systems;

// Ported from: src/TecmoSBGame/ArchiveMge/Systems/PlayScriptSystem.cs

/// <summary>
/// Executes compiled PlayScriptOps from the <see cref="PlayScriptRegistry"/>.
///
/// Current supported ops:
/// - wait (seconds)
/// - move_by (one-shot)
/// - handoff_to (delayed)
/// - loop
/// </summary>
public sealed class PlayScriptSystem
{
    public void Update(
        World world,
        float dtSeconds,
        int ballEntityId,
        IReadOnlyList<int> offenseEntityIds,
        IReadOnlyList<int> defenseEntityIds,
        PlayScriptRegistry registry,
        ref Control control)
    {
        // We can't capture a ref parameter inside the query lambda, so we hop through a local.
        var controlLocal = control;

        // Build offense slot lookup once (used for handoff_to).
        var offenseSlots = BuildOffenseSlotLookup(world, offenseEntityIds);

        var query = new QueryDescription().WithAll<PlayScript>();
        world.Query(in query, (Entity e, ref PlayScript s) =>
        {
            var ops = registry.Get(s.ScriptId);

            // Handle waiting.
            if (s.WaitSeconds > 0f)
            {
                s.WaitSeconds = MathF.Max(0f, s.WaitSeconds - dtSeconds);
                if (s.WaitSeconds > 0f)
                    return;
            }

            // Run at most a few ops per tick to avoid infinite loops.
            for (var steps = 0; steps < 4; steps++)
            {
                if (s.Ip < 0 || s.Ip >= ops.Length)
                {
                    s.Ip = 0;
                    return;
                }

                var op = ops[s.Ip];
                s.Ip++;

                switch (op.Kind)
                {
                    case PlayScriptOpKind.Loop:
                        s.Ip = 0;
                        return;

                    case PlayScriptOpKind.WaitSeconds:
                        s.WaitSeconds = MathF.Max(0f, op.Seconds);
                        return;

                    case PlayScriptOpKind.MoveBy:
                        ApplyMoveBy(world, e.Id, op.Delta);
                        break;

                    case PlayScriptOpKind.HandoffToSlotAfterSeconds:
                        // Convert to the old handoff bookkeeping so we don't store managed slot strings in components.
                        if (op.Slot is not null && offenseSlots.TryGetValue(op.Slot, out var toEntityId))
                        {
                            s.WaitSeconds = MathF.Max(0f, op.Seconds);
                            s.PendingHandoffToEntityId = toEntityId;

                            // When the wait completes, we perform the handoff via the old path.
                            // We rely on the next tick after WaitSeconds reaches 0.
                            return;
                        }
                        break;

                    case PlayScriptOpKind.SetMs:
                    case PlayScriptOpKind.BoostRs:
                        // TODO: ratings/tuning influence; scaffold is already present.
                        break;

                    default:
                        return;
                }
            }
        });

        // After advancing scripts, execute any now-ready handoffs.
        var handoffQuery = new QueryDescription().WithAll<PlayScript>();
        world.Query(in handoffQuery, (Entity e, ref PlayScript s) =>
        {
            if (s.PendingHandoffToEntityId < 0)
                return;
            if (s.WaitSeconds > 0f)
                return;

            ExecuteHandoff(world, ballEntityId, fromEntityId: e.Id, toEntityId: s.PendingHandoffToEntityId, ref controlLocal);
            s.PendingHandoffToEntityId = -1;
        });

        control = controlLocal;
    }

    private static Dictionary<string, int> BuildOffenseSlotLookup(World world, IReadOnlyList<int> offenseEntityIds)
    {
        var allow = new HashSet<int>(offenseEntityIds);
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var q = new QueryDescription().WithAll<Role>();
        world.Query(in q, (Entity e, ref Role r) =>
        {
            if (!allow.Contains(e.Id))
                return;

            var slot = r.Id switch
            {
                RoleId.QB => "QB",
                RoleId.HB => "HB",
                RoleId.FB => "FB",
                RoleId.WR1 => "WR1",
                RoleId.WR2 => "WR2",
                RoleId.TE => "TE",
                RoleId.OC => "OC",
                RoleId.LG => "LG",
                RoleId.RG => "RG",
                RoleId.LT => "LT",
                RoleId.RT => "RT",
                _ => null,
            };

            if (slot is null)
                return;

            if (!map.ContainsKey(slot))
                map[slot] = e.Id;
        });

        return map;
    }

    private static void ApplyMoveBy(World world, int entityId, Vector2 delta)
    {
        var q = new QueryDescription().WithAll<Position, Behavior>();
        world.Query(in q, (Entity e, ref Position pos, ref Behavior beh) =>
        {
            if (e.Id != entityId)
                return;

            beh.State = BehaviorState.MovingToPosition;
            beh.TargetEntityId = -1;
            beh.TargetPosition = pos.Value + delta;
            beh.StateTimer = 0f;
        });
    }

    private static void ExecuteHandoff(World world, int ballEntityId, int fromEntityId, int toEntityId, ref Control control)
    {
        var ballQuery = new QueryDescription().WithAll<Ball>();
        var didUpdateBall = false;

        world.Query(in ballQuery, (Entity e, ref Ball b) =>
        {
            if (e.Id != ballEntityId)
                return;

            b.State = BallState.Held;
            b.OwnerEntityId = toEntityId;
            b.FlightKind = BallFlightKind.None;
            b.ElapsedSeconds = 0f;
            b.DurationSeconds = 0f;
            b.Height = 0f;
            b.IsComplete = true;

            didUpdateBall = true;
        });

        if (!didUpdateBall)
            return;

        // Force control to the new carrier.
        control.PendingForcedEntityId = toEntityId;
        control.ControlledEntityId = toEntityId;

        Console.WriteLine($"[sim-arch] handoff from={fromEntityId} to={toEntityId}");

        // Give HB a deterministic run target so the play is visibly alive even before input wiring.
        var hbQuery = new QueryDescription().WithAll<Position, Behavior>();
        world.Query(in hbQuery, (Entity e, ref Position pos, ref Behavior beh) =>
        {
            if (e.Id != toEntityId)
                return;

            if (beh.State == BehaviorState.Idle)
            {
                beh.State = BehaviorState.MovingToPosition;
                beh.TargetPosition = pos.Value + new Vector2(64, -8);
            }
        });

        // Switch defenders to pursue ballcarrier.
        var query = new QueryDescription().WithAll<Team, Behavior>();
        world.Query(in query, (Entity e, ref Team t, ref Behavior beh) =>
        {
            if (t.IsOffense)
                return;

            beh.State = BehaviorState.TrackingEntity;
            beh.TargetEntityId = toEntityId;
        });
    }
}
