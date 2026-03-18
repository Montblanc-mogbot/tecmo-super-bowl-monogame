namespace TecmoSBGame.SimArch.Events;

// NOTE: All events are structs for performance and to match Arch.EventBus patterns.

public readonly record struct SnapEvent(int OffenseTeamIndex, int DefenseTeamIndex);

public readonly record struct HandoffEvent(int FromEntityId, int ToEntityId);

public readonly record struct WhistleEvent(string Reason);

public readonly record struct PlayEndedEvent(string Reason);

public readonly record struct PlaySelectedEvent(int PlayNumber, string FormationId, string OffensivePlayName, string OffensivePlaySlot);
