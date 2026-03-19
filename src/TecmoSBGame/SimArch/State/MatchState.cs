namespace TecmoSBGame.SimArch.State;

/// <summary>
/// Match-level rules state (score, clock, down/distance, ball spot).
///
/// Ported from: ArchiveMge/State/MatchState.cs (scaffold; will be refined for NES parity later)
/// </summary>
public sealed class MatchState
{
    // Team id mapping
    public int AwayTeamId;
    public int HomeTeamId;

    // Score
    public int Team0Score;
    public int Team1Score;

    // Clock
    public int Quarter;
    public int GameClockSeconds;
    public bool MatchOver;

    // Possession + direction
    public int PossessionTeam;
    public OffenseDirection OffenseDirection;

    // Down & distance
    public int Down;
    public int YardsToGo;

    public BallSpot BallSpot;

    // Drive/play ids
    public int PlayNumber;
    public int DriveId;

    // Kickoff setup
    public int KickingTeamIndex;
    public int ReceivingTeamIndex;

    public void ResetForKickoff(int kickingTeam, int receivingTeam)
    {
        KickingTeamIndex = kickingTeam;
        ReceivingTeamIndex = receivingTeam;
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
            YardsToGo = 10;
        }
        else
        {
            Down = System.Math.Clamp(Down + 1, 1, 4);
            YardsToGo = System.Math.Max(1, YardsToGo - yardsGained);
        }
    }

    public void SpotBall(BallSpot spot)
    {
        BallSpot = spot;
    }

    public void SpotBallAbsoluteYard(int absoluteYard)
    {
        // absoluteYard is 0..100 from left endzone to right endzone.
        // Convert into a BallSpot relative to offense direction.
        BallSpot = OffenseDirection == OffenseDirection.LeftToRight
            ? BallSpot.Own(absoluteYard)
            : BallSpot.Opp(100 - absoluteYard);
    }
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
