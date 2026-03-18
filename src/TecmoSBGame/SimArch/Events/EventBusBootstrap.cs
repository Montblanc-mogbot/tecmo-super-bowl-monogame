using System;
using Arch.EventBus;

namespace TecmoSBGame.SimArch.Events;

/// <summary>
/// Centralizes EventBus init/warmup patterns.
///
/// Arch.EventBus is source-generated; you typically don't need runtime init.
/// This class exists to make the dependency explicit and provide a single place
/// to add any future warmup/diagnostics.
/// </summary>
public static class EventBusBootstrap
{
    public static void SanityCheck()
    {
        // Touch EventBus type so the reference doesn't get trimmed in aggressive publish modes.
        _ = typeof(EventBus);
        Console.WriteLine("[sim-arch] EventBus available");
    }
}
