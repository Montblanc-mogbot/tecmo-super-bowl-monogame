using Microsoft.Xna.Framework;

namespace TecmoSBGame.State;

/// <summary>
/// Comprehensive game state manager that handles the full football game flow.
/// Tracks score, downs, field position, game clock, and game phases.
/// </summary>
public class GameStateManager
{
    // Game configuration
    public const int QUARTER_LENGTH = 300; // 5 minutes per quarter (in seconds)
    public const int PLAY_CLOCK = 25; // 25 second play clock
    public const int YARDS_FOR_FIRST_DOWN = 10;
    
    // Game state
    public GamePhase CurrentPhase { get; private set; } = GamePhase.CoinToss;
    public int Quarter { get; private set; } = 1;
    public float GameClock { get; private set; } = QUARTER_LENGTH;
    public float PlayClock { get; private set; } = PLAY_CLOCK;
    public bool GameClockRunning { get; private set; } = false;
    
    // Down and distance
    public int Down { get; private set; } = 1;
    public int YardsToGo { get; private set; } = YARDS_FOR_FIRST_DOWN;
    public int FieldPosition { get; private set; } = 25; // Yard line (0-100, 0=own goal line, 50=midfield)
    public bool OffenseFacingRight { get; private set; } = true;
    
    // Team state
    public int OffenseTeam { get; private set; } = 0;
    public int DefenseTeam { get; private set; } = 1;
    public int TeamWithBall { get; private set; } = 0;
    
    // Scoring
    public int HomeScore { get; private set; } = 0;
    public int AwayScore { get; private set; } = 0;
    
    // Drive stats
    public int DrivePlays { get; private set; } = 0;
    public int DriveYards { get; private set; } = 0;
    public float DriveTime { get; private set; } = 0f;
    
    // Possession stats
    public float HomePossessionTime { get; private set; } = 0f;
    public float AwayPossessionTime { get; private set; } = 0f;
    
    // Game events
    public event Action? OnScoreChanged;
    public event Action? OnDownChanged;
    public event Action? OnPossessionChanged;
    public event Action? OnQuarterChanged;
    public event Action? OnGameOver;
    public event Action? OnTouchdown;
    public event Action? OnSafety;
    public event Action? OnTurnover;
    
    public void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        if (GameClockRunning)
        {
            // Update game clock
            GameClock -= dt;
            if (GameClock <= 0)
            {
                GameClock = 0;
                EndQuarter();
            }
            
            // Update possession time
            if (TeamWithBall == 0)
                HomePossessionTime += dt;
            else
                AwayPossessionTime += dt;
            
            // Update drive time
            DriveTime += dt;
        }
        
