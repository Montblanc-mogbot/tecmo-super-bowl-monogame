# Asset Extraction & Import Pipeline

This document defines the pipeline for extracting graphics, palettes, and tiles from the NES ROM and importing them into the MonoGame content system.

## Overview

The NES Tecmo Super Bowl stores assets in a proprietary format within the ROM banks. This pipeline extracts those assets and converts them to modern formats usable by MonoGame.

## Pipeline Stages

### Stage 1: ROM Analysis & Extraction

**Input:** `Tecmo Super Bowl (USA).nes` ROM file

**Tools:**
- `tools/extract_chr.py` - Extracts CHR (character/tile) data from ROM banks
- `tools/extract_palettes.py` - Extracts NES palette data
- `tools/extract_metatiles.py` - Extracts background metatile definitions

**Output:** Raw binary files in `assets/raw/`

### Stage 2: Format Conversion

**Tools:**
- `tools/convert_chr_to_png.py` - Converts CHR data to PNG sprite sheets
- `tools/convert_nes_pal.py` - Converts NES palettes to RGB values
- `tools/convert_metatiles.py` - Converts metatiles to YAML + PNG

**Output:**
- `assets/sprites/` - PNG sprite sheets
- `assets/palettes/` - JSON/YAML palette definitions
- `assets/tiles/` - PNG tile atlases + YAML metadata

### Stage 3: MonoGame Content Pipeline

**Input:** Converted assets from Stage 2

**Process:**
1. Copy assets to `Content/` directory
2. Run MonoGame Content Pipeline Tool (MGCB) to build XNB files
3. Reference XNB files in game code via `Content.Load<T>()`

**Output:** `Content/bin/DesktopGL/*.xnb` files

## Asset Types

### 1. CHR Data (Sprite/Tile Graphics)

- **Location:** Various banks (check `docs/rom_map_reference.md`)
- **Format:** NES 2bpp planar format, 16 bytes per 8x8 tile
- **Extraction:** `tools/extract_chr.py --bank N --output assets/raw/chr_bankN.bin`
- **Conversion:** `tools/convert_chr_to_png.py --chr assets/raw/chr_bankN.bin --output assets/sprites/bankN.png`

### 2. Palettes

- **Location:** Bank 14 (`content/palettes/bank14.yaml` already extracted)
- **Format:** NES 6-bit color indices (0-63), 4 colors per palette
- **Conversion:** Map NES indices to RGB using standard NES palette

### 3. Metatiles (Background Tiles)

- **Location:** Banks 11-12 (`content/bgmetatiles/` already scaffolded)
- **Format:** 2x2 tile references with attributes
- **Conversion:** YAML metadata + PNG visualization

### 4. Sprite Animations

- **Location:** Banks 9-10 (`content/spritescripts/` already scaffolded)
- **Format:** Script-based animations with frame references
- **Conversion:** YAML animation data referencing CHR tiles

## Directory Structure

```
tecmo-super-bowl-monogame/
├── assets/
│   ├── raw/           # Extracted binary data from ROM
│   ├── sprites/       # Converted PNG sprite sheets
│   ├── palettes/      # Converted palette files
│   └── tiles/         # Converted tile atlases
├── Content/
│   ├── Sprites/       # MonoGame content sprites
│   ├── Palettes/      # MonoGame content palettes
│   └── Tiles/         # MonoGame content tiles
└── tools/
    ├── extract_chr.py
    ├── extract_palettes.py
    ├── convert_chr_to_png.py
    └── convert_nes_pal.py
```

## Usage

### Full Pipeline

```bash
# 1. Extract from ROM
cd tools
python extract_all.py --rom ~/roms/tecmo-super-bowl.nes --output ../assets/raw/

# 2. Convert to modern formats
python convert_all.py --input ../assets/raw/ --output ../assets/

# 3. Copy to Content and build
cd ..
cp -r assets/* Content/
dotnet mgcb /build Content/Content.mgcb
```

### Individual Asset Types

```bash
# Extract player sprites (Bank 10)
python tools/extract_chr.py --rom tecmo.nes --bank 10 --address 0xA000 --size 8192 --output assets/raw/player_sprites.chr

# Convert to PNG
python tools/convert_chr_to_png.py --input assets/raw/player_sprites.chr --palette content/palettes/bank14.yaml --output Content/Sprites/players.png
```

## Implementation Notes

- NES uses 2bpp planar format: each 8x8 tile = 16 bytes
- CHR-ROM banks are 8KB each
- Palettes use 6-bit color (0-63), mapped to standard NES RGB
- Sprite sizes: 8x8 or 8x16 (configurable via PPU register)
- Background uses 8x8 tiles in 2x2 metatile groupings

## Next Steps

1. Implement `extract_chr.py` for CHR data extraction
2. Implement `convert_chr_to_png.py` for PNG conversion
3. Create MGCB content project file
4. Set up automated pipeline script
5. Document specific bank locations for each asset type
