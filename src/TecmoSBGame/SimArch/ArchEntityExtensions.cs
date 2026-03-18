using System;
using Arch.Core;

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
            e.Add<T>();
    }

    public static void Upsert<T>(this Entity e, in T value) where T : unmanaged
    {
        if (e.Has<T>())
        {
            e.Set(value);
        }
        else
        {
            e.Add(value);
        }
    }

    public static ref T GetOrAdd<T>(this Entity e) where T : unmanaged
    {
        if (!e.Has<T>())
            e.Add<T>();

        return ref e.Get<T>();
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
