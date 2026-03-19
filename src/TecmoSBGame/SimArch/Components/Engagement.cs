namespace TecmoSBGame.SimArch.Components;

/// <summary>
/// Tracks a short engagement cooldown so blockers/defenders don't re-engage every tick.
/// </summary>
public struct Engagement
{
    public int PartnerEntityId;
    public float CooldownSeconds;
}
