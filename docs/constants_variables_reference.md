# Constants and Variables Reference

Source: `constants_variables/*.asm` files

## Overview

The `constants_variables/` directory contains ~40 `.asm` files defining constants, enums, and variable offsets used throughout the NES ROM.

## Categories

### Bank/ROM Constants
- `bank_ids.asm` - PRG bank identifiers
- `chr_bank_names.asm` - CHR bank names
- `rom_map.asm` - ROM layout (already covered)

### Game Data Constants
- `team_ids_league_structure.asm` - Team IDs and conference/divisions
- `formation_ids.asm` - Formation type identifiers
- `playoff_bracket_ppu_locations.asm` - UI locations for playoff bracket
- `roster_ids.asm`, `roster_positions_starter_ids.asm` - Player roster data
- `skill_indexes.asm`, `stat_indexes.asm` - Player attribute indices

### UI/PPU Constants
- `banner_ids.asm` - Banner graphics IDs
- `coin_toss_ppu_locations.asm` - Coin toss screen positions
- `color_ids.asm` - Palette color indices
- `end_of_game_stats_ppu_locations.asm` - Stats screen layout
- `field_locations.asm` - Field coordinate constants
- `leader_screen_ppu_locations.asm` - Leaderboard UI positions
- `menu_choices.asm` - Menu option indices
- `pallete_indexes.asm` - Palette table indices
- `player_data_ppu_locations.asm` - Player info screen layout
- `ppu_locations.asm` - General PPU nametable addresses

### Hardware/Engine Constants
- `nes_registers.asm` - NES hardware registers
- `mmc3_registers.asm` - MMC3 mapper registers
- `sprite_script_ids.asm` - Sprite animation IDs
- `sound_ids.asm` - Sound effect/music IDs
- `scene_ids.asm` - Scene/cutscene IDs
- `cutscene_sequence_ids.asm` - Cutscene timing

### Memory Variables (NOT for YAML)
- `ram_variables.asm` - RAM variable offsets
- `sram_variables.asm` - Save RAM offsets
- `zero_page_variables.asm` - Zero-page RAM usage

## MonoGame Approach

### Convert to YAML:
- Game data constants (team IDs, formation IDs, etc.)
- UI layout constants (positions can be useful reference)
- Sprite/sound/scene IDs (content references)

### Discard (NES-specific):
- RAM variable offsets (we use C# objects)
- PPU addresses (we use modern rendering)
- Hardware registers (not applicable)
- Zero-page variables (not applicable)

## Implementation

Create a single YAML file per category:
- `content/constants/team_constants.yaml`
- `content/constants/formation_constants.yaml`
- `content/constants/ui_constants.yaml`
- `content/constants/content_ids.yaml`

## Progress

Most game data constants are already captured in:
- `content/teamtext/bank16_team_text_data.yaml` (team IDs)
- `content/formations/bank3_formation_metatile_data.yaml` (formation IDs)
- Various content YAMLs (sound IDs, sprite IDs, etc.)

The remaining work is organizing these into dedicated constant files for clarity.
