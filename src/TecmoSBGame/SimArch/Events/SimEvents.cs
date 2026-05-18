using Microsoft.Xna.Framework;
using TecmoSBGame.SimArch.Components;

namespace TecmoSBGame.SimArch.Events;

// NOTE: All events are structs for performance and deterministic sim.
// Ported from: ArchiveMge/Events/GameEventTypes.cs (partial; contact events live in ContactEvents.cs)

public readonly record struct SnapEvent(int OffenseTeam, int DefenseTeam);

public readonly record struct HandoffEvent(int FromEntityId, int ToEntityId);

public readonly record struct BallCaughtEvent(int ReceiverId, Vector2 Position);

public enum PassOutcome
{
    Catch = 0,
    Interception = 1,
    Incomplete = 2,
}

public readonly record struct PassResolvedEvent(
    PassOutcome Outcome,
    int PasserId,
    int? TargetId,
    RoleId IntendedReceiverRoleId,
    string IntendedReceiverSlot,
    int? WinnerId,
    int? PrimaryDefenderId,
    Vector2 TargetPosition,
    Vector2 BallPosition);

public readonly record struct TackleEvent(int TacklerId, int BallCarrierId, Vector2 Position);

public readonly record struct TackleAttemptEvent(int TacklerId, int BallCarrierId, Vector2 Position);

public enum PenaltyType
{
    Unknown = 0,
    Offsides = 1,
}

public readonly record struct PenaltyEvent(int PlayId, int TeamIndex, PenaltyType Type, string Detail);
public readonly record struct PenaltyAssessedEvent(int PlayId, int AgainstTeamIndex, PenaltyType Type, int Yards, bool Accepted);

public readonly record struct PassRequestedEvent(int PasserId, int? TargetId = null, TecmoSBGame.SimArch.PassType PassType = TecmoSBGame.SimArch.PassType.Bullet);
public readonly record struct PitchRequestedEvent(int BallCarrierId);

public readonly record struct WhistleEvent(string Reason);

// SimArch rules/state port pending; we keep Reason as int for now.
public readonly record struct PlayEndedEvent(
    int PlayId,
    int Reason,
    int EndAbsoluteYard,
    int YardsGained,
    bool Turnover,
    bool Touchdown,
    bool Safety);

public readonly record struct ResetToPreSnapEvent(int FromPlayId);
public readonly record struct AdvanceToNextPlayEvent(int FromPlayId);

public enum KickoffSetupReason
{
    AfterTouchdown = 0,
    AfterSafety = 1,
}

public readonly record struct KickoffSetupEvent(int KickingTeam, int ReceivingTeam, KickoffSetupReason Reason);

public readonly record struct FumbleEvent(int CarrierId, string Cause);
public readonly record struct LooseBallPickupEvent(int PickerId, Vector2 BallPosition);

public readonly record struct PostPlayContinueRequestedEvent(int FromPlayId);

public readonly record struct QuarterEndedEvent(int Quarter);
public readonly record struct HalftimeEvent();
public readonly record struct GameEndedEvent(int FinalQuarter);

public readonly record struct PlaySelectedEvent(
    string OffensiveFormationId,
    string OffensivePlayName,
    string OffensivePlaySlot,
    int OffensivePlayNumber,
    string DefensiveCallId);
