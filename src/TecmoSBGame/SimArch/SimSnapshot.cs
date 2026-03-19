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

    // Debug overlays (optional)
    public EngagementLine[] EngagementLines = Array.Empty<EngagementLine>();
    public RouteDebug[] Routes = Array.Empty<RouteDebug>();
    public CoverageDebug[] Coverage = Array.Empty<CoverageDebug>();

    // HUD
    public HudSnapshot Hud;

    public sealed class PlayerSnapshot
    {
        public int EntityId;
        public Vector2 Position;
        public int TeamIndex;
        public bool IsOffense;
        public bool HasBall;
        public string SpriteId = "";

        // Debug-friendly labels
        public string Role = "";
        public string Behavior = "";
    }

    public struct BallSnapshot
    {
        public Vector2 Position;
        public bool IsHeld;
        public int OwnerEntityId;
        public string SpriteId;
    }

    public readonly record struct EngagementLine(int A, int B, Vector2 APos, Vector2 BPos);

    public readonly record struct RouteDebug(int EntityId, Vector2 TargetPosition, int NodeIndex, int FramesRemaining, bool Completed);

    public readonly record struct CoverageDebug(int DefenderId, SnapshotCoverageType Type, int AssignmentTargetId, int PursuitTargetId, bool InPursuit, Vector2 Landmark);

    public struct HudSnapshot
    {
        public int Quarter;
        public int GameClockSeconds;

        public int Team0Score;
        public int Team1Score;

        public int Down;
        public int YardsToGo;
        public bool BallOnOwnSide;
        public int BallYards;
    }
}

// Snapshot-only enum to avoid type name collisions with SimArch.Components.CoverageType.
public enum SnapshotCoverageType
{
    ManToMan = 0,
    ZoneDeep = 1,
    ZoneFlat = 2,
    ZoneHook = 3,
    ZoneCurl = 4,
    DeepHalf = 5,
    DeepThird = 6,
    DeepQuarter = 7,
}

