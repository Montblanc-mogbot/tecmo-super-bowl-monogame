using Microsoft.Xna.Framework;

namespace TecmoSBGame.SimArch;

/// <summary>
/// Render-facing DTO for the Arch simulation.
///
/// Keep this as a stable reference so render code doesn't chase allocations.
/// </summary>
public sealed class SimSnapshot
{
    public int Tick;

    public PlayerSnapshot[] Players = Array.Empty<PlayerSnapshot>();
    public BallSnapshot Ball;

    public sealed class PlayerSnapshot
    {
        public int EntityId;
        public Vector2 Position;
        public int TeamIndex;
        public bool IsOffense;
        public bool HasBall;
        public string SpriteId = "";
    }

    public struct BallSnapshot
    {
        public Vector2 Position;
        public bool IsHeld;
        public int OwnerEntityId;
        public string SpriteId;
    }
}
