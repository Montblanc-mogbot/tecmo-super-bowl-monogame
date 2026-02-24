# Player Sprites by Bank Reference

Source: `DOCS/player_sprites_by_bank.bmp`

## What This File Is

A 1434×812 pixel bitmap image (3.5MB) showing all player sprites organized by their CHR (character) banks in the NES ROM.

## Contents

The image displays:
- Player sprites from all 28 NFL teams
- Sprite animations (running, throwing, catching, tackling, etc.)
- Organization by CHR bank locations ($1000, $1400, $1800, $1C00)
- Uniform colors and helmet designs

## Why It's Not Converted to YAML

This is **visual reference material** - an image showing graphics, not data tables. The actual sprite data would be:

### For MonoGame Implementation:

| Original NES | MonoGame Equivalent |
|--------------|---------------------|
| CHR ROM banks | Sprite sheets (PNG/texture atlases) |
| 8x8 pixel tiles | Higher resolution sprites |
| Limited NES palette | Full color sprites |
| This reference image | Source art reference or sprite sheets |

## How This Data Is Used

1. **Sprite organization**: Shows which team sprites are in which banks
2. **Animation reference**: Shows frame sequences for animations
3. **Color palette**: Shows uniform/helmet color combinations

## For MonoGame Reimplementation

Instead of using this bitmap, you would:

1. **Create new sprite assets** in higher resolution
2. **Use sprite sheets** (PNG files with transparent backgrounds)
3. **Define animations** in code or animation data files
4. **Reference team colors** from `content/teamtext/bank16_team_text_data.yaml`

## Location of Related Data

- Team colors: `content/teamtext/bank16_team_text_data.yaml`
- Sprite scripts: `content/spritescripts_bank9/bank9_sprite_scripts.yaml`
- Face data: `content/faces/bank15_*.yaml`

## Preservation

The original `.bmp` file is preserved in the disassembly repository for reference. This documentation file exists to explain its purpose.
