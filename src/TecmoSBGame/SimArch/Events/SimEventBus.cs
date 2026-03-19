using System.Collections.Generic;

namespace TecmoSBGame.SimArch.Events;

/// <summary>
/// Minimal in-memory event bus for SimArch.
///
/// This is a stopgap until we wire up Arch.EventBus source generation.
/// Keep it deterministic: events are drained in FIFO order.
/// </summary>
public static class SimEventBus
{
    private static class Queue<T> where T : struct
    {
        public static readonly List<T> Items = new();
    }

    public static void Send<T>(ref T e) where T : struct
    {
        Queue<T>.Items.Add(e);
    }

    public static List<T> Drain<T>() where T : struct
    {
        var items = Queue<T>.Items;
        if (items.Count == 0)
            return items;

        // Copy out then clear so producers can continue writing without swapping the list.
        var drained = new List<T>(items);
        items.Clear();
        return drained;
    }
}

