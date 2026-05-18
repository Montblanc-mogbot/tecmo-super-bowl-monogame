using System;
using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Events;
using TecmoSBGame.SimArch.State;

namespace TecmoSBGame.SimArch.Systems;

// Ported from: src/TecmoSBGame/ArchiveMge/Systems/TackleResolutionSystem.cs
// Ported from: src/TecmoSBGame/ArchiveMge/Systems/WhistleOnTackleSystem.cs

public enum TackleOutcome
{
    Downed = 0,
    BrokenTackle = 1,
    Stumble = 2,
    FallForward = 3,
}

/// <summary>
/// Ratings-based tackle resolution scaffold for SimArch.
///
/// Consumes <see cref="TackleContactEvent"/> and deterministically resolves an outcome.
/// If downed/fall-forward: whistles by setting the ball Dead.
///
/// Notes:
/// - This is intentionally not Tecmo-perfect yet; it's a deterministic scaffold.
/// - Uses <see cref="Ratings"/> (MS/HP/RS). Defaults to 50 if missing.
/// </summary>
public sealed class TackleResolutionSystem
{
    private const float BaseForcedFumbleChance = 0.14f;
    private const float TacklerHpFumbleWeight = 0.0035f;
    private const float CarrierRsFumbleWeight = 0.0020f;
    private const float CarrierHpFumbleWeight = 0.0010f;
    private const float FumbleImpulsePerTick = 1.8f;

    // (tackler, carrier) -> remaining cooldown seconds
    private readonly Dictionary<ulong, float> _cooldowns = new(capacity: 64);

    // (tackler, carrier) -> roll index
    private readonly Dictionary<ulong, int> _rollIndex = new(capacity: 64);

    public float ContactCooldownSeconds = 0.25f;

    /// <summary>
    /// Set to true for the tick when a tackle downed the carrier (or fall-forward).
    /// Caller is responsible for clearing each tick.
    /// </summary>
    public bool WhistledThisTick { get; private set; }

    public bool FumbleTriggeredThisTick { get; private set; }

    public void Update(World world, float dtSeconds, int ballEntityId, ref Control control, PlayState? play = null)
    {
        WhistledThisTick = false;
        FumbleTriggeredThisTick = false;

        if (dtSeconds > 0f)
            TickCooldowns(dtSeconds);

        foreach (var evt in SimEventBus.Drain<TackleContactEvent>())
        {
            var tacklerId = evt.DefenderId;
            var carrierId = evt.BallCarrierId;
            if (tacklerId < 0 || carrierId < 0 || tacklerId == carrierId)
                continue;

            var key = PairKey(tacklerId, carrierId);
            if (_cooldowns.TryGetValue(key, out var cd) && cd > 0f)
                continue;
            _cooldowns[key] = ContactCooldownSeconds;

            var tackler = TryGetRatings(world, tacklerId, out var tR) ? tR : new Ratings { MS = 50, HP = 50, RS = 50 };
            var carrier = TryGetRatings(world, carrierId, out var cR) ? cR : new Ratings { MS = 50, HP = 50, RS = 50 };

            _rollIndex.TryGetValue(key, out var roll);
            roll++;
            _rollIndex[key] = roll;

            var playSeed = play is null
                ? (uint)roll
                : unchecked((uint)(play.PlayId * 131 + roll));

            var (outcome, u) = ResolveOutcome(
                playSeed: playSeed,
                tacklerId: tacklerId,
                carrierId: carrierId,
                tackler: tackler,
                carrier: carrier);

            var resolved = new TackleResolvedEvent(tacklerId, carrierId, evt.Position, outcome.ToString());
            SimEventBus.Send(ref resolved);

            switch (outcome)
            {
                case TackleOutcome.Downed:
                case TackleOutcome.FallForward:
                    if (TryForceFumble(world, ballEntityId, tacklerId, carrierId, tackler, carrier, playSeed, evt.Position, ref control))
                    {
                        FumbleTriggeredThisTick = true;
                        break;
                    }

                    WhistleDeadBall(world, ballEntityId, carrierId, ref control);
                    WhistledThisTick = true;
                    break;

                case TackleOutcome.Stumble:
                    // Mildly shorten the tackle interrupt via state timer (scaffold).
                    ApplyInterrupt(world, tacklerId, carrierId, BehaviorState.Tackling, 0.20f);
                    ApplyInterrupt(world, carrierId, tacklerId, BehaviorState.Grappling, 0.20f);
                    break;

                case TackleOutcome.BrokenTackle:
                    // No whistle; clear any active tackle interrupt by restoring immediately (best-effort).
                    ClearTopIfTackle(world, tacklerId);
                    ClearTopIfTackle(world, carrierId);
                    break;
            }

            Console.WriteLine($"[sim-arch] tackle resolve tackler={tacklerId} carrier={carrierId} u={u:0.000} outcome={outcome}");
        }
    }

