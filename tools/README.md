# Asset Extraction Tools

Tools for extracting and converting NES assets from Tecmo Super Bowl ROM.

## Prerequisites

```bash
pip install Pillow
```

## Tools

### extract_chr.py
Extracts CHR (character/tile) data from the NES ROM.

```bash
python extract_chr.py --rom tecmo.nes --bank 10 --output player_sprites.chr
python extract_chr.py --rom tecmo.nes --address 0x4010 --size 8192 --output tiles.chr
```

### convert_chr_to_png.py
Converts CHR data to PNG sprite sheets.

```bash
# Convert with default palette (grayscale + colors)
python convert_chr_to_png.py --input player_sprites.chr --output players.png

# Convert with specific NES palette
python convert_chr_to_png.py --input player_sprites.chr --output players.png \
    --palette 0x0f,0x20,0x28,0x30 --scale 2
```

### extract_all.py
Master extraction script (requires ROM bank addresses to be configured).

```bash
python extract_all.py --rom ~/roms/tecmo-super-bowl.nes --output ../assets/
```

## Workflow

1. **Extract**: Use `extract_chr.py` to pull raw CHR data from ROM banks
2. **Convert**: Use `convert_chr_to_png.py` to create PNG files
3. **Copy**: Place PNGs in `Content/` directory
4. **Build**: Run MonoGame Content Pipeline (MGCB) to create XNB files

## NES Graphics Format

- **Tiles**: 8x8 pixels, 2bpp (4 colors), 16 bytes per tile
- **Sprites**: Can be 8x8 or 8x16 (set via PPU register)
- **Palettes**: 4 colors per palette, 6-bit color indices
- **CHR Banks**: Typically 8KB per bank

## Notes

- Actual ROM bank addresses need to be determined from disassembly
- CHR data may be compressed or scattered across multiple banks
- See `docs/rom_map_reference.md` for bank layout details
