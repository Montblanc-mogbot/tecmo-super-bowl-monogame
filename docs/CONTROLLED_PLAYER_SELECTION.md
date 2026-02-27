# Controlled Player Selection (Tecmo-style)

This project separates:

- **Player-controlled TEAM**: `TeamComponent.IsPlayerControlled == true` (human is playing this team)
- **Currently controlled ENTITY**: exactly one entity has `PlayerControlComponent.IsControlled == true`

The goal is to match Tecmo Super Bowl’s feel: you *mostly* control the ball carrier on offense, and on defense you control the best/nearest pursuing defender, with a manual switch option.

## LoopMachine mapping

We currently gate “input & switching” against the **OnField loop** state ids:

- `pre_snap` → offense selection before the snap (formation/setup)
- `live_play` → in-play control (run/pass/return)
- `post_play` → play is dead; no movement input and no manual switching

(See `LoopState.IsOnField("pre_snap", "live_play")` for the runtime gate.)

## Offense rules

### Pre-snap (`pre_snap`)

Default controlled entity:

1. If a QB exists on the player team offense (`PlayerAttributesComponent.Position == "QB"`), control the QB.
2. Otherwise, control the first eligible offensive player on the player team.

Switching (manual):

- While in `pre_snap`, pressing **Tab** cycles among eligible **player-team offensive** entities (stable order by `entityId`).

### Post-snap / live play (`live_play`)

Default controlled entity:

- If the **ball is held** by a player-team entity (`BallCarrierComponent.HasBall == true`), control that **ball carrier**.

Notes / future extensions:

- As pass logic is introduced, this spec expects:
  - control QB until handoff/throw
  - control targeted receiver during catch attempt
  - after catch, control receiver as ball carrier

(Current scaffolding only has “HasBall”, so it drives control based on possession.)

## Defense rules

### Live play (`live_play`) when opponent has the ball

Default controlled entity:

- Select the **nearest player-team defender** (by squared distance to the ball carrier position).
- Tie-breakers are deterministic: smallest `entityId` wins.

Switching (manual):

- Pressing **Tab** cycles through defenders ordered by distance-to-ball (nearest → farthest).

## Kickoffs / returns

Kickoff slice behavior (current prototype):

- Before the kick, the kicking team may be the player team (kicker is tagged `IsPlayerControlled`).
- Once the returner catches/receives the ball, the returner entity becomes player-team (`IsPlayerControlled = true`) and will be automatically selected as the controlled entity because it has `HasBall == true`.

Determinism note:

- In headless runs, we disable reading input devices; control selection is rule-based only.

## Runtime implementation summary

- `ControlState` (service) holds the authoritative selection:
  - `ControlledEntityId`, `ControlledTeamIndex`, and `Role` (debug)
- `PlayerControlSystem` applies Tecmo-style selection rules and toggles `PlayerControlComponent.IsControlled`.
- `InputSystem` applies movement only to entities where:
  - `TeamComponent.IsPlayerControlled == true` **and**
  - `PlayerControlComponent.IsControlled == true`

This is intended as minimal scaffolding for the next step (movement tuning + richer play state).
