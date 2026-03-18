using Arch.EventBus;

namespace TecmoSBGame.SimArch.Events;

/// <summary>
/// Thin wrapper around Arch.EventBus to keep call-sites consistent and allow us to swap
/// implementation if the Arch docs recommend different patterns.
/// </summary>
public static class SimEventBus
{
    public static void Send<T>(ref T e) where T : struct
    {
        EventBus.Send(ref e);
    }
}
