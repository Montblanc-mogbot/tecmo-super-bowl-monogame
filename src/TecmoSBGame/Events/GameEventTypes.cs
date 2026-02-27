using Microsoft.Xna.Framework;

namespace TecmoSBGame.Events;

// Keep these as small structs for low allocation / easy determinism.

public readonly record struct SnapEvent(int OffenseTeam, int DefenseTeam);

public readonly record struct BallCaughtEvent(int ReceiverId, Vector2 Position);

public enum PassOutcome
{
    Catch = 0,
    Interception = 1,
    Incomplete = 2,
}

/// <summary>
/// High-level resolution event emitted when a pass flight completes.
/// WinnerId is the entity that ends up possessing the ball (receiver/defender) or null for incompletions.
/// </summary>
public readonly record struct PassResolvedEvent(PassOutcome Outcome, int PasserId, int? TargetId, int? WinnerId, Vector2 BallPosition);

public readonly record struct TackleEvent(int TacklerId, int BallCarrierId, Vector2 Position);

/// <summary>
/// Low-level contact signal emitted by collision checks when a defender is close enough to the ball carrier.
/// Downstream systems decide whether this becomes a full tackle/whistle/fumble/etc.
/// </summary>
public readonly record struct TackleContactEvent(int DefenderId, int BallCarrierId, Vector2 Position);

/// <summary>
/// Low-level contact signal emitted by collision checks when an offensive player engages a defender.
/// Downstream systems decide consequences (slowdown, animations, lane creation, etc.).
/// </summary>
public readonly record struct BlockContactEvent(int BlockerId, int DefenderId, Vector2 Position);

// High-level intent events (requested by input/AI; resolved by gameplay systems).
public readonly record struct TackleAttemptEvent(int TacklerId, int BallCarrierId, Vector2 Position);
public enum PassType
{
    Bullet = 0,
    Lob = 1,
}

public readonly record struct PassRequestedEvent(int PasserId, int? TargetId = null, PassType PassType = PassType.Bullet);
public readonly record struct PitchRequestedEvent(int BallCarrierId);

public readonly record struct WhistleEvent(string Reason);

/// <summary>
/// Derived high-level signal published by <see cref="TecmoSBGame.Systems.PlayEndSystem"/> when it finalizes a play.
/// Consumers that need a single authoritative end-of-play hook should prefer this over raw whistles.
/// </summary>
public readonly record struct PlayEndedEvent(
    int PlayId,
    TecmoSBGame.State.WhistleReason Reason,
    int EndAbsoluteYard,
    int YardsGained,
    bool Turnover,
    bool Touchdown,
    bool Safety);

/// <summary>
/// High-level fumble event: the ball carrier lost possession due to a hit/tackle/etc.
/// The actual ball state transition is handled by gameplay systems consuming this event.
/// </summary>
public readonly record struct FumbleEvent(int CarrierId, string Cause);

/// <summary>
/// Emitted when a loose ball is recovered by a player.
/// </summary>
public readonly record struct LooseBallPickupEvent(int PickerId, Vector2 BallPosition);