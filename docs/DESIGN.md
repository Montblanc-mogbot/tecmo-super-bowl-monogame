# Tecmo Super Bowl - MonoGame Design Document

## Overview

This document outlines the complete architecture for reimplementing Tecmo Super Bowl using MonoGame. Instead of faithfully replicating the NES architecture (bank switching, PPU rendering, 6502 assembly patterns), we leverage modern game development patterns to create a cleaner, more maintainable codebase.

## Key Design Principles

1. **Data-Driven Design** - All game data in YAML, runtime state in C# objects
2. **Component-Based Architecture** - Use MonoGame's GameComponent system
3. **Event-Driven Communication** - Decouple systems via events/messages
4. **Modern Rendering** - SpriteBatch, not tile-by-tile PPU simulation
5. **Tecmo-Style Movement** - Velocity without momentum, instant direction changes

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

### On-Field Game Loop

Every frame at 60Hz:

```csharp
public class OnFieldGameState : GameState
{
    public override void Update(GameTime dt)
    {
        // 1. Update all player behaviors
        foreach (var player in AllPlayers)
        {
            player.UpdateBehavior(dt);
        }
        
        // 2. Check collisions (discrete distance checks)
        CollisionSystem.CheckCollisions();
        
        // 3. Update ball (if in air)
        if (Ball.IsInAir)
        {
            Ball.UpdateTrajectory(dt);
        }
        
        // 4. Check play outcomes
        CheckPlayEndConditions();
        
        // 5. Update clock
        GameClock.Update(dt);
    }
}
```

### Movement System (Tecmo-Style)

The original uses velocity without momentum - snappy, responsive controls:

```csharp
public class TecmoMovement
{
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    
    // From player RS (Running Speed) rating
    public float MaxSpeed { get; set; }  // 3.0 to 6.5 pixels/frame
    
    // Acceleration - how quickly reach max speed
    public float Acceleration { get; set; } = 0.4f;
    
    public void Update(Vector2 inputDirection)
    {
        if (inputDirection == Vector2.Zero)
        {
            // INSTANT stop (no momentum)
            Velocity = Vector2.Zero;
            return;
        }
        
        // Snap to input direction, ramp speed
        var targetVelocity = inputDirection * MaxSpeed;
        Velocity = Vector2.Lerp(Velocity, targetVelocity, Acceleration);
        
        Position += Velocity;
    }
}
```

**Key characteristics:**
- **Instant direction changes** - No turning radius, no inertia
- **Speed ramps up** - Quick acceleration to max speed
- **Instant stop** - Release input = immediate stop
- **Max speed capped** by RS rating
- **No momentum** - Players don't carry forward when stopping

### Player Behavior System

Frame-by-frame behavior execution (60Hz), matching the original bytecode VM:

```csharp
public abstract class Behavior
{
    // Run every frame, return what to do next
    public abstract BehaviorResult Update(Player player, GameTime dt);
}

public class RushQBBehavior : Behavior
{
    public override BehaviorResult Update(Player player, GameTime dt)
    {
        // 1. Move toward QB's CURRENT position (every frame)
        var qbPos = GameState.QB.Position;
        var direction = Vector2.Normalize(qbPos - player.Position);
        player.Velocity = direction * player.MaxSpeed;
        player.Position += player.Velocity;
        
        // 2. Check for blocker collision every frame
        var blocker = CheckBlockerInPath(player);
        if (blocker != null)
        {
            // Engage - switch to grapple behavior (push to stack)
            return BehaviorResult.Push(new GrappleBehavior(blocker));
        }
        
        // 3. Check for QB tackle opportunity
        if (DistanceTo(qbPos) < TackleRange)
        {
            return BehaviorResult.SwitchTo(new TackleAttemptBehavior());
        }
        
        // 4. Continue rushing
        return BehaviorResult.Continue;
    }
}

public class ManCoverageBehavior : Behavior
{
    public Player Target { get; set; }
    public float Cushion { get; set; } = 5f;
    
    public override BehaviorResult Update(Player player, GameTime dt)
    {
        // Track target's CURRENT position every frame
        var targetPos = Target.Position;
        var qbPos = GameState.QB.Position;
        
        // Stay between target and QB, with cushion
        var toQB = Vector2.Normalize(qbPos - targetPos);
        var idealPos = targetPos + toQB * Cushion;
        
        var direction = Vector2.Normalize(idealPos - player.Position);
        player.Velocity = direction * player.MaxSpeed;
        player.Position += player.Velocity;
        
        // Check for throw
        if (GameState.Ball.IsThrown && GameState.Ball.Target == Target)
        {
            return BehaviorResult.SwitchTo(new BreakOnBallBehavior());
        }
        
        return BehaviorResult.Continue;
    }
}
```