        // Update play clock during pre-snap
        if (CurrentPhase == GamePhase.PreSnap)
        {
            PlayClock -= dt;
            if (PlayClock <= 0)
            {
                DelayOfGame();
            }
        }
    }
    
    // Phase management
    
    public void StartGame(int homeTeam, int awayTeam)
    {
        // Coin toss phase
        CurrentPhase = GamePhase.CoinToss;
    }
    
    public void SetKickoff(int kickingTeam, int receivingTeam)
    {
        OffenseTeam = receivingTeam; // Receiving team starts on offense
        DefenseTeam = kickingTeam;
        TeamWithBall = receivingTeam;
        OffenseFacingRight = receivingTeam == 0; // Team 0 faces right
        
        FieldPosition = 25; // Kickoff return starts at 25 (touchback spot)
        ResetDownAndDistance();
        
        CurrentPhase = GamePhase.Kickoff;
    }
    
    public void StartDrive(int offenseTeam, int startPosition)
    {
        OffenseTeam = offenseTeam;
        DefenseTeam = offenseTeam == 0 ? 1 : 0;
        TeamWithBall = offenseTeam;
        FieldPosition = startPosition;
        OffenseFacingRight = offenseTeam == 0;
        
        ResetDownAndDistance();
        ResetDriveStats();
        
        CurrentPhase = GamePhase.PreSnap;
        OnPossessionChanged?.Invoke();
    }
    
    public void SnapBall()
    {
        CurrentPhase = GamePhase.InPlay;
        GameClockRunning = true;
        DrivePlays++;
    }
    
    public void EndPlay(int yardsGained, bool firstDown = false, bool touchdown = false, bool turnover = false)
    {
        GameClockRunning = false;
        DriveYards += yardsGained;
        
        // Update field position
        if (OffenseFacingRight)
        {
            FieldPosition += yardsGained;
        }
        else
        {
            FieldPosition -= yardsGained;
        }
        
        // Check for touchdown
        if (FieldPosition >= 100)
        {
            Touchdown();
            return;
        }
        
        // Check for safety
        if (FieldPosition <= 0)
        {
            Safety();
            return;
        }
        
        if (firstDown || touchdown)
        {
            ResetDownAndDistance();
        }
        else if (turnover)
        {
            TurnoverOnDowns();
            return;
        }
        else
        {
            Down++;
            YardsToGo -= yardsGained;
            
            if (Down > 4)
            {
                TurnoverOnDowns();
                return;
            }
        }
        
        CurrentPhase = GamePhase.PostPlay;
        OnDownChanged?.Invoke();
        
        // Reset for next play
        ResetPlayClock();
    }
    
    // Scoring
    
    public void Touchdown()
    {
        AddScore(TeamWithBall, 6);
        OnTouchdown?.Invoke();
        CurrentPhase = GamePhase.PAT; // Point after touchdown
    }
    
    public void FieldGoal()
    {
        AddScore(TeamWithBall, 3);
        StartDrive(DefenseTeam, 25); // Other team gets ball at 25
    }
    
    public void Safety()
    {
        AddScore(DefenseTeam, 2);
        OnSafety?.Invoke();
        // Free kick by scoring team
        SetKickoff(DefenseTeam, OffenseTeam);
    }
    
    public void ExtraPoint(bool good)
    {
        if (good)
            AddScore(TeamWithBall, 1);
        
        StartDrive(DefenseTeam, 25); // Kickoff to other team
    }
    
    public void TwoPointConversion(bool good)
    {
        if (good)
            AddScore(TeamWithBall, 2);
        
        StartDrive(DefenseTeam, 25);
    }
    
    private void AddScore(int team, int points)
    {
        if (team == 0)
            HomeScore += points;
        else
            AwayScore += points;
        
        OnScoreChanged?.Invoke();
    }
    
    // Turnovers
    
    public void TurnoverOnDowns()
    {
        OnTurnover?.Invoke();
        StartDrive(DefenseTeam, 100 - FieldPosition); // Flip field position
    }
    
    public void Interception(int returnYards)
    {
        OnTurnover?.Invoke();
        var newPosition = 100 - FieldPosition + returnYards;
        StartDrive(DefenseTeam, Math.Clamp(newPosition, 0, 100));
    }
    
    public void Fumble(bool recoveredByDefense, int returnYards = 0)
    {
        if (recoveredByDefense)
        {
            OnTurnover?.Invoke();
            var newPosition = 100 - FieldPosition + returnYards;
            StartDrive(DefenseTeam, Math.Clamp(newPosition, 0, 100));
        }
        // If recovered by offense, play continues
    }
    
    // Penalties
    
    public void DelayOfGame()
    {
        // 5 yard penalty, replay down
        if (OffenseFacingRight)
            FieldPosition -= 5;
        else
            FieldPosition += 5;
        
        ResetPlayClock();
    }
    
    // Quarter management
    
    private void EndQuarter()
    {
        if (Quarter < 4)
        {
            Quarter++;
            GameClock = QUARTER_LENGTH;
            
            // Switch offense at halftime (end of Q2)
            if (Quarter == 3)
            {
                SwitchPossession();
            }
            
            OnQuarterChanged?.Invoke();
        }
        else
        {
            EndGame();
        }
    }
    
    private void EndGame()
    {
        CurrentPhase = GamePhase.GameOver;
        OnGameOver?.Invoke();
    }
    
    // Helper methods
    
    private void ResetDownAndDistance()
    {
        Down = 1;
        YardsToGo = YARDS_FOR_FIRST_DOWN;
    }
    
    private void ResetPlayClock()
    {
        PlayClock = PLAY_CLOCK;
    }
    
    private void ResetDriveStats()
    {
        DrivePlays = 0;
        DriveYards = 0;
        DriveTime = 0f;
    }
    
    private void SwitchPossession()
    {
        var temp = OffenseTeam;
        OffenseTeam = DefenseTeam;
        DefenseTeam = temp;
        TeamWithBall = OffenseTeam;
        OffenseFacingRight = !OffenseFacingRight;
    }
    
    // Query methods
    
    public int GetScore(int team)
    {
        return team == 0 ? HomeScore : AwayScore;
    }
    
    public bool IsRedZone()
    {
        // Red zone is inside opponent's 20 yard line
        if (OffenseFacingRight)
            return FieldPosition >= 80;
        else
            return FieldPosition <= 20;
    }
    
    public int GetYardsToEndzone()
    {
        if (OffenseFacingRight)
            return 100 - FieldPosition;
        else
            return FieldPosition;
    }
    
    public string GetDownAndDistance()
    {
        string downText = Down switch
        {
            1 => "1st",
            2 => "2nd",
            3 => "3rd",
            4 => "4th",
            _ => $"{Down}th"
        };
        
        string distanceText = YardsToGo <= 0 ? "Goal" : $"{YardsToGo}";
        return $"{downText} & {distanceText}";
    }
    
    public string GetFieldPositionString()
    {
        if (FieldPosition == 50)
            return "Midfield";
        
        if (FieldPosition > 50)
        {
            int yards = 100 - FieldPosition;
            return $"Opp {yards}";
        }
        else
        {
            return $"Own {FieldPosition}";
        }
    }
    
    public string GetGameClockString()
    {
        int minutes = (int)(GameClock / 60);
        int seconds = (int)(GameClock % 60);
        return $"{minutes}:{seconds:D2}";
    }
}

/// <summary>
/// Game phases for state management.
/// </summary>
public enum GamePhase
{
    CoinToss,
    Kickoff,
    PreSnap,
    InPlay,
    PostPlay,
    PAT, // Point after touchdown
    TwoPointConversion,
    QuarterEnd,
    Halftime,
    GameOver
}
