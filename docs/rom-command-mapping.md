# ROM Command ↔ Engine Command Mapping

This document maps Tecmo Super Bowl NES on-field *player command* opcodes to the engine-native
PlayData YAML command vocabulary.

Goal: preserve gameplay semantics while using a modern ECS/state model.

Sources:
- `Tecmo_Super_Bowl_NES_Disassembly/Bank21_22_play_commands_on_field_logic.asm`
  - `GROUP_COMMAND_TABLE` (0x?0 group commands)
  - `SINGLE_COMMAND_TABLE` (0xC0..0xFF single-player commands)
- `Tecmo_Super_Bowl_NES_Disassembly/Bank5_6_off_def_play_data.asm`

## Conventions
- **ROM opcode**: value used by the original script bytecode.
- **ROM label**: label/name from the disassembly.
- **Engine command**: YAML command id we expose in `content/playdata/*.yaml`.
- **Notes**: what we preserve vs what we intentionally do not emulate.

## Single-player commands (0xC0..0xFF)

| ROM | ROM label (disassembly) | Engine command | Notes |
|---:|---|---|---|
| 0xD7 | `MOVE_RELATIVE` | `move_by(dx,dy)` | Signed byte deltas; affects movement target/intent. |
| 0xD0 | `SET_SNAP_LOC_RELATIVE_TO_BALL` | `set_anchor(kind=los, dx,dy)` | In ROM this sets a snap-relative anchor; engine stores anchor in PlayScript state. |
| 0xD1 | `SET_SNAP_LOC_RELATIVE_TO_MID` | `set_anchor(kind=midfield, dx,dy)` | Same as above but midfield reference. |
| 0xCC | `PASS_BLOCK` | `pass_block(...)` | Engine expresses as blocker intent + engagement permissions. |
| 0xCD | `MOVE_AND_BLOCK_RELATIVE` | `pull_and_block(offset=..., target=...)` | High-level: move to offset then engage. |
| 0xCF | `MOVE_AND_BLOCK_REL_BALL_CARRIER` | `pull_and_block(anchor=ballcarrier, offset=...)` | High-level: anchor follows ballcarrier. |
| 0xFB/0xFC | `CAN_COLLIDE` | `enable_engagement(mode=collide, mask=...)` | Engine stores eligibility/filters; we may not reproduce bit-exact masks initially. |
| 0xFD | `CAN_BLOCK` | `enable_engagement(mode=block, mask=...)` | Same as above. |
| 0xEA | `THREE_PT_STANCE` | `wait_until_snap(stance=three_point)` | Snap gating; stance is cosmetic/animation for now. |
| 0xEC | `TWO_PT_STANCE` | `wait_until_snap(stance=two_point)` | Snap gating. |
| 0xFE | `BRANCH` | `branch_if(...)` | Engine uses label-based control flow rather than byte offsets. |
| 0xFF | `JUMP` | `jump(label)` | Engine uses labels. |

## Group commands (0x?0)

These are multi-player style commands (upper nibble selects group command).

| ROM group | ROM label | Engine command | Notes |
|---:|---|---|---|
| 0x30 | `BLOCK_COMMAND_START` | `block_group(...)` | Often sets/executes blocking across multiple players. |
| 0x50 | `HANDOFF_COMMAND_START` | `handoff_to(slot, timing=...)` | Engine will model ball transfer via BallOwner/BallState + carrier flags. |

## TODO
- Flesh out remaining opcodes as we implement them.
- Add examples of full play scripts and the engine-native equivalents.
