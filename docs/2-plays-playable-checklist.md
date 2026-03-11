# 2 plays playable — checklist (as of 2026-03-11)

Goal: one offensive run play + one defensive play produce a playable, deterministic loop driven by YAML play-data.

## What already exists

- PlayScript compilation scaffold: `src/TecmoSBGame/Spawning/PlayScriptCompiler.cs`
- PlayScript execution scaffold: `src/TecmoSBGame/Systems/PlayScriptSystem.cs`
- Movement supports target tracking **already**:
  - `BehaviorState.TrackingPlayer` computes desired direction toward `BehaviorComponent.TargetEntityId` in `src/TecmoSBGame/Systems/MovementSystem.cs` (see `GetBehaviorDirection`).

## Gaps to close for the 2-play demo

### 1) Handoff needs delay + correct entity scans (currently buggy)

File: `src/TecmoSBGame/Systems/PlayScriptSystem.cs` (`case PlayScriptOpKind.HandoffTo`)

Problems:
- No delay support (script op currently only includes a slot string; no `delayFrames`).
- Uses `ActiveEntities` for finding the target player and for carrier-flag updates.
  - `ActiveEntities` is constrained by the system aspect: only entities with `PlayScriptComponent + BehaviorComponent + PositionComponent + TeamComponent`.
  - That means **players without scripts will be skipped**, and the **ball entity will be skipped**.
- Ball sync loop also iterates `ActiveEntities`, so it likely never finds the ball entity.

Needed:
- Extend compiler/op to include delay parameter.
- Iterate actual player entities (by component presence) rather than `ActiveEntities`, or track offense/defense entity ids in `PlayState`.
- Find ball entity via a dedicated query (entities with `BallComponent`) instead of `ActiveEntities`.

### 2) Control-switch rules on handoff

Files:
- `src/TecmoSBGame/Systems/PlayerControlSystem.cs` (control assignment)
- `src/TecmoSBGame/Systems/InputSystem.cs` (input routing)
- `src/TecmoSBGame/State/PlayState.cs` (store controlled entity id?)

Needed:
- Deterministically switch control QB → HB once handoff completes.

### 3) Defensive pursuit/rush scripts (YAML → tracking)

Files:
- `src/TecmoSBGame/Spawning/PlayScriptCompiler.cs` (add ops like `pursue_ballcarrier`, `rush_qb`)
- `src/TecmoSBGame/Systems/PlayScriptSystem.cs` (set `BehaviorState.TrackingPlayer` + `TargetEntityId`)

Note:
- Movement already supports tracking if `TargetEntityId` is set.

### 4) Ensure play end (tackle/whistle) returns to pre-snap cleanly

Files:
- `src/TecmoSBGame/Systems/TackleResolutionSystem.cs`
- `src/TecmoSBGame/Systems/GameStateSystem.cs`
- `src/TecmoSBGame/Systems/ActionResolutionSystem.cs` (if involved)

Needed:
- Confirm tackle ends play and transitions game flow.
- Ensure all relevant per-play state resets (ball owner, carrier flags, behavior states, scripts, etc.).

### 5) Deterministic “2 plays” demo wiring

Files/data:
- Play list / call: `content/playcall/playlist.yaml` and play-call systems.
- Play-data YAML: `content/playdata/bank5_6_play_data.yaml`

Needed:
- Pick one offense play (e.g. play #10) and one defense play.
- Ensure those selections result in scripts applied to the right entities.

### 6) Logging + headless smoke test

Files:
- `src/TecmoSBGame/Headless/HeadlessRunner.cs`
- Add a minimal headless scenario: select play → snap → assert HB becomes owner → assert a defender tracks → assert play ends.

