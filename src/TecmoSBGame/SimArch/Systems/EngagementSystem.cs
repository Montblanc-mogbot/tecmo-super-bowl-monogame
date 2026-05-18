using System;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;
using TecmoSBGame.SimArch.Events;

namespace TecmoSBGame.SimArch.Systems;

// Ported from: src/TecmoSBGame/ArchiveMge/Systems/EngagementSystem.cs

/// <summary>
/// Consumes <see cref="BlockContactEvent"/> and temporarily interrupts both entities into an Engaged state.
///
/// This version keeps engagements deterministic but varies hold time based on blocker/rusher strength
/// and nearby support so single wins, sheds, and double-teams have visible trench consequences.
/// </summary>
public sealed class EngagementSystem
{
    public float EngagementDurationSeconds = 0.35f;
    public float EngagementCooldownSeconds = 0.60f;
    public float ContactDistancePixels = 6f;

    public void Update(World world, float dtSeconds)
    {
        if (dtSeconds > 0f)
        {
            var qEng = new QueryDescription().WithAll<Engagement>();
            world.Query(in qEng, (Entity e, ref Engagement eng) =>
            {
                if (eng.CooldownSeconds <= 0f)
                    return;

                eng.CooldownSeconds = MathF.Max(0f, eng.CooldownSeconds - dtSeconds);
                if (eng.CooldownSeconds <= 0f)
                    eng.PartnerEntityId = -1;
            });

            var qRush = new QueryDescription().WithAll<Rush, Behavior>();
            world.Query(in qRush, (Entity _, ref Rush rush, ref Behavior behavior) =>
            {
                if (rush.Engaged && behavior.State == BehaviorState.Engaged)
                    rush.EngagementFrames += Math.Max(1, (int)MathF.Round(dtSeconds * 60f));
                else if (!rush.Engaged)
                    rush.EngagementFrames = 0;
            });
        }

        foreach (var evt in SimEventBus.Drain<BlockContactEvent>())
        {
            var blockerId = evt.BlockerId;
            var defenderId = evt.DefenderId;

            if (!TryGet(world, blockerId, out var blockerB, out var blockerStack, out var blockerEng, out var blockerPos))
                continue;
            if (!TryGet(world, defenderId, out var defenderB, out var defenderStack, out var defenderEng, out var defenderPos))
                continue;

            if (blockerEng.CooldownSeconds > 0f || defenderEng.CooldownSeconds > 0f)
                continue;

            if (blockerStack.HasActive(BehaviorInterruptKind.Engagement) || defenderStack.HasActive(BehaviorInterruptKind.Engagement))
                continue;

            var distSq = Vector2.DistanceSquared(blockerPos.Value, defenderPos.Value);
            if (distSq > ContactDistancePixels * ContactDistancePixels)
                continue;

            var engagementDuration = ComputeEngagementDuration(world, blockerId, defenderId);
            var cooldown = MathF.Max(EngagementCooldownSeconds, engagementDuration + 0.12f);

            BeginEngagement(world, blockerId, defenderId, engagementDuration, cooldown);
            MarkRushEngaged(world, defenderId, blockerId, engaged: true);
        }
    }

    private void BeginEngagement(World world, int blockerId, int defenderId, float durationSeconds, float cooldownSeconds)
    {
        InterruptIntoEngaged(world, blockerId, defenderId, durationSeconds);
        InterruptIntoEngaged(world, defenderId, blockerId, durationSeconds);

        SetEngagement(world, blockerId, partnerId: defenderId, cooldownSeconds: cooldownSeconds);
        SetEngagement(world, defenderId, partnerId: blockerId, cooldownSeconds: cooldownSeconds);

        Console.WriteLine($"[sim-arch] interrupt begin kind=Engagement blocker={blockerId} defender={defenderId} duration={durationSeconds:0.00}");
    }

    private static void InterruptIntoEngaged(World world, int entityId, int partnerId, float durationSeconds)
    {
        var q = new QueryDescription().WithAll<Behavior, BehaviorStack>();
        world.Query(in q, (Entity e, ref Behavior b, ref BehaviorStack stack) =>
        {
            if (e.Id != entityId)
                return;

            BehaviorInterrupt.Push(ref b, ref stack, BehaviorInterruptKind.Engagement, durationSeconds: durationSeconds);

            b.State = BehaviorState.Engaged;
            b.StateTimer = durationSeconds;
            b.TargetEntityId = partnerId;
        });
    }

    private float ComputeEngagementDuration(World world, int blockerId, int defenderId)
    {
        var blockerStrength = GetBlockStrength(world, blockerId);
        var defenderStrength = GetRushStrength(world, defenderId);
        var supportBonus = CountSupportingBlockers(world, blockerId, defenderId) * 0.14f;
        var advantageBonus = MathHelper.Clamp((blockerStrength - defenderStrength) / 220f, -0.12f, 0.12f);
        var preferredBonus = HasPreferredMatch(world, blockerId, defenderId) ? 0.05f : 0f;
        var pressureBonus = GetPressureHelpBonus(world, blockerId, defenderId);
        var rushMovePenalty = GetRushMovePressure(world, defenderId);

        return MathHelper.Clamp(0.30f + supportBonus + advantageBonus + preferredBonus + pressureBonus - rushMovePenalty, 0.16f, 0.82f);
    }

