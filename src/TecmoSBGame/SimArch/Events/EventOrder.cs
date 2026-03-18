namespace TecmoSBGame.SimArch.Events;

/// <summary>
/// EventBus receiver ordering conventions.
///
/// Arch.EventBus dispatches:
/// - static receivers first,
/// - then instance receivers,
/// and supports explicit ordering via [Event(order: N)].
///
/// Convention (lower runs earlier):
/// - 0..99   : simulation rule state changes (ownership, phase transitions)
/// - 100..199: physics / movement consequences
/// - 200..299: logging / analytics / snapshot updates
/// - 300..399: UI notifications (non-sim) (usually instance receivers outside SimArch)
/// </summary>
public static class EventOrder
{
    public const int Rules = 50;
    public const int Physics = 150;
    public const int Logging = 250;
    public const int Ui = 350;
}
