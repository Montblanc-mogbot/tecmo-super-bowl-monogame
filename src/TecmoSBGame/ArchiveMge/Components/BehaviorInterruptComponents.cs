using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TecmoSBGame.Components;

public enum BehaviorInterruptKind
{
    Engagement = 0,
    Tackle = 1,
}

public readonly record struct BehaviorSnapshot(
    BehaviorState State,
    float StateTimer,
    Vector2 TargetPosition,
    int TargetEntityId);

public readonly record struct BehaviorStackEntry(
    BehaviorInterruptKind Kind,
    BehaviorSnapshot Saved,
    float RemainingSeconds);

/// <summary>
/// Small LIFO stack used to temporarily interrupt an entity's behavior and restore it later.
/// </summary>
public sealed class BehaviorStackComponent
{
    public readonly List<BehaviorStackEntry> Stack = new(capacity: 2);

    public bool TryPeek(out BehaviorStackEntry entry)
    {
        if (Stack.Count <= 0)
        {
            entry = default;
            return false;
        }

        entry = Stack[^1];
        return true;
    }

    public void Push(BehaviorStackEntry entry) => Stack.Add(entry);

    public bool TryPop(out BehaviorStackEntry entry)
    {
        if (Stack.Count <= 0)
        {
            entry = default;
            return false;
        }

        var i = Stack.Count - 1;
        entry = Stack[i];
        Stack.RemoveAt(i);
        return true;
    }

    public bool HasActive(BehaviorInterruptKind kind)
    {
        if (Stack.Count <= 0)
            return false;

        return Stack[^1].Kind == kind;
    }
}

/// <summary>
/// Tracks a short engagement cooldown so blockers/defenders don't re-engage every tick.
/// </summary>
public sealed class EngagementComponent
{
    public int PartnerEntityId;
    public float CooldownSeconds;
}

public static class BehaviorInterrupt
{
    public static BehaviorSnapshot Snapshot(BehaviorComponent b)
        => new(
            State: b.State,
            StateTimer: b.StateTimer,
            TargetPosition: b.TargetPosition,
            TargetEntityId: b.TargetEntityId);

    public static void Restore(BehaviorComponent b, in BehaviorSnapshot s)
    {
        b.State = s.State;
        b.StateTimer = s.StateTimer;
        b.TargetPosition = s.TargetPosition;
        b.TargetEntityId = s.TargetEntityId;
    }

    public static void Push(
        BehaviorComponent behavior,
        BehaviorStackComponent stack,
        BehaviorInterruptKind kind,
        float durationSeconds)
    {
        if (durationSeconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));

        var saved = Snapshot(behavior);
        stack.Push(new BehaviorStackEntry(kind, saved, RemainingSeconds: durationSeconds));
    }
}
