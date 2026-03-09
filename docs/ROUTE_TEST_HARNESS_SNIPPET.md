# Route Timing Test Harness (Headless) — Snippet

This is a minimal deterministic harness you can paste into a test/console context to verify:

1. Same play + same ratings => identical coordinates per tick
2. Higher MS => faster route progression (higher MaxSpeedPerTick)
3. Break timing occurs on the exact configured frame counts

```csharp
// Pseudocode/snippet (can live in a unit test or a console app)
var world = new WorldBuilder()
    .AddSystem(new RouteFollowSystem())
    .AddSystem(new MovementSystem())
    .Build();

var id = PlayerEntityFactory.CreatePlayerWithAttributes(
    world,
    position: new Vector2(128, 112),
    teamIndex: 0,
    isPlayerControlled: false,
    isOffense: true,
    positionName: "WR",
    playerName: "WR",
    jerseyNumber: 80,
    stats: new PlayerStats { Ms = 100, Rs = 100, Hp = 50, Rp = 50, Bc = 50, Rec = 50, Pa = 50, Ar = 50, Kp = 50, Kab = 50 });

var e = world.GetEntity(id);
var origin = e.Get<PositionComponent>().Position;

// 30 frames straight, then a 90-degree cut upfield and keep running.
var route = new RouteComponent
{
    RouteType = "TEST",
    StemFrames = 30,
    BaseSpeed = 3.65f, // interpreted as speed at MS=69 (TSB max)
    Nodes = new List<RouteNode>
    {
        new RouteNode { Offset = new Vector2(60, 0), MinFrames = 30, Action = "RUN" },
        new RouteNode { Offset = new Vector2(60, -220), MinFrames = int.MaxValue, Action = "RUN" },
    }
};

e.Attach(route);

// Step 120 ticks (2 seconds @ 60Hz)
for (var tick = 0; tick < 120; tick++)
{
    world.Update(new GameTime(TimeSpan.FromSeconds(tick / 60.0), TimeSpan.FromSeconds(1.0 / 60.0)));

    var p = e.Get<PositionComponent>().Position;
    Console.WriteLine($"t={tick:000} pos=({p.X:0.00},{p.Y:0.00}) node={route.CurrentNodeIndex} f={route.FrameCounter}");
}

// Determinism check: run again with same initial conditions and diff the output.
```

Notes:
- `RouteFollowSystem` advances nodes based on `MinFrames` (frame counters) and snaps to the node’s absolute position at the transition tick (Tecmo-style cut).
- `MovementSystem` is responsible for applying speed/accel/decel; the route system only drives Behavior targets.
