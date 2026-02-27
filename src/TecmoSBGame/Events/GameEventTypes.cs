using Microsoft.Xna.Framework;

namespace TecmoSBGame.Events;

// Keep these as small structs for low allocation / easy determinism.

public readonly record struct SnapEvent(int OffenseTeam, int DefenseTeam);

public readonly record struct BallCaughtEvent(int ReceiverId, Vector2 Position);

public readonly record struct TackleEvent(int TacklerId, int BallCarrierId, Vector2 Position);

// High-level intent events (requested by input/AI; resolved by gameplay systems).
public readonly record struct TackleAttemptEvent(int TacklerId, int BallCarrierId, Vector2 Position);
public readonly record struct PassRequestedEvent(int QuarterbackId);
public readonly record struct PitchRequestedEvent(int BallCarrierId);

public readonly record struct WhistleEvent(string Reason);