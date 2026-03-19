using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Events;

// Keep these as small structs for low allocation / deterministic sim.

public readonly record struct TackleContactEvent(int DefenderId, int BallCarrierId, Vector2 Position);

public readonly record struct BlockContactEvent(int BlockerId, int DefenderId, Vector2 Position);
