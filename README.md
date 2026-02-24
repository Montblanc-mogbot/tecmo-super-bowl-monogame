# Tecmo Super Bowl (MonoGame) — reimplementation

Goal: Recreate Tecmo Super Bowl’s moment-to-moment gameplay in a modern, data-driven (YAML) MonoGame codebase.

This repo is a clean-room reimplementation inspired by the structure of the NES original. We will replace ROM/RAM/SRAM tables with YAML content + runtime state.

## Content
- `content/` holds YAML-driven data (tuning tables, plays, formations, sprite scripts, etc.)
- `src/` holds the simulation + rendering engine.

## Current focus
- Port/reauthor sprite scripts (Bank9/10 in the disassembly) into YAML and build a runtime interpreter.