### Behavior Stack

Grapple attempts interrupt current behavior, then resume:

```csharp
public class Player
{
    public Stack<Behavior> BehaviorStack { get; set; } = new();
    
    public void Update(GameTime dt)
    {
        if (BehaviorStack.Count == 0) return;
        
        var current = BehaviorStack.Peek();
        var result = current.Update(this, dt);
        
        switch (result)
        {
            case BehaviorResult.Complete:
                BehaviorStack.Pop();  // Done
                break;
            case BehaviorResult.Continue:
                // Keep running same behavior
                break;
            case BehaviorResult.Push(Behavior newBehavior):
                BehaviorStack.Push(newBehavior);  // Interrupt (grapple)
                break;
            case BehaviorResult.PopAndContinue:
                BehaviorStack.Pop();  // Resume previous (won grapple)
                break;
        }
    }
}
```

### Collision System (Discrete)

Check collision every frame with simple distance (not continuous):

```csharp
public class CollisionSystem
{
    public void CheckCollisions()
    {
        foreach (var defender in Defense.Players)
        {
            foreach (var offense in Offense.Players)
            {
                // Simple distance check every frame
                var distance = Vector2.Distance(defender.Position, offense.Position);
                if (distance < CollisionRange)  // ~8 pixels
                {
                    ResolveCollision(defender, offense);
                }
            }
        }
    }
}
```

### Grapple/Tackle Resolution

Rating-driven outcomes (HP = Hitting Power, RS = Running Speed):

```csharp
public class GrappleBehavior : Behavior
{
    public Player Opponent { get; set; }
    
    public override BehaviorResult Update(Player player, GameTime dt)
    {
        // Both stop moving during grapple
        player.Velocity = Vector2.Zero;
        Opponent.Velocity = Vector2.Zero;
        
        // Check winner every frame based on HP
        var playerWinChance = 0.5f + (player.HP - Opponent.HP) * 0.004f;
        
        if (Random.NextFloat() < playerWinChance * dt)
        {
            // Win grapple - resume previous behavior
            return BehaviorResult.PopAndContinue;
        }
        
        // Continue fighting
        return BehaviorResult.Continue;
    }
}

public class TackleAttemptBehavior : Behavior
{
    public override BehaviorResult Update(Player player, GameTime dt)
    {
        var ballCarrier = GameState.BallCarrier;
        if (ballCarrier == null) return BehaviorResult.Complete;
        
        // Pursue ball carrier
        var direction = Vector2.Normalize(ballCarrier.Position - player.Position);
        player.Velocity = direction * player.MaxSpeed;
        player.Position += player.Velocity;
        
        // Check collision every frame
        if (IsCollision(player, ballCarrier))
        {
            // Tackle check: HP vs HP + MS (break tackle chance)
            var successChance = 0.7f + (player.HP - ballCarrier.HP) * 0.005f 
                                      - ballCarrier.MS * 0.01f;
            
            if (Random.NextFloat() < successChance)
            {
                // Tackle made
                ballCarrier.State = PlayerState.Tackled;
                ballCarrier.Velocity = Vector2.Zero;
                return BehaviorResult.Complete;
            }
            // Broken tackle - continue pursuit
        }
        
        return BehaviorResult.Continue;
    }
}
```
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
│   │   │   ├── MovementComponent.cs
│   │   │   └── CollisionComponent.cs
│   │   ├── Systems/
│   │   │   ├── MovementSystem.cs
│   │   │   ├── CollisionSystem.cs
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
- **Target**: 60 FPS (matches original NES)
- **Fixed timestep** for gameplay logic

### Movement System
- **Tecmo-style velocity** - No momentum, instant direction changes
- **Discrete collision** - Distance checks every frame (~8px range)
- **Behavior stack** - Push/pop for grapple interrupts
- **Rating-driven** - HP, RS, MS determine outcomes

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

This design preserves Tecmo Super Bowl's classic gameplay feel while using modern C# patterns.

### Key Simplifications from NES
1. **No bank switching** - Load all data at startup
2. **No PPU simulation** - Standard SpriteBatch rendering
3. **No bytecode VM** - C# behavior classes
4. **No assembly macros** - C# methods and properties

### Preserved from Original
1. **Tecmo-style movement** - Velocity without momentum, instant direction changes
2. **Discrete collision** - Frame-by-frame distance checks
3. **Behavior stack** - Push/pop for grapple interrupts
4. **Rating-driven outcomes** - HP, RS, MS determine success
5. **Frame-by-frame execution** - 60Hz behavior updates

The YAML data files provide the foundation - all teams, plays, formations, and constants are ready to load. The C# implementation focuses on executing behaviors exactly as the original did.
