namespace TecmoSBGame.SimArch.Events;

/// <summary>
/// Event dispatch abstraction for SimArch.
///
/// We *intend* to use Arch.EventBus (source generated) here.
/// However, until receiver wiring exists (and therefore the generator emits a bus),
/// this is a safe no-op shim that keeps SimArch code compiling.
/// </summary>
public static class SimEventBus
{
    public static void Send<T>(ref T _) where T : struct
    {
        // Intentionally no-op for now.
        // When we add receiver methods (with the Arch.EventBus attributes), we'll swap this
        // to call the generated bus type.
    }
}
