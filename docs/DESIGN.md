# Tecmo Super Bowl - MonoGame Design Document

## Overview

This document outlines the complete architecture for reimplementing Tecmo Super Bowl using MonoGame. Instead of faithfully replicating the NES architecture (bank switching, PPU rendering, 6502 assembly patterns), we leverage modern game development patterns to create a cleaner, more maintainable codebase.

## Key Design Principles

1. **Data-Driven Design** - All game data in YAML, runtime state in C# objects
2. **Component-Based Architecture** - Use MonoGame's GameComponent system
3. **Event-Driven Communication** - Decouple systems via events/messages
4. **Modern Rendering** - SpriteBatch, not tile-by-tile PPU simulation
5. **Physics-Based Movement** - Real physics, not pre-calculated tables

---

## High-Level Architecture

### Core Systems

```
┌─────────────────────────────────────────────────────────────────┐
│                        Game1 (Main Game)                         │
├─────────────────────────────────────────────────────────────────┤
│  StateMachine                                                  │
│  ├── MenuState                                                 │
│  ├── SeasonModeState                                           │
│  ├── ExhibitionGameState                                       │
│  └── OnFieldGameState                                          │
├─────────────────────────────────────────────────────────────────┤
│  ContentManager                                                │
│  ├── YamlContentLoader                                         │
│  ├── TextureContent                                            │
│  ├── AudioContent                                              │
│  └── FontContent                                               │
├─────────────────────────────────────────────────────────────────┤
│  Services                                                      │
│  ├── GameplayService                                           │
│  ├── SimulationService                                         │
│  ├── AudioService                                              │
│  ├── InputService                                              │
│  └── UIService                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Game States

The game uses a state machine pattern with these primary states:

| State | Description |
|-------|-------------|
| `BootState` | Initialize systems, load content |
| `TitleState` | Title screen, attract mode |
| `MainMenuState` | Game mode selection (Exhibition, Season, Pro Bowl) |
| `TeamSelectState` | Team selection, coin toss |
| `PlayCallState` | Offensive/defensive play selection |
| `OnFieldState` | Live gameplay |
| `PostPlayState` | Stats, replay, next play setup |
| `SeasonMenuState` | Season mode menus (schedule, standings) |
| `SimState` | CPU vs CPU simulation |

---

## Data Architecture

### YAML Content Pipeline

All game data is stored in YAML files and loaded at runtime:

```csharp
// Content loading pattern
public class ContentManager
{
    public T Load<T>(string path) where T : class
    {
        var yaml = File.ReadAllText(path);
        return YamlDeserializer.Deserialize<T>(yaml);
    }
}

