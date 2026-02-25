# Save Data & Season Simulation Design

This document outlines the save data system and season simulation for the Tecmo Super Bowl MonoGame remake.

## Save Data System

### Overview

Support for multiple save slots, automatic saves, and cross-platform compatibility using JSON.

### Save Data Types

1. **Game Settings** - Global preferences (not slot-specific)
2. **Season Save** - Full season state (multiple slots)
3. **Quick Save** - Mid-game resume point
4. **High Scores** - Records and achievements

### Save File Structure

```
saves/
├── settings.json           # Global settings
├── highscores.json         # Records/achievements
├── season_01.json          # Save slot 1
├── season_02.json          # Save slot 2
├── season_03.json          # Save slot 3
└── quicksave.json          # Mid-game save
```

### Season Save Data Schema

```json
{
  "version": 1,
  "created": "2026-02-24T19:30:00Z",
  "modified": "2026-02-24T20:15:00Z",
  "metadata": {
    "slotNumber": 1,
    "saveName": "My Season",
    "seasonYear": 1991,
    "weekNumber": 5,
    "gamesPlayed": 20,
    "userTeam": "buf"
  },
  "teams": [
    {
      "id": "buf",
      "wins": 3,
      "losses": 2,
      "ties": 0,
      "pointsFor": 128,
      "pointsAgainst": 95,
      "players": [
        {
          "id": "BUF_00",
          "name": "Jim Kelly",
          "position": "QB",
          "stats": {
            "passingYards": 1247,
            "passingTDs": 8,
            "interceptions": 3
          },
          "condition": 95,
          "injured": false
        }
      ]
    }
  ],
  "schedule": {
    "weeks": [
      {
        "weekNumber": 1,
        "games": [
          {
            "homeTeam": "buf",
            "awayTeam": "mia",
            "played": true,
            "homeScore": 21,
            "awayScore": 14
          }
        ]
      }
    ]
  },
  "standings": {
    "afcEast": ["buf", "mia", "nyj", "ne"],
    "playoffPicture": {
      "divisionLeaders": ["buf", "pit", "kan", "den"],
      "wildCards": ["mia", "hou"]
    }
  },
  "records": {
    "singleGame": {
      "passingYards": { "value": 450, "player": "Jim Kelly", "week": 3 }
    }
  }
}
```

### Save Manager

```csharp
public class SaveManager
{
    public static SaveManager Instance { get; } = new();
    
    // Settings
    public GameSettings LoadSettings();
    public void SaveSettings(GameSettings settings);
    
    // Season saves
    public SeasonSave LoadSeason(int slot);
    public void SaveSeason(SeasonSave save, int slot);
    public void DeleteSeason(int slot);
    public bool HasSave(int slot);
    
    // Quick save
    public void QuickSave(GameState state);
    public GameState LoadQuickSave();
    public bool HasQuickSave();
    
    // High scores
    public HighScoreTable LoadHighScores();
    public void SaveHighScore(GameResult result);
}
```

## Season Simulation

### Overview

Full 16-game season with playoffs, tracking stats and standings.

### Season Structure

1. **Preseason** - Optional exhibition games
2. **Regular Season** - 16 games over 17 weeks
3. **Playoffs** - Wild card, divisional, conference, Super Bowl
4. **Pro Bowl** - All-star game

### Schedule Generation

```csharp
public class ScheduleGenerator
{
    public SeasonSchedule Generate(int year, List<Team> teams)
    {
        // Division games (6): play each division rival twice
        // Conference games (4): play 4 teams from other divisions
        // Inter-conference (4): play entire division from other conference
        // Rotating (2): remaining based on previous year's standing
    }
}
```

### Simulation Modes

1. **Play All** - User plays every game
2. **Coach Mode** - Call plays, AI executes
3. **Simulate** - AI plays entire game instantly
4. **Mixed** - User chooses which games to play

### Stats Tracking

**Player Stats:**
- Passing: completions, attempts, yards, TDs, INTs
- Rushing: carries, yards, TDs, fumbles
- Receiving: catches, yards, TDs, drops
- Defense: tackles, sacks, INTs, forced fumbles
- Special Teams: returns, FG%, punting average

**Team Stats:**
- Points for/against
- Total yards (offense/defense)
- Turnover differential
- Third down conversion
- Red zone efficiency

### Standings Calculation

```csharp
public class StandingsCalculator
{
    public void UpdateStandings(SeasonSave save)
    {
        // Sort by: win%, division record, conference record,
        // common opponents, strength of victory, strength of schedule
    }
    
    public List<Team> GetPlayoffTeams(SeasonSave save)
    {
        // 4 division winners + 2 wild cards per conference
    }
}
```

### Pro Bowl Selection

- Top players at each position based on stats
- User can override selections
- Injury replacements

## Persistence

### Cross-Platform Save Location

```csharp
public static string GetSaveDirectory()
{
    // Windows: %APPDATA%/TecmoSB/saves/
    // Linux: ~/.local/share/TecmoSB/saves/
    // macOS: ~/Library/Application Support/TecmoSB/saves/
    
    return Environment.GetFolderPath(
        Environment.SpecialFolder.ApplicationData) 
        + "/TecmoSB/saves/";
}
```

### Auto-Save Strategy

- After every completed game
- After significant events (trade, injury)
- On graceful exit
- Optional: mid-game checkpoint every 5 minutes

### Import/Export

Support sharing seasons:
```csharp
public void ExportSeason(int slot, string filePath);
public void ImportSeason(string filePath, int slot);
```

## Implementation Phases

### Phase 1: Basic Saves
- [ ] SaveManager implementation
- [ ] Settings persistence
- [ ] Single season save slot

### Phase 2: Full Season
- [ ] 16-game schedule generation
- [ ] Stats tracking
- [ ] Standings calculation

### Phase 3: Playoffs
- [ ] Playoff bracket logic
- [ ] Pro Bowl selection
- [ ] Championship tracking

### Phase 4: Advanced
- [ ] Multiple save slots
- [ ] Quick save/load
- [ ] Import/export

## Open Questions

- Should we support the original NES password system for nostalgia?
- Cloud save sync (Steam, etc.)?
- Historical season data (real 1991 stats)?
- Multi-season franchise mode with player aging/draft?
