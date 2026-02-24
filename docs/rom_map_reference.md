# ROM Map Reference

Source: `DOCS/rom_map.xlsx`

## What This File Is

A hex ROM map showing the memory layout of the original Tecmo Super Bowl NES ROM.

## Contents

The spreadsheet documents:
- Bank addresses (0x0-0x1F)
- Memory regions for each bank
- Data locations within the ROM
- Bank switching points

## Why It's Not Converted to YAML

This is **ROM architecture documentation** - it describes how the NES cartridge is organized. For the MonoGame reimplementation:

| ROM Map Concept | MonoGame Equivalent |
|-----------------|---------------------|
| Bank switching | Not needed - all data loaded at once |
| PRG ROM addresses | YAML file paths |
| CHR ROM graphics | Sprite sheets/texture atlases |
| Memory banking | ContentManager.Load() |

## ROM Bank Layout (Original NES)

| Bank | Address Range | Content |
|------|---------------|---------|
| 0-1 | $8000-$9FFF | Team data |
| 2 | $A000-$BFFF | Formation/metatile data |
| 3 | $8000-$9FFF | Defensive play pointers |
| 4 | $A000-$BFFF | Defensive play data |
| 5-6 | $A000-$BFFF | Offensive/defensive play commands |
| 7 | $A000-$BFFF | Scene scripts |
| 8 | $8000-$9FFF | Scene scripts (continued) |
| 9-10 | $A000-$BFFF | Sprite scripts |
| 11-12 | $8000-$9FFF | BG metatile tiles |
| 13-14 | $8000-$9FFF | Animation/palette data |
| 15 | $8000-$9FFF | Faces/playbooks |
| 16 | $8000-$9FFF | Menu screens |
| 17-18 | $8000-$9FFF | Main game loop |
| 19-20 | $8000-$9FFF | On-field gameplay |
| 21-22 | $8000-$9FFF | Play commands |
| 23 | $8000-$9FFF | Field/ball/collision |
| 24 | $8000-$9FFF | Draw script engine |
| 25 | $8000-$9FFF | Leaders/player data |
| 26-27 | $8000-$9FFF | Misc data |
| 28 | $8000-$9FFF | Sound engine |
| 29-30 | $8000-$9FFF | Sound data |
| 31 | $C000-$FFFF | Fixed bank (system code) |
| 32 | $E000-$FFFF | DMC samples |

## For MonoGame

All this data has been ported to:
- `content/*/*.yaml` - Game data files
- `src/TecmoSB/*.cs` - C# models and loaders

No bank switching needed - MonoGame loads everything into memory as needed.

## Preservation

The original Excel file is preserved in the disassembly repository. This documentation explains its relationship to the MonoGame reimplementation.