    private static (TackleOutcome Outcome, float u) ResolveOutcome(uint playSeed, int tacklerId, int carrierId, Ratings tackler, Ratings carrier)
    {
        // Simple weighted win probability.
        var tacklerScore = (tackler.HP * 1.00f) + (tackler.RS * 0.35f) + (tackler.MS * 0.25f);
        var carrierResist = (carrier.HP * 1.00f) + (carrier.RS * 0.25f) + (carrier.MS * 0.35f);

        tacklerScore = MathF.Max(1f, tacklerScore);
        carrierResist = MathF.Max(1f, carrierResist);

        var pDown = tacklerScore / (tacklerScore + carrierResist);
        pDown = MathHelper.Clamp(pDown + 0.05f, 0.02f, 0.98f);

        var pStumble = 0.12f;

        var u = DeterministicFloat01(playSeed, (uint)tacklerId, (uint)carrierId, 0xC01AC7u);

        if (u < pDown)
        {
            // 20% of downs are "fall forward".
            var u2 = DeterministicFloat01(playSeed, (uint)carrierId, (uint)tacklerId, 0xF411F0D0u);
            if (u2 < 0.20f)
                return (TackleOutcome.FallForward, u);
            return (TackleOutcome.Downed, u);
        }

        if (u < pDown + pStumble)
            return (TackleOutcome.Stumble, u);

        return (TackleOutcome.BrokenTackle, u);
    }

    private static void WhistleDeadBall(World world, int ballEntityId, int carrierId, ref Control control)
    {
        // Clear velocities (carrier + ball) deterministically.
        var qVel = new QueryDescription().WithAll<Velocity>();
        world.Query(in qVel, (Entity e, ref Velocity v) =>
        {
            if (e.Id == ballEntityId || e.Id == carrierId)
                v.Value = Vector2.Zero;
        });

        // Mark ball dead, but preserve the last carrier until play-end resolution runs.
        var qBall = new QueryDescription().WithAll<Ball>();
        world.Query(in qBall, (Entity e, ref Ball b) =>
        {
            if (e.Id != ballEntityId)
                return;

            b.State = Components.BallState.Dead;
            b.OwnerEntityId = carrierId;
            b.FlightKind = BallFlightKind.None;
            b.Height = 0f;
            b.IsComplete = true;
        });

        // Control reset: if user was controlling the carrier, release it.
        if (control.ControlledEntityId == carrierId)
            control.ControlledEntityId = -1;
    }

    private static bool TryForceFumble(World world, int ballEntityId, int tacklerId, int carrierId, Ratings tackler, Ratings carrier, uint playSeed, Vector2 contactPos, ref Control control)
    {
        if (!BallIsHeldByCarrier(world, ballEntityId, carrierId))
            return false;

        var fumbleChance = MathHelper.Clamp(
            BaseForcedFumbleChance
            + ((tackler.HP - 50f) * TacklerHpFumbleWeight)
            - ((carrier.RS - 50f) * CarrierRsFumbleWeight)
            - ((carrier.HP - 50f) * CarrierHpFumbleWeight),
            0.02f,
            0.65f);

        var roll = DeterministicFloat01(playSeed, (uint)tacklerId, (uint)carrierId, 0xF00B1Eu);
        if (roll >= fumbleChance)
            return false;

        var qBall = new QueryDescription().WithAll<Ball, Position, Velocity>();
        world.Query(in qBall, (Entity e, ref Ball b, ref Position p, ref Velocity v) =>
        {
            if (e.Id != ballEntityId)
                return;

            b.State = BallState.Loose;
            b.OwnerEntityId = -1;
            b.FlightKind = BallFlightKind.None;
            b.PasserEntityId = 0;
            b.TargetEntityId = 0;
            b.IntendedReceiverRoleId = RoleId.Unknown;
            b.IntendedReceiverSlot = string.Empty;
            b.PassTargetPosition = Vector2.Zero;
            b.NearestDefenderEntityId = 0;
            b.NearestDefenderPosition = Vector2.Zero;
            b.Height = 0f;
            b.IsComplete = true;
            p.Value = contactPos;

            var dir = BuildFumbleImpulseDirection(world, carrierId, tacklerId);
            v.Value = dir * FumbleImpulsePerTick;
        });

        if (control.ControlledEntityId == carrierId)
            control.ControlledEntityId = -1;

        var fumble = new FumbleEvent(carrierId, "tackle");
        SimEventBus.Send(ref fumble);
        return true;
    }

