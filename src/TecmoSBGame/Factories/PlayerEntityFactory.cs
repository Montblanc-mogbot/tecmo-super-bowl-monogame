using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using TecmoSBGame.Components;

namespace TecmoSBGame.Factories;

/// <summary>
/// Factory for creating player entities with consistent configuration.
/// Centralizes player entity creation and ensures all required components are attached.
/// </summary>
public static class PlayerEntityFactory
{
    /// <summary>
    /// Creates a basic player entity with all required components.
    /// </summary>    
    public static int CreatePlayer(
        World world,
        Vector2 position,
        int teamIndex,
        bool isPlayerControlled,
        bool isOffense,
        string spriteId = "player_placeholder",
        float maxSpeed = 2.5f,
        float acceleration = 0.3f)
    {
        var entity = world.CreateEntity();

        entity.Attach(new PositionComponent(position));
        entity.Attach(new VelocityComponent(maxSpeed, acceleration));
        entity.Attach(new MovementTuningComponent(
            maxSpeedPerTick: maxSpeed,
            accelPerTick: MathHelper.Clamp(acceleration, 0.05f, 1f) * maxSpeed,
            decelPerTick: maxSpeed * 4.0f,
            cutPenalty: 0.25f,
            burstMultiplier: 1.20f));
        entity.Attach(new MovementInputComponent());
        entity.Attach(new MovementActionComponent());
        entity.Attach(new PlayerActionStateComponent());
        entity.Attach(new TeamComponent 
        { 
            TeamIndex = teamIndex, 
            IsPlayerControlled = isPlayerControlled,
            IsOffense = isOffense
        });
        entity.Attach(new BehaviorComponent 
        { 
            State = BehaviorState.Idle,
            TargetPosition = position
        });
        entity.Attach(new SpriteComponent(spriteId));
        entity.Attach(new BallCarrierComponent { HasBall = false });
        entity.Attach(new PlayerControlComponent { IsControlled = false });

        return entity.Id;
    }

    /// <summary>
    /// Creates a player with full attributes (QB, RB, WR, etc.).
    /// </summary>
    public static int CreatePlayerWithAttributes(
        World world,
        Vector2 position,
        int teamIndex,
        bool isPlayerControlled,
        bool isOffense,
        string positionName,
        string playerName,
        int jerseyNumber,
        PlayerStats stats,
        string spriteId = "player_placeholder")
    {
        var entityId = CreatePlayer(world, position, teamIndex, isPlayerControlled, isOffense, spriteId);

        var entity = world.GetEntity(entityId);
        
        entity.Attach(new PlayerAttributesComponent
        {
            Position = positionName,
            Name = playerName,
            JerseyNumber = jerseyNumber,
            Hp = stats.Hp,
            Rs = stats.Rs,
            Ms = stats.Ms,
            Rp = stats.Rp,
            Bc = stats.Bc,
            Rec = stats.Rec,
            Pa = stats.Pa,
            Ar = stats.Ar,
            Kp = stats.Kp,
            Kab = stats.Kab
        });

        // Deterministic mapping from ratings/role to movement tuning.
        if (entity.Get<MovementTuningComponent>() is { } tuning)
        {
            ApplyMovementTuningFromAttributes(tuning, positionName, stats);
        }

        return entityId;
    }

    private static void ApplyMovementTuningFromAttributes(MovementTuningComponent tuning, string positionName, PlayerStats stats)
    {
        // Ratings are assumed 0..100.
        var rs = MathHelper.Clamp(stats.Rs / 100f, 0f, 1f);
        var ms = MathHelper.Clamp(stats.Ms / 100f, 0f, 1f);

        // Role modifiers: RB/WR/DB feel shiftier; linemen less so.
        var pos = (positionName ?? string.Empty).Trim().ToUpperInvariant();
        var shifty = pos is "RB" or "WR" or "DB" or "CB" or "S";
        var heavy = pos is "OL" or "OT" or "OG" or "C" or "DL" or "DT" or "DE" or "NT" or "LB";

        // Baselines (units per 60Hz tick): keep close to existing maxSpeed defaults (~2.0-3.0).
        var baseMax = MathHelper.Lerp(1.85f, 3.65f, ms);
        if (shifty) baseMax *= 1.05f;
        if (heavy) baseMax *= 0.92f;

        // Acceleration: higher RS ramps faster.
        var baseAccel = MathHelper.Lerp(0.10f, 0.42f, rs);
        if (shifty) baseAccel *= 1.10f;
        if (heavy) baseAccel *= 0.88f;

        // Decel: big so releasing input stops quickly.
        var baseDecel = MathHelper.Lerp(1.8f, 3.8f, rs);

        // Cut penalty: higher RS means less penalty.
        var baseCutPenalty = MathHelper.Lerp(0.45f, 0.18f, rs);
        if (shifty) baseCutPenalty *= 0.85f;
        if (heavy) baseCutPenalty *= 1.10f;

        var burstMult = shifty ? 1.28f : 1.20f;

        tuning.MaxSpeedPerTick = baseMax;
        tuning.AccelPerTick = baseAccel;
        tuning.DecelPerTick = baseDecel;
        tuning.CutPenalty = MathHelper.Clamp(baseCutPenalty, 0.05f, 0.85f);
        tuning.BurstMultiplier = burstMult;
        tuning.UseAccelCurve = true;
    }

