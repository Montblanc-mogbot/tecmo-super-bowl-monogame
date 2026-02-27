using System;

namespace TecmoSBGame.State;

/// <summary>
/// Small shared service holding the authoritative "who is currently controlled" selection.
///
/// This is intentionally tiny and deterministic: systems read/update this during a fixed tick.
/// </summary>
public sealed class ControlState
{
    /// <summary>
    /// Entity id currently receiving player input. Null means "no controlled entity".
    /// </summary>
    public int? ControlledEntityId { get; private set; }

    /// <summary>
    /// Team index that the human is currently playing as (best-effort).
    /// Used as a stable tie-break when multiple entities are tagged as player-team.
    /// </summary>
    public int? ControlledTeamIndex { get; set; }

    /// <summary>
    /// Last known control role (purely for logging/debugging; gameplay does not depend on it).
    /// </summary>
    public ControlRole Role { get; set; } = ControlRole.Unknown;

    // Input edge tracking (kept here so headless can disable input deterministically).
    internal bool PrevSwitchDown;

    public void SetControlledEntity(int? entityId)
    {
        ControlledEntityId = entityId;
    }
}

public enum ControlRole
{
    Unknown = 0,
    Quarterback,
    BallCarrier,
    Receiver,
    DefenderNearestBall,
}
