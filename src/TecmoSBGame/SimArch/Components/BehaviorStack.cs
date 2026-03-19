using System;

namespace TecmoSBGame.SimArch.Components;

public enum BehaviorInterruptKind
{
    Engagement = 0,
    Tackle = 1,
}

public struct BehaviorSnapshot
{
    public BehaviorState State;
    public float StateTimer;
    public Microsoft.Xna.Framework.Vector2 TargetPosition;
    public int TargetEntityId;
}

public struct BehaviorStackEntry
{
    public BehaviorInterruptKind Kind;
    public BehaviorSnapshot Saved;
    public float RemainingSeconds;
}

/// <summary>
/// Small LIFO stack used to temporarily interrupt an entity's behavior and restore it later.
///
/// IMPORTANT: Avoid managed allocations inside components.
/// We store a fixed-size stack of 2 entries.
/// </summary>
public struct BehaviorStack
{
    public int Count;
    public BehaviorStackEntry E0;
    public BehaviorStackEntry E1;

    public bool TryPeek(out BehaviorStackEntry entry)
    {
        if (Count <= 0)
        {
            entry = default;
            return false;
        }

        entry = Count == 1 ? E0 : E1;
        return true;
    }

    public void Push(in BehaviorStackEntry entry)
    {
        if (Count >= 2)
            throw new InvalidOperationException("BehaviorStack overflow (max=2)");

        if (Count == 0) E0 = entry;
        else E1 = entry;

        Count++;
    }

    public bool TryPop(out BehaviorStackEntry entry)
    {
        if (Count <= 0)
        {
            entry = default;
            return false;
        }

        if (Count == 1)
        {
            entry = E0;
            E0 = default;
            Count = 0;
            return true;
        }

        entry = E1;
        E1 = default;
        Count = 1;
        return true;
    }

    public bool HasActive(BehaviorInterruptKind kind)
    {
        if (Count <= 0)
            return false;

        var top = Count == 1 ? E0 : E1;
        return top.Kind == kind;
    }
}

public static class BehaviorInterrupt
{
    public static BehaviorSnapshot Snapshot(in Behavior b)
        => new()
        {
            State = b.State,
            StateTimer = b.StateTimer,
            TargetPosition = b.TargetPosition,
            TargetEntityId = b.TargetEntityId,
        };

    public static void Restore(ref Behavior b, in BehaviorSnapshot s)
    {
        b.State = s.State;
        b.StateTimer = s.StateTimer;
        b.TargetPosition = s.TargetPosition;
        b.TargetEntityId = s.TargetEntityId;
    }

    public static void Push(ref Behavior behavior, ref BehaviorStack stack, BehaviorInterruptKind kind, float durationSeconds)
    {
        if (durationSeconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));

        var saved = Snapshot(behavior);
        stack.Push(new BehaviorStackEntry { Kind = kind, Saved = saved, RemainingSeconds = durationSeconds });
    }
}