    private static bool BallIsHeldByCarrier(World world, int ballEntityId, int carrierId)
    {
        var held = false;
        var qBall = new QueryDescription().WithAll<Ball>();
        world.Query(in qBall, (Entity e, ref Ball b) =>
        {
            if (held || e.Id != ballEntityId)
                return;

            held = b.State == BallState.Held && b.OwnerEntityId == carrierId;
        });

        return held;
    }

    private static Vector2 BuildFumbleImpulseDirection(World world, int carrierId, int tacklerId)
    {
        var carrierPos = TryGetPosition(world, carrierId, out var cPos) ? cPos : Vector2.Zero;
        var tacklerPos = TryGetPosition(world, tacklerId, out var tPos) ? tPos : carrierPos - Vector2.UnitX;
        var dir = carrierPos - tacklerPos;
        if (dir.LengthSquared() <= 0.001f)
            dir = new Vector2(1f, 0.3f);
        else
            dir.Normalize();

        dir.Y += dir.Y >= 0f ? 0.2f : -0.2f;
        if (dir.LengthSquared() <= 0.001f)
            dir = new Vector2(1f, 0.25f);
        else
            dir.Normalize();

        return dir;
    }

    private static bool TryGetPosition(World world, int entityId, out Vector2 position)
    {
        var local = Vector2.Zero;
        var found = false;
        var q = new QueryDescription().WithAll<Position>();
        world.Query(in q, (Entity e, ref Position p) =>
        {
            if (found || e.Id != entityId)
                return;

            local = p.Value;
            found = true;
        });

        position = local;
        return found;
    }

    private static void ApplyInterrupt(World world, int entityId, int targetId, BehaviorState state, float durationSeconds)
    {
        var q = new QueryDescription().WithAll<Behavior, BehaviorStack>();
        world.Query(in q, (Entity e, ref Behavior b, ref BehaviorStack stack) =>
        {
            if (e.Id != entityId)
                return;

            if (!stack.HasActive(BehaviorInterruptKind.Tackle))
                BehaviorInterrupt.Push(ref b, ref stack, BehaviorInterruptKind.Tackle, durationSeconds);

            b.State = state;
            b.StateTimer = durationSeconds;
            b.TargetEntityId = targetId;
        });
    }

    private static void ClearTopIfTackle(World world, int entityId)
    {
        var q = new QueryDescription().WithAll<Behavior, BehaviorStack>();
        world.Query(in q, (Entity e, ref Behavior b, ref BehaviorStack stack) =>
        {
            if (e.Id != entityId)
                return;

            if (!stack.TryPeek(out var top))
                return;

            if (top.Kind != BehaviorInterruptKind.Tackle)
                return;

            // Pop and restore immediately.
            if (!stack.TryPop(out var popped))
                return;

            BehaviorInterrupt.Restore(ref b, popped.Saved);
        });
    }

    private static bool TryGetRatings(World world, int id, out Ratings ratings)
    {
        ratings = default;
        var found = false;
        var local = default(Ratings);

        var q = new QueryDescription().WithAll<Ratings>();
        world.Query(in q, (Entity e, ref Ratings r) =>
        {
            if (found)
                return;
            if (e.Id != id)
                return;

            local = r;
            found = true;
        });

        if (!found)
            return false;

        ratings = local;
        return true;
    }

    private void TickCooldowns(float dt)
    {
        if (_cooldowns.Count == 0)
            return;

        var keys = new List<ulong>(_cooldowns.Keys);
        for (var i = 0; i < keys.Count; i++)
        {
            var k = keys[i];
            var v = _cooldowns[k] - dt;
            if (v <= 0f)
                _cooldowns.Remove(k);
            else
                _cooldowns[k] = v;
        }
    }

    private static ulong PairKey(int a, int b)
    {
        unchecked
        {
            return ((ulong)(uint)a << 32) | (uint)b;
        }
    }

    private static float DeterministicFloat01(uint a, uint b, uint c, uint salt)
    {
        unchecked
        {
            var x = a;
            x ^= b + 0x9E3779B9u + (x << 6) + (x >> 2);
            x ^= c + 0x85EBCA6Bu + (x << 13) + (x >> 7);
            x ^= salt + 0xC2B2AE35u + (x << 16) + (x >> 3);
            // xorshift
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;

            // 24-bit mantissa
            return (x & 0x00FFFFFFu) / 16777216f;
        }
    }
}
