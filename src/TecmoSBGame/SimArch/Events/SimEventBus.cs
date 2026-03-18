namespace TecmoSBGame.SimArch.Events;

/// <summary>
/// Thin wrapper around Arch.EventBus.
///
/// Per Arch.EventBus docs, the source generator emits a global static EventBus type.
/// We intentionally avoid hard-binding to a package-defined namespace/type.
/// </summary>
public static class SimEventBus
{
    public static void Send<T>(ref T e) where T : struct
    {
        EventBus.Send(ref e);
    }
}
