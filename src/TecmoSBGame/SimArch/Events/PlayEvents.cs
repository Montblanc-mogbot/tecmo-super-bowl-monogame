using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch.Events;

public readonly record struct TackleResolvedEvent(int TacklerId, int CarrierId, Vector2 Position, string Outcome);
