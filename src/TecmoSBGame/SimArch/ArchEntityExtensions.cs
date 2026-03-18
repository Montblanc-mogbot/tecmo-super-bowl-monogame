using System;
using Arch.Core;
using Arch.Core.Extensions;

namespace TecmoSBGame.SimArch;

/// <summary>
/// Arch entity helper methods.
///
/// Principles (doc-driven):
/// - Check Has&lt;T&gt;() before Add/Set.
/// - Prefer creating entities with all required components up-front.
/// - Avoid structural changes during iteration unless using the documented approach.
/// </summary>
public static class ArchEntityExtensions
{
    public static void Ensure<T>(this Entity e) where T : unmanaged
    {
        if (!e.Has<T>())
            e.Add(default(T));
    }

    public static void Upsert<T>(this Entity e, in T value) where T : unmanaged
    {
        if (e.Has<T>())
            e.Set(in value);
        else
            e.Add(in value);
    }

    public static T GetOrAdd<T>(this Entity e) where T : unmanaged
    {
        if (!e.Has<T>())
            e.Add(default(T));

        return e.Get<T>();
    }

    public static void RemoveIfPresent<T>(this Entity e) where T : unmanaged
    {
        if (e.Has<T>())
            e.Remove<T>();
    }

    public static void Require<T>(this Entity e, string? context = null) where T : unmanaged
    {
        if (!e.Has<T>())
            throw new InvalidOperationException($"Arch entity missing component {typeof(T).Name}{(context is null ? "" : $" ({context})")}");
    }
}
