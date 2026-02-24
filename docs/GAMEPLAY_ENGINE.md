# Tecmo Super Bowl - Gameplay Engine Design

## Core Philosophy

Preserve the original NES gameplay feel by maintaining the same behavioral model:
- **Scripted movement** (not physics-based)
- **Discrete collision checks** (not continuous collision)
- **Rating-driven outcomes** (HP, RS, MS determine success)
- **Frame-by-frame execution** (60Hz behavior updates)

---

## Movement System

### Velocity Model (Tecmo-Style)

```csharp
public class TecmoMovement
{
    // Position and velocity (no momentum/inertia)
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    
    // From player ratings (RS = Running Speed)
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

### Behavior Execution

```csharp
public abstract class Behavior
{
    // Run every frame
    public abstract BehaviorResult Update(Player player, GameTime dt);
}

public class RushQBBehavior : Behavior
{
    public override BehaviorResult Update(Player player, GameTime dt)
    {
        // 1. Move toward QB (every frame)
        var qbPos = GameState.BallCarrier?.Position ?? GameState.QB.Position;
        var direction = Vector2.Normalize(qbPos - player.Position);
        player.Velocity = direction * player.MaxSpeed;
        player.Position += player.Velocity;
        
        // 2. Check for blocker collision
        var blocker = CheckBlockerInPath(player);
        if (blocker != null)
        {
            // Engage - switch to grapple behavior
            return BehaviorResult.SwitchTo(new GrappleBehavior(blocker));
        }
        
        // 3. Check for QB tackle opportunity
        if (DistanceTo(qbPos) < TackleRange)
        {
            return BehaviorResult.SwitchTo(new TackleAttemptBehavior());
        }
        
        // 4. Check if QB threw ball
        if (GameState.Ball.IsInAir)
        {
            // Continue rushing or abort based on distance
            if (DistanceTo(qbPos) > 30)
                return BehaviorResult.SwitchTo(new ReturnToZoneBehavior());
        }
        
        return BehaviorResult.Continue;
    }
}

public class ManCoverageBehavior : Behavior
{
    public Player Target { get; set; }
    public float Cushion { get; set; } = 5f;  // Yards
    
    public override BehaviorResult Update(Player player, GameTime dt)
    {
        // 1. Track target's CURRENT position (every frame)
        var targetPos = Target.Position;
        var qbPos = GameState.QB.Position;
        
        // 2. Calculate ideal position (between target and QB, with cushion)
        var toQB = Vector2.Normalize(qbPos - targetPos);
        var idealPos = targetPos + toQB * Cushion;
        
        // 3. Move toward ideal position
        var direction = Vector2.Normalize(idealPos - player.Position);
        player.Velocity = direction * player.MaxSpeed;
        player.Position += player.Velocity;
        
        // 4. Check for throw
        if (GameState.Ball.IsThrown && GameState.Ball.Target == Target)
        {
            // Ball in air - break!
            return BehaviorResult.SwitchTo(new BreakOnBallBehavior());
        }
        
        return BehaviorResult.Continue;
    }
}
```

---

## Collision System

### Discrete Collision Checking

Original Tecmo checks collision **every frame** at specific points:

```csharp
public class CollisionSystem
{
    public void CheckCollisions()
    {
        foreach (var defender in Defense.Players)
        {
            // Check against all offensive players
            foreach (var offense in Offense.Players)
            {
                if (IsCollision(defender, offense))
                {
                    ResolveCollision(defender, offense);
                }
            }
        }
    }
    
    bool IsCollision(Player p1, Player p2)
    {
        // Simple distance check (not continuous collision detection)
        var distance = Vector2.Distance(p1.Position, p2.Position);
        return distance < CollisionRange;  // ~8 pixels
    }
}
```

### Collision Types

#### 1. Pass Rush vs Blocker (Grapple)

```csharp
public class GrappleBehavior : Behavior
{
    public Player Opponent { get; set; }
    public float GrappleTimer { get; set; }
    
    public override BehaviorResult Update(Player player, GameTime dt)
    {
        // Both players stop moving
        player.Velocity = Vector2.Zero;
        Opponent.Velocity = Vector2.Zero;
        
        // Grapple fight! Check every frame
        GrappleTimer += dt;
        
        // Winner determined by HP (Hitting Power) ratings
        var playerWinChance = CalculateWinChance(player.HP, Opponent.HP);
        
        if (Random.NextFloat() < playerWinChance * dt)
        {
            // Player wins grapple
            if (player.IsDefense)
            {
                // Shed block, continue rush
                return BehaviorResult.PopAndContinue;
            }
            else
            {
                // Blocker holds, rusher stuck
                return BehaviorResult.Continue;
            }
        }
        
        if (GrappleTimer > MaxGrappleTime)
        {
            // Timeout - break grapple
            return BehaviorResult.PopAndContinue;
        }
        
        return BehaviorResult.Continue;
    }
    
