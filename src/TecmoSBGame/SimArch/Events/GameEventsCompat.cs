using System;
using System.Collections;
using System.Collections.Generic;

namespace TecmoSBGame.SimArch.Events;

/// <summary>
/// Legacy-style, instance-based event bus.
///
/// SimArch currently uses <see cref="SimEventBus"/> (static queues). This wrapper exists
/// to preserve the ArchiveMge API shape for ports/tests that expect a per-sim instance.
///
/// Ported from: ArchiveMge/Events/GameEvents.cs
/// </summary>
public sealed class GameEvents
{
    private readonly Dictionary<Type, IList> _queues = new();

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

    public IReadOnlyList<TEvent> Read<TEvent>() where TEvent : struct
        => GetOrCreateQueue<TEvent>();

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
