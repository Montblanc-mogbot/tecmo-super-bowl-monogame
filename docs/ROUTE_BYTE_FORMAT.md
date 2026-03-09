# Tecmo Super Bowl (NES) Route Byte Format — Notes / Gaps

This project’s **goal** for receiver route running is *frame-accurate, timing-based* execution (60Hz), with **instant cuts** at break points.

However, the current repo YAML (`content/playdata/bank5_6_play_data.yaml`) is a **scaffold** describing play-command opcodes and example scripts. It does **not** yet include the ROM’s dedicated pass-route timing tables in a form we can consume.

## What we have today

### 1) Bank5/6 player reaction scripts (bytecode-style)

The ROM uses *player reaction scripts* (offense/defense) made of opcodes + parameters. This is reflected in `PlayDataModels.cs` and the scaffold YAML.

A typical entry in the scaffold looks like:

```yaml
- { cmd: pullRelative, params: [0x60, 0x28] }
- { cmd: pullRelative, params: [0x0C, 0x7F] }
- { cmd: pullMiddleOfField, params: [0x10, 0x00], label: loop_start }
- { cmd: loopTo, params: [loop_start] }
```

This is *already* a route-like description, but it is not explicitly expressed as “segments with frame counts”.

### 2) Route order vs. route geometry

In the disassembly (`Tecmo_Super_Bowl_NES_Disassembly/Bank21_22_play_commands_on_field_logic.asm`) there is a command labeled as the **AX ROUTE ORDER COMMAND**:

- `SET_TARGET_ORDER_COMMAND` sets the receiver target priority for passing (`PASS_TARGETS[]`, `CURRENT_PASS_TARGET`).

This is **not** the same thing as the receiver’s path/geometry/timing; it is *pass target selection ordering*.

## What we still need (for precise emulation)

To emulate Tecmo pass routes precisely we need a data representation that captures:

1. **Segment direction** (or destination) per route phase
2. **Frame count** per segment (break point timing)
3. Optional **sit/stop** behavior (“curl/sit and wait”)
4. Optional **release delay / press jam** at LOS

### Proposed minimal route table format (engine-facing)

Until the ROM tables are imported, the engine uses this internal format:

- A route is a list of nodes:
  - `Offset` (relative to captured origin/LOS)
  - `MinFrames` (frames spent before transitioning)
  - `Action` (RUN/CUT/SIT/RETURN)

This is implemented in `TecmoSBGame.Components.RouteComponent`.

### Likely ROM-level encoding (to confirm)

Based on common Tecmo-era scripting patterns and the existing opcode scaffold, the ROM route *movement* is likely encoded as either:

- **(A)** repeated “move/pull relative” commands where the *movement loop itself* implies timing, or
- **(B)** compact per-route tables of `{direction, frames}` pairs, interpreted by a route runner routine.

We have **not yet pinned down** the exact byte layout for (B) in the current disassembly references.

## Gap in YAML content

Current formation + play scaffolds provide:

- starting positions (`SetPosFromKick/SetPosFromHike/...`)
- high-level play selection
- placeholder route waypoints from `PlaySpawner` (generated, not ROM-derived)

But they do **not** provide:

- per-route **StemFrames**
- per-node **MinFrames**
- ROM-authentic per-route segments

## Next step to complete ROUTE-1 fully

Locate and extract the ROM’s **pass-route geometry/timing tables** (or the routine that interprets route movement commands) and encode them into YAML as frame-timed nodes.

Once that data exists, `PlaySpawner` can attach the `RouteComponent` directly from YAML instead of generating placeholder nodes.