// Usage
var teams = Content.Load<TeamDataConfig>("content/teamtext/bank16_team_text_data.yaml");
var plays = Content.Load<PlayListConfig>("content/playcall/playlist.yaml");
```

### Data Categories

| Category | Files | Purpose |
|----------|-------|---------|
| **Teams** | `teamtext/*.yaml` | 28 NFL teams, rosters, colors |
| **Plays** | `playcall/*.yaml`, `playdata/*.yaml` | Offensive/defensive plays |
| **Formations** | `formations/*.yaml` | Player positioning |
| **Game Systems** | `gameloop/*.yaml`, `onfieldloop/*.yaml` | State machines |
| **Audio** | `sound/*.yaml`, `sounddata/*.yaml` | Music and SFX |
| **UI** | `menuscripts/*.yaml` | Menu layouts |
| **Constants** | `constants/*.yaml` | Game constants |

---

## On-Field Gameplay System

### Entity Component System

Players, ball, and field entities use a component-based architecture:

```csharp
public class Player : Entity
{
    public PositionComponent Position { get; set; }
    public VelocityComponent Velocity { get; set; }
    public SpriteComponent Sprite { get; set; }
    public PlayerAttributesComponent Attributes { get; set; }
    public BehaviorComponent Behavior { get; set; }
    public CollisionComponent Collision { get; set; }
}

public class Ball : Entity
{
    public PositionComponent Position { get; set; }
    public VelocityComponent Velocity { get; set; }
    public PhysicsComponent Physics { get; set; }
    public TrailComponent Trail { get; set; }
}
```

### Player Behavior System

Instead of bytecode scripts, use behavior classes:

```csharp
public abstract class PlayerBehavior
{
    public abstract void Update(Player player, GameTime gameTime);
}

public class RouteBehavior : PlayerBehavior
{
    public List<Vector2> Waypoints { get; set; }
    public float Speed { get; set; }
    
    public override void Update(Player player, GameTime gameTime)
    {
        // Move along route
    }
}

public class BlockBehavior : PlayerBehavior
{
    public Player Target { get; set; }
    
    public override void Update(Player player, GameTime gameTime)
    {
        // Engage blocker
    }
}

public class RushBehavior : PlayerBehavior
{
    public Vector2 TargetPosition { get; set; }
    
    public override void Update(Player player, GameTime gameTime)
    {
        // Rush QB
    }
}
```

### Play Execution

Plays are data-driven behavior assignments:

```csharp
public class Play
{
    public string Name { get; set; }
    public Formation Formation { get; set; }
    public Dictionary<string, PlayerBehavior> PlayerBehaviors { get; set; }
    
    public void AssignToTeam(Team team)
    {
        foreach (var player in team.Players)
        {
            if (PlayerBehaviors.TryGetValue(player.Position, out var behavior))
            {
                player.Behavior = behavior;
            }
        }
    }
}
```

### Physics System

Modern physics instead of lookup tables:

```csharp
public class PhysicsSystem
{
    public void Update(GameTime gameTime)
    {
        foreach (var entity in _physicsEntities)
        {
            // Apply velocity
            entity.Position += entity.Velocity * gameTime.ElapsedGameTime;
            
            // Apply friction
            entity.Velocity *= 0.98f;
            
            // Boundary collision
            CheckFieldBoundaries(entity);
        }
    }
}
```

---

## Rendering System

### No More PPU Simulation

Instead of simulating NES PPU (tile rendering, nametables, sprites):

```csharp
public class RenderingSystem
{
    private SpriteBatch _spriteBatch;
    private Camera _camera;
    
    public void Draw(GameTime gameTime)
    {
        _spriteBatch.Begin(transformMatrix: _camera.ViewMatrix);
        
        // Draw field (single texture or tiled)
        DrawField();
        
        // Draw entities (players sorted by Y for depth)
        foreach (var entity in _entities.OrderBy(e => e.Position.Y))
        {
            DrawEntity(entity);
        }
        
        // Draw UI overlay
        DrawUI();
        
        _spriteBatch.End();
    }
}
```

### Field Rendering

```csharp
public class FieldRenderer
{
    private Texture2D _fieldTexture;
    private Texture2D _yardlineTexture;
    
    public void Draw(SpriteBatch spriteBatch)
    {
        // Draw base field
        spriteBatch.Draw(_fieldTexture, _fieldBounds, Color.White);
        
        // Draw yard lines (procedurally or from texture)
        for (int yard = 0; yard <= 100; yard += 10)
        {
            var position = YardToScreen(yard);
            spriteBatch.Draw(_yardlineTexture, position, Color.White);
            
            // Draw numbers
            DrawYardNumber(spriteBatch, yard, position);
        }
    }
}
```

### Player Rendering

```csharp
public class PlayerRenderer
{
    public void Draw(SpriteBatch spriteBatch, Player player)
    {
        // Get sprite based on animation state
        var sprite = GetSprite(player);
        
        // Draw with team colors
        spriteBatch.Draw(
            sprite.Texture,
            player.Position,
            sprite.SourceRectangle,
            player.Team.PrimaryColor,
            player.Rotation,
            sprite.Origin,
            1.0f,
            player.Facing == Facing.Left ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
            0);
    }
}
```

---

## Input System

### Modern Input Handling

```csharp
public class InputService
{
    private GamePadState _gamePadState;
    private KeyboardState _keyboardState;
    
    public InputState GetPlayerInput(int playerIndex)
    {
        return new InputState
        {
            Direction = GetDirection(playerIndex),
            A = WasButtonPressed(playerIndex, Buttons.A),
            B = WasButtonPressed(playerIndex, Buttons.B),
            Start = WasButtonPressed(playerIndex, Buttons.Start),
            Select = WasButtonPressed(playerIndex, Buttons.Select)
        };
    }
}
```

---

## Audio System

### Simplified Audio

Instead of NES APU emulation:

```csharp
public class AudioService
{
    private SoundEffect _kickSound;
    private SoundEffect _tackleSound;
    private Song _bgm;
    
    public void PlaySound(string soundId)
    {
        var sound = Content.Load<SoundEffect>($"audio/sfx/{soundId}");
        sound.Play();
    }
    
    public void PlayMusic(string musicId)
    {
        MediaPlayer.Play(Content.Load<Song>($"audio/music/{musicId}"));
    }
}
```

---

## Season/Simulation Mode

### Pure C# Implementation

```csharp
public class SeasonMode
{
    public List<Team> Teams { get; set; }
    public List<WeekSchedule> Schedule { get; set; }
    public Standings Standings { get; set; }
    
    public void SimulateWeek(int weekNumber)
    {
        foreach (var game in Schedule[weekNumber].Games)
        {
            var result = SimulationService.Simulate(game.Home, game.Away);
            UpdateStandings(result);
        }
    }
}

public class SimulationService
{
    public GameResult Simulate(Team home, Team away)
    {
        // Use team ratings and randomness
        var homeScore = CalculateScore(home.OffenseRating, away.DefenseRating);
        var awayScore = CalculateScore(away.OffenseRating, home.DefenseRating);
        
        return new GameResult { HomeScore = homeScore, AwayScore = awayScore };
    }
}
```

---

## UI System

### Modern UI Framework

```csharp
public class UIService
{
    private List<UIElement> _elements;
    
    public void ShowPlayCallMenu(Team offense, Team defense)
    {
        var menu = new PlayCallMenu();
        menu.Initialize(offense.Playbook, defense.Playbook);
        _elements.Add(menu);
    }
    
    public void Draw(SpriteBatch spriteBatch)
    {
        foreach (var element in _elements)
        {
            element.Draw(spriteBatch);
        }
    }
}
```

---

## Project Structure

```
TecmoSuperBowl/
├── src/
│   ├── TecmoSB/
│   │   ├── Core/
│   │   │   ├── Game1.cs
│   │   │   ├── StateMachine.cs
│   │   │   └── ContentManager.cs
│   │   ├── Entities/
│   │   │   ├── Entity.cs
│   │   │   ├── Player.cs
│   │   │   ├── Ball.cs
│   │   │   └── Field.cs
│   │   ├── Components/
│   │   │   ├── PositionComponent.cs
│   │   │   ├── SpriteComponent.cs
│   │   │   ├── BehaviorComponent.cs
│   │   │   └── PhysicsComponent.cs
│   │   ├── Systems/
│   │   │   ├── PhysicsSystem.cs
│   │   │   ├── RenderingSystem.cs
│   │   │   ├── InputSystem.cs
│   │   │   └── AudioSystem.cs
│   │   ├── Gameplay/
│   │   │   ├── Play.cs
│   │   │   ├── Formation.cs
│   │   │   ├── GameState.cs
│   │   │   └── Matchup.cs
│   │   ├── UI/
│   │   │   ├── UIService.cs
│   │   │   ├── Menu.cs
│   │   │   └── HUD.cs
│   │   ├── Season/
│   │   │   ├── SeasonMode.cs
│   │   │   ├── SimulationService.cs
│   │   │   └── Standings.cs
│   │   ├── Data/
│   │   │   └── YamlContentLoader.cs
│   │   └── Program.cs
│   └── TecmoSB.Tests/
├── content/
│   └── (all YAML data files)
├── assets/
│   ├── sprites/
│   ├── audio/
│   └── fonts/
└── TecmoSuperBowl.csproj
```

---

## Technical Specifications

### Resolution
- **Base**: 256x224 (authentic NES aspect ratio)
- **Scaled**: 1280x720 (16:9 with pillarboxing) or fullscreen

### Frame Rate
- **Target**: 60 FPS
- **Fixed timestep** for gameplay logic

### Content Pipeline
- **Sprites**: PNG with transparency
- **Audio**: WAV for SFX, MP3/OGG for music
- **Fonts**: TrueType fonts
- **Data**: YAML files (loaded at runtime)

### Dependencies
- MonoGame 3.8+
- YamlDotNet
- (Optional) ImGui.NET for debug UI

---

## Development Phases

### Phase 1: Core Framework
- [ ] Project setup
- [ ] Content pipeline (YAML loading)
- [ ] Basic entity system
- [ ] Rendering pipeline

### Phase 2: On-Field Gameplay
- [ ] Field rendering
- [ ] Player movement
- [ ] Play execution
- [ ] Collision detection
- [ ] Scoring

### Phase 3: Game Flow
- [ ] Play calling
- [ ] Coin toss/kickoff
- [ ] Drive management
- [ ] Stats tracking

### Phase 4: Game Modes
- [ ] Exhibition
- [ ] Season mode
- [ ] Simulation
- [ ] Save/load

### Phase 5: Polish
- [ ] Audio
- [ ] Visual effects
- [ ] UI polish
- [ ] Bug fixes

---

## Conclusion

This design leverages MonoGame's modern capabilities to create a much cleaner implementation than the NES original. Key simplifications:

1. **No bank switching** - Load all data at startup
2. **No PPU simulation** - Standard SpriteBatch rendering
3. **No bytecode VM** - C# behavior classes
4. **No fixed-point math** - Floating-point physics
5. **No assembly macros** - C# methods and properties

The YAML data files already created provide the foundation. The C# implementation focuses on modern game architecture patterns.