    float CalculateWinChance(int hp1, int hp2)
    {
        // Higher HP = better chance to win
        // 50 HP vs 50 HP = 50% win chance
        // 75 HP vs 50 HP = ~60% win chance
        return 0.5f + (hp1 - hp2) * 0.004f;
    }
}
```

#### 2. Ball Carrier vs Defender (Tackle)

```csharp
public class TackleAttemptBehavior : Behavior
{
    public override BehaviorResult Update(Player player, GameTime dt)
    {
        var ballCarrier = GameState.BallCarrier;
        
        if (ballCarrier == null)
            return BehaviorResult.Complete;
        
        // Check collision every frame
        if (IsCollision(player, ballCarrier))
        {
            // Tackle check!
            var success = AttemptTackle(player, ballCarrier);
            
            if (success)
            {
                // Tackle made
                ballCarrier.State = PlayerState.Tackled;
                ballCarrier.Velocity = Vector2.Zero;
                GameState.PlayResult = PlayResult.Tackle;
                return BehaviorResult.Complete;
            }
            else
            {
                // Broken tackle - continue pursuit
                // Ball carrier might get speed boost (MS check)
                return BehaviorResult.Continue;
            }
        }
        
        // Continue pursuit
        var direction = Vector2.Normalize(ballCarrier.Position - player.Position);
        player.Velocity = direction * player.MaxSpeed;
        player.Position += player.Velocity;
        
        return BehaviorResult.Continue;
    }
    
    bool AttemptTackle(Player defender, Player ballCarrier)
    {
        // Tackle success based on:
        // - Defender HP vs Ball Carrier HP
        // - Angle of attack
        // - Ball carrier's current action
        
        var baseChance = 0.7f;
        var hpAdvantage = (defender.HP - ballCarrier.HP) * 0.005f;
        
        // Break tackle chance (ball carrier MS = Maximum Speed)
        var breakChance = ballCarrier.MS * 0.01f;
        
        var finalChance = baseChance + hpAdvantage - breakChance;
        return Random.NextFloat() < finalChance;
    }
}
```

#### 3. Receiver vs Defender (Catch/Defense)

```csharp
public class PassDefenseBehavior : Behavior
{
    public override BehaviorResult Update(Player player, GameTime dt)
    {
        var ball = GameState.Ball;
        
        if (!ball.IsInAir)
            return BehaviorResult.Complete;
        
        // Move toward ball position
        var direction = Vector2.Normalize(ball.Position - player.Position);
        player.Velocity = direction * player.MaxSpeed;
        player.Position += player.Velocity;
        
        // Check for ball arrival
        if (IsCollision(player, ball))
        {
            // Determine outcome
            var outcome = ResolvePassDefense(player, ball);
            
            switch (outcome)
            {
                case PassOutcome.Interception:
                    ball.Catch(player);
                    GameState.Turnover();
                    break;
                case PassOutcome.Deflection:
                    ball.Deflect();
                    break;
                case PassOutcome.Incomplete:
                    ball.Drop();
                    break;
            }
            
            return BehaviorResult.Complete;
        }
        
        return BehaviorResult.Continue;
    }
}
```

---

## Play Execution System

### Behavior Stack

Players can have behaviors pushed/popped (grapple interrupts rush):

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
                BehaviorStack.Pop();
                break;
            case BehaviorResult.SwitchTo(Behavior newBehavior):
                BehaviorStack.Pop();
                BehaviorStack.Push(newBehavior);
                break;
            case BehaviorResult.Push(Behavior newBehavior):
                BehaviorStack.Push(newBehavior);  // Interrupt
                break;
            case BehaviorResult.PopAndContinue:
                BehaviorStack.Pop();  // Resume previous
                break;
        }
    }
}
```

### Play-to-Behavior Mapping

```yaml
# content/plays/defense_cover_2.yaml
defensive_play:
  name: "Cover 2"
  
  assignments:
    RE:
      behavior: rush_qb
      rush_path: outside_arc
      
    NT:
      behavior: rush_qb
      rush_path: straight
      
    ROLB:
      behavior: zone_drop
      zone: [15, -10]  # yards deep, outside
      
    RCB:
      behavior: man_coverage
      target: WR1
      cushion: 5
      
    FS:
      behavior: zone_drop
      zone: [20, 0]  # Deep middle
```

---

## Game Loop

```csharp
public class OnFieldGameState : GameState
{
    public override void Update(GameTime dt)
    {
        // 1. Update all player behaviors (60Hz)
        foreach (var player in AllPlayers)
        {
            player.UpdateBehavior(dt);
        }
        
        // 2. Check collisions (every frame)
        CollisionSystem.CheckCollisions();
        
        // 3. Update ball physics (if in air)
        if (Ball.IsInAir)
        {
            Ball.UpdateTrajectory(dt);
        }
        
        // 4. Check play outcomes
        CheckPlayEndConditions();
        
        // 5. Update game clock
        GameClock.Update(dt);
    }
}
```

---

## Key Mechanics Summary

| Mechanic | Implementation |
|----------|----------------|
| **Movement** | Velocity with instant direction change, speed ramps to max |
| **Collision** | Discrete distance check every frame (~8px range) |
| **Grapple** | Both stop moving, HP vs HP check each frame |
| **Tackle** | Distance check, HP/HP + MS check for success |
| **Coverage** | Man = track target position, Zone = go to spot then read |
| **Pass Rush** | Move toward QB position, grapple blockers, tackle at range |

---

## Data Flow

```
YAML Play Definition
       ↓
Behavior Factory (creates behavior objects)
       ↓
Player.BehaviorStack (at snap)
       ↓
Update() every frame:
  - Run current behavior
  - Check collisions
  - Push/pop behaviors
  - Update positions
```

This preserves the original feel while being clean, data-driven C# code.
