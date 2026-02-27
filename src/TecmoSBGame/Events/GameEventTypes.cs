using Microsoft.Xna.Framework;

namespace TecmoSBGame.Events;

// Keep these as small structs for low allocation / easy determinism.

public readonly record struct SnapEvent(int OffenseTeam, int DefenseTeam);

public readonly record struct BallCaughtEvent(int ReceiverId, Vector2 Position);

public readonly record struct TackleEvent(int TacklerId, int BallCarrierId, Vector2 Position);

public readonly record struct WhistleEvent(string Reason);