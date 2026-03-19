using System;
using System.Collections;
using System.Collections.Generic;

namespace TecmoSBGame.Events;

/// <summary>
/// Lightweight in-process event bus intended for deterministic, decoupled ECS systems.
///
/// Usage model:
/// - Driver (MainGame/headless loop) calls <see cref="BeginTick"/> once per simulation tick.
/// - Systems publish events during their Update.
/// - Systems consume events during their Update (typically later in the system order).
///
/// Notes:
/// - This is intentionally small (no threading, no async, no global statics).
/// - Consumption is polling-based to keep determinism explicit.
/// </summary>
public sealed class GameEvents
{
    private readonly Dictionary<Type, IList> _queues = new();

    /// <summary>
    /// Clears all queued events. Call once at the start of each simulation tick.
    /// </summary>
    public void BeginTick()
    {
        foreach (var kv in _queues)
            kv.Value.Clear();
    }

    public void Publish<TEvent>(in TEvent evt) where TEvent : struct
    {
        var list = GetOrCreateQueue<TEvent>();
        list.Add(evt);
    }

    /// <summary>
    /// Returns the currently queued events for this tick.
    /// Prefer <see cref="Drain{TEvent}(Action{TEvent})"/> when only one consumer exists.
    /// </summary>
    public IReadOnlyList<TEvent> Read<TEvent>() where TEvent : struct
        => GetOrCreateQueue<TEvent>();

    /// <summary>
    /// Processes and clears the queued events of this type.
    /// </summary>
    public void Drain<TEvent>(Action<TEvent> handler) where TEvent : struct
    {
        var list = GetOrCreateQueue<TEvent>();
        for (var i = 0; i < list.Count; i++)
            handler(list[i]);
        list.Clear();
    }

    private List<TEvent> GetOrCreateQueue<TEvent>() where TEvent : struct
    {
        var type = typeof(TEvent);
        if (_queues.TryGetValue(type, out var existing))
            return (List<TEvent>)existing;

        var created = new List<TEvent>(capacity: 4);
        _queues[type] = created;
        return created;
    }
}