# ROM command ↔ engine-native command mapping (2-play subset)

Purpose: preserve Tecmo semantics while expressing behavior as engine-native commands + ECS state.

This doc is intentionally **incomplete**: it covers only the subset needed for the current **2-play demo** (offensive play_number=10).

## Engine-native commands (current)

### Gating / flow

- `wait_until_snap(stance)`
  - **Intent:** script yields until the play is live.
  - **Engine behavior:** `PlayScriptSystem` holds IP steady until `PlayState.Phase == InPlay`.

### Ball / possession

- `handoff_to(slot, delayFrames)`
  - **Intent:** transfer possession from QB to the named slot after a deterministic delay.
  - **Engine behavior:** delayed yield; then sets `PlayState.BallOwnerEntityId`, updates `BallCarrierComponent.HasBall`, and syncs the dedicated ball entity.
  - **Control:** triggers deterministic control swap to the new carrier via `ControlState.PendingForcedEntityId`.

### Tracking / pursuit

- `rush_qb`
  - **Intent:** defenders rush the QB (pressure).
  - **Engine behavior:** sets `BehaviorState.TrackingPlayer` with `TargetEntityId = offense QB`.

- `pursue_ballcarrier`
  - **Intent:** flow/pursue the current ballcarrier.
  - **Engine behavior:** sets `BehaviorState.TrackingPlayer` with `TargetEntityId = current ballcarrier`.

### Movement / turning (Tecmo-feel)

- Turning is limited globally via `MovementTuningComponent.MaxTurnDegreesPerTick`.
  - **Intent:** emulate Tecmo’s non-instant direction change and chase arcs.

## ROM/disassembly references (intent-level)

The Tecmo disassembly expresses pursuit feel with:

- **Conservative chase turning** (turn-rate limiting / reduced adjustment):
  - `CHASE_CONSERVATIVE_TURN_TABLE` in `Bank21_22_play_commands_on_field_logic.asm`

- **Timed defender slowdown after snap** (time-based nerf):
  - `DEFENDER_SLOW_DOWN_DELAY_FRAMES = $1E` in `Bank21_22_play_commands_on_field_logic.asm`

We currently model the **turning** intent explicitly (turn-rate limiting). The **timed slowdown** intent is planned as a dedicated ECS speed-mod system with tunable constants.

## 2-play demo wiring

- Offensive play: `play_number=10` ("T FAKE SWEEP R")
- PlayData YAML: `content/playdata/bank5_6_play_data.yaml`
  - QB reaction uses `handoff_to(HB, 38)` after snap.
  - Defense reactions use `rush_qb` / `pursue_ballcarrier`.

