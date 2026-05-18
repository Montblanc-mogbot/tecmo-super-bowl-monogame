using TecmoSBGame.SimArch.Events;

namespace TecmoSBGame.SimArch.State;

/// <summary>
/// Match-level rules state (score, clock, down/distance, ball spot).
///
/// Ported from: ArchiveMge/State/MatchState.cs (scaffold; will be refined for NES parity later)
/// </summary>
public sealed class MatchState
{
    public const int TouchbackSpotYard = 25;

    // Team id mapping
    public int AwayTeamId;
    public int HomeTeamId;

    // Score
    public int Team0Score;
    public int Team1Score;

    // Clock / phase
    public int Quarter;
    public int GameClockSeconds;
    public bool MatchOver;
    public MatchPhase Phase;
    public bool ClockRunning;
    public int DeferredKickReceivingTeam;
    public int DeferredKickKickingTeam;

    // Possession + direction
    public int PossessionTeam;
    public OffenseDirection OffenseDirection;

    // Down & distance
    public int Down;
    public int YardsToGo;
    public bool GoalToGo;

    public BallSpot BallSpot;

    // Drive/play ids
    public int PlayNumber;
    public int DriveId;

    // Kickoff setup
    public int KickingTeamIndex;
    public int ReceivingTeamIndex;
    public KickoffSetupReason? PendingKickoffReason;
    public bool KickoffPending;
    public bool KickoffPlayActive;
    public int? KickoffLandingAbsoluteYardOverride;

    // Punt setup
    public bool PuntPending;
    public bool PuntPlayActive;
    public int? PuntLandingAbsoluteYardOverride;
    public bool ForcePuntMuff;

    // Field goal / PAT setup
    public bool FieldGoalPending;
    public bool FieldGoalPlayActive;
    public bool ExtraPointPending;
    public int? FieldGoalTargetAbsoluteYardOverride;
    public bool ForceFieldGoalBlock;
    public bool ForceFieldGoalMiss;

    public void ResetForKickoff(int kickingTeam, int receivingTeam, KickoffSetupReason? reason = null)
    {
        KickingTeamIndex = kickingTeam;
        ReceivingTeamIndex = receivingTeam;
        PendingKickoffReason = reason;
        KickoffPending = true;
        KickoffPlayActive = false;
        KickoffLandingAbsoluteYardOverride = null;
        PuntPending = false;
        PuntPlayActive = false;
        PuntLandingAbsoluteYardOverride = null;
        ForcePuntMuff = false;
        FieldGoalPending = false;
        FieldGoalPlayActive = false;
        ExtraPointPending = false;
        FieldGoalTargetAbsoluteYardOverride = null;
        ForceFieldGoalBlock = false;
        ForceFieldGoalMiss = false;
    }

    public void AddScore(int teamIndex, int points)
    {
        if (teamIndex == 0) Team0Score += points;
        else Team1Score += points;
    }

    public void AdvanceDownDistance(int yardsGained)
    {
        var firstDown = yardsGained >= YardsToGo;
        AdvanceDownDistance(yardsGained, firstDown);
    }

    public void AdvanceDownDistance(int yardsGained, bool firstDown)
    {
        if (firstDown)
        {
            Down = 1;
            YardsToGo = ComputeYardsToGoal(BallSpot) <= 10 ? ComputeYardsToGoal(BallSpot) : 10;
        }
        else
        {
            Down = System.Math.Clamp(Down + 1, 1, 4);
            YardsToGo = System.Math.Max(1, YardsToGo - yardsGained);
        }

        GoalToGo = ComputeYardsToGoal(BallSpot) <= YardsToGo;
    }

    public void ResetSeries()
    {
        Down = 1;
        YardsToGo = ComputeYardsToGoal(BallSpot) <= 10 ? ComputeYardsToGoal(BallSpot) : 10;
        GoalToGo = ComputeYardsToGoal(BallSpot) <= YardsToGo;
    }

    public void SpotBall(BallSpot spot)
    {
        BallSpot = NormalizeSpot(spot);
        GoalToGo = ComputeYardsToGoal(BallSpot) <= YardsToGo;
    }

    public void SpotBallAbsoluteYard(int absoluteYard)
    {
        BallSpot = NormalizeSpot(FromAbsoluteYard(absoluteYard, OffenseDirection));
        GoalToGo = ComputeYardsToGoal(BallSpot) <= YardsToGo;
    }

    public int GetAbsoluteBallYard()
        => ToAbsoluteYard(BallSpot, OffenseDirection);

    public int ComputeYardsToGoal()
        => ComputeYardsToGoal(BallSpot);

    public static int ComputeYardsToGoal(BallSpot spot)
    {
        var absolute = ToAbsoluteYard(spot, OffenseDirection.LeftToRight);
        return System.Math.Max(0, 100 - absolute);
    }

    public static BallSpot FromAbsoluteYard(int absoluteYard, OffenseDirection direction)
    {
        absoluteYard = System.Math.Clamp(absoluteYard, 0, 100);
        if (direction == OffenseDirection.RightToLeft)
            absoluteYard = 100 - absoluteYard;

        if (absoluteYard < 50)
            return BallSpot.Own(absoluteYard);
        if (absoluteYard == 50)
            return BallSpot.Opp(50);
        return BallSpot.Opp(100 - absoluteYard);
    }

    public static int ToAbsoluteYard(BallSpot spot, OffenseDirection direction)
    {
        var abs = spot.OnOwnSide ? spot.Yards : 100 - spot.Yards;
        return direction == OffenseDirection.LeftToRight ? abs : 100 - abs;
    }

    private static BallSpot NormalizeSpot(BallSpot spot)
    {
        if (spot.OnOwnSide && spot.Yards > 50)
            return BallSpot.Opp(100 - spot.Yards);
        if (!spot.OnOwnSide && spot.Yards > 50)
            return BallSpot.Own(100 - spot.Yards);
        return spot;
    }
}

public enum MatchPhase
{
    FirstQuarter = 1,
    SecondQuarter = 2,
    Halftime = 3,
    ThirdQuarter = 4,
    FourthQuarter = 5,
    Final = 6,
}

public enum OffenseDirection
{
    LeftToRight = 0,
    RightToLeft = 1,
}

/// <summary>
/// Ball spot relative to offense perspective.
/// Own(x) is x yards from own goal line.
/// Opp(x) is x yards from opponent goal line.
/// </summary>
public readonly record struct BallSpot(bool OnOwnSide, int Yards)
{
    public static BallSpot Own(int yards) => new(true, System.Math.Clamp(yards, 0, 50));
    public static BallSpot Opp(int yards) => new(false, System.Math.Clamp(yards, 0, 50));
}