    /// <summary>
    /// Creates a kicker for kickoff scenarios.
    /// </summary>
    public static int CreateKicker(
        World world,
        Vector2 position,
        int teamIndex,
        bool isPlayerControlled = true)
    {
        return CreatePlayer(
            world, 
            position, 
            teamIndex, 
            isPlayerControlled, 
            true, // kicker is on offense during kick
            "player_kicker",
            maxSpeed: 2.0f, // Kickers are slower
            acceleration: 0.25f);
    }

    /// <summary>
    /// Creates a return specialist.
    /// </summary>
    public static int CreateReturner(
        World world,
        Vector2 position,
        int teamIndex,
        bool isPlayerControlled = false)
    {
        var entityId = CreatePlayer(
            world,
            position,
            teamIndex,
            isPlayerControlled,
            true, // returner is on offense
            "player_returner",
            maxSpeed: 3.0f, // Returners are faster
            acceleration: 0.4f);

        var entity = world.GetEntity(entityId);
        
        // Returners do not automatically start with the ball.
        // The kickoff slice assigns possession to the dedicated ball entity (and later to the returner on catch).
        if (entity.Get<BallCarrierComponent>() is { } ballCarrier)
        {
            ballCarrier.HasBall = false;
        }

        return entityId;
    }

    /// <summary>
    /// Creates a coverage team player (fast, pursuit-focused).
    /// </summary>
    public static int CreateCoveragePlayer(
        World world,
        Vector2 position,
        int teamIndex)
    {
        return CreatePlayer(
            world,
            position,
            teamIndex,
            isPlayerControlled: false,
            isOffense: false,
            "player_coverage",
            maxSpeed: 2.8f, // Fast for coverage
            acceleration: 0.35f);
    }

    /// <summary>
    /// Creates a blocker (slower, stronger).
    /// </summary>
    public static int CreateBlocker(
        World world,
        Vector2 position,
        int teamIndex)
    {
        return CreatePlayer(
            world,
            position,
            teamIndex,
            isPlayerControlled: false,
            isOffense: true,
            "player_blocker",
            maxSpeed: 2.0f,
            acceleration: 0.2f);
    }

    /// <summary>
    /// Creates a full team of 11 players for a given formation.
    /// </summary>
    public static List<int> CreateTeam(
        World world,
        int teamIndex,
        bool isPlayerControlled,
        bool isOffense,
        FormationType formation)
    {
        var playerIds = new List<int>();

        switch (formation)
        {
            case FormationType.Kickoff:
                // Kicker + coverage team
                playerIds.Add(CreateKicker(world, new Vector2(40, 112), teamIndex, isPlayerControlled));
                playerIds.Add(CreateCoveragePlayer(world, new Vector2(30, 80), teamIndex));
                playerIds.Add(CreateCoveragePlayer(world, new Vector2(30, 144), teamIndex));
                playerIds.Add(CreateCoveragePlayer(world, new Vector2(20, 112), teamIndex));
                // Fill remaining with generic players
                for (int i = 0; i < 7; i++)
                {
                    playerIds.Add(CreatePlayer(world, new Vector2(20 + i * 5, 112), teamIndex, false, isOffense));
                }
                break;

            case FormationType.Return:
                // Returner + blockers
                playerIds.Add(CreateReturner(world, new Vector2(200, 112), teamIndex, isPlayerControlled));
                playerIds.Add(CreateBlocker(world, new Vector2(210, 80), teamIndex));
                playerIds.Add(CreateBlocker(world, new Vector2(210, 144), teamIndex));
                playerIds.Add(CreateBlocker(world, new Vector2(220, 112), teamIndex));
                // Fill remaining with generic players
                for (int i = 0; i < 7; i++)
                {
                    playerIds.Add(CreatePlayer(world, new Vector2(220 + i * 5, 112), teamIndex, false, isOffense));
                }
                break;

            default:
                // Generic 11 players
                for (int i = 0; i < 11; i++)
                {
                    playerIds.Add(CreatePlayer(
                        world, 
                        new Vector2(100 + i * 10, 112), 
                        teamIndex, 
                        i == 0 && isPlayerControlled, // First player is controlled if specified
                        isOffense));
                }
                break;
        }

        return playerIds;
    }
}

/// <summary>
/// Player stats for attribute initialization.
/// </summary>
public record PlayerStats(
    int Hp = 50,  // Hitting Power
    int Rs = 50,  // Running Speed
    int Ms = 50,  // Maximum Speed
    int Rp = 50,  // Running Power
    int Bc = 50,  // Ball Control
    int Rec = 50, // Receiving
    int Pa = 50,  // Pass Accuracy
    int Ar = 50,  // Avoid Rush
    int Kp = 50,  // Kicking Power
    int Kab = 50  // Kicking Accuracy
);

/// <summary>
/// Predefined formation types for team creation.
/// </summary>
public enum FormationType
{
    Generic,
    Kickoff,
    Return,
    Offense,
    Defense
}