    private static int CountSupportingBlockers(World world, int blockerId, int defenderId)
    {
        var support = 0;
        var q = new QueryDescription().WithAll<BlockTarget, Team>();
        world.Query(in q, (Entity e, ref BlockTarget blockTarget, ref Team team) =>
        {
            if (e.Id == blockerId || !team.IsOffense)
                return;

            if (blockTarget.EngagedEntityId == defenderId || blockTarget.TargetEntityId == defenderId)
                support++;
        });

        return support;
    }

    private static float GetBlockStrength(World world, int entityId)
    {
        var strength = 50f;
        var q = new QueryDescription().WithAll<Ratings>();
        world.Query(in q, (Entity e, ref Ratings ratings) =>
        {
            if (e.Id != entityId)
                return;

            strength = (ratings.HP * 0.65f) + (ratings.RS * 0.35f);
        });

        return strength;
    }

    private static float GetRushStrength(World world, int entityId)
    {
        var strength = 50f;
        var q = new QueryDescription().WithAll<Ratings>();
        world.Query(in q, (Entity e, ref Ratings ratings) =>
        {
            if (e.Id != entityId)
                return;

            strength = (ratings.HP * 0.55f) + (ratings.MS * 0.45f);
        });

        return strength;
    }

    private static float GetRushMovePressure(World world, int defenderId)
    {
        var penalty = 0f;
        var q = new QueryDescription().WithAll<Rush>();
        world.Query(in q, (Entity e, ref Rush rush) =>
        {
            if (e.Id != defenderId)
                return;

            penalty = MathHelper.Clamp((rush.FailedRushMoveCount * 0.02f) - (rush.EngagementFrames / 240f), -0.04f, 0.08f);
        });

        return penalty;
    }

    private static void SetEngagement(World world, int entityId, int partnerId, float cooldownSeconds)
    {
        var q = new QueryDescription().WithAll<Engagement>();
        world.Query(in q, (Entity e, ref Engagement eng) =>
        {
            if (e.Id != entityId)
                return;

            eng.PartnerEntityId = partnerId;
            eng.CooldownSeconds = cooldownSeconds;
        });
    }

    private static bool HasPreferredMatch(World world, int blockerId, int defenderId)
    {
        var matched = false;
        var defenderSlot = string.Empty;

        var qDef = new QueryDescription().WithAll<PlayerRole>();
        world.Query(in qDef, (Entity e, ref PlayerRole role) =>
        {
            if (e.Id == defenderId)
                defenderSlot = role.Slot ?? string.Empty;
        });

        var qBlock = new QueryDescription().WithAll<BlockTarget>();
        world.Query(in qBlock, (Entity e, ref BlockTarget blockTarget) =>
        {
            if (e.Id != blockerId)
                return;

            var preferred = (blockTarget.PreferredDefenderKey ?? string.Empty).Trim().ToUpperInvariant();
            var slot = defenderSlot.Trim().ToUpperInvariant();
            matched = preferred switch
            {
                "RE" => slot == "DE-R",
                "LE" => slot == "DE-L",
                "NT" => slot.StartsWith("DT", StringComparison.Ordinal),
                "RILB" => slot is "LB-R" or "MLB",
                "LILB" => slot is "LB-L" or "MLB",
                "ROLB" => slot == "LB-R",
                "LOLB" => slot == "LB-L",
                "RCB" => slot == "CB-R",
                "LCB" => slot == "CB-L",
                _ => string.IsNullOrEmpty(preferred) || slot.Contains(preferred, StringComparison.Ordinal),
            };
        });

        return matched;
    }

    private static float GetPressureHelpBonus(World world, int blockerId, int defenderId)
    {
        var bonus = 0f;
        var q = new QueryDescription().WithAll<BlockTarget>();
        world.Query(in q, (Entity e, ref BlockTarget blockTarget) =>
        {
            if (e.Id != blockerId)
                return;

            if (blockTarget.TargetEntityId == defenderId && blockTarget.PressureContributionFrames > 0)
                bonus = MathHelper.Clamp(blockTarget.PressureContributionFrames / 180f, 0f, 0.08f);
        });

        return bonus;
    }

    private static void MarkRushEngaged(World world, int defenderId, int blockerId, bool engaged)
    {
        var q = new QueryDescription().WithAll<Rush>();
        world.Query(in q, (Entity e, ref Rush rush) =>
        {
            if (e.Id != defenderId)
                return;

            rush.Engaged = engaged;
            rush.EngagedBlockerId = engaged ? blockerId : -1;
            if (!engaged)
                rush.EngagementFrames = 0;
        });
    }

    private static bool TryGet(World world, int id, out Behavior b, out BehaviorStack stack, out Engagement eng, out Position pos)
    {
        b = default;
        stack = default;
        eng = default;
        pos = default;

        var found = false;
        var bLocal = default(Behavior);
        var sLocal = default(BehaviorStack);
        var eLocal = default(Engagement);
        var pLocal = default(Position);

        var q = new QueryDescription().WithAll<Behavior, BehaviorStack, Engagement, Position>();
        world.Query(in q, (Entity e, ref Behavior bb, ref BehaviorStack ss, ref Engagement ee, ref Position pp) =>
        {
            if (found)
                return;
            if (e.Id != id)
                return;

            bLocal = bb;
            sLocal = ss;
            eLocal = ee;
            pLocal = pp;
            found = true;
        });

        if (!found)
            return false;

        b = bLocal;
        stack = sLocal;
        eng = eLocal;
        pos = pLocal;
        return true;
    }
}
