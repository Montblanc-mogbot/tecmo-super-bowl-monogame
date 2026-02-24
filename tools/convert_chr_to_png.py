#!/usr/bin/env python3
"""
Convert NES CHR (2bpp planar) data to PNG images.

NES 2bpp format:
- 8x8 pixel tiles
- 16 bytes per tile
- First 8 bytes = bit 0 of each pixel
- Next 8 bytes = bit 1 of each pixel
- Combined: pixel value = (bit1 << 1) | bit0 (0-3)
"""

import argparse
import sys
from pathlib import Path
from typing import List, Tuple

try:
    from PIL import Image
except ImportError:
    print("Error: Pillow (PIL) is required. Install with: pip install Pillow", file=sys.stderr)
    sys.exit(1)

# Standard NES color palette (RGB values for NES color indices 0-63)
# From: https://www.nesdev.org/wiki/PPU_palettes
NES_PALETTE = [
    (0x62, 0x62, 0x62), (0x00, 0x1f, 0xb2), (0x21, 0x0c, 0xa0), (0x44, 0x00, 0x96),
    (0x73, 0x00, 0x6b), (0x80, 0x00, 0x2e), (0x6f, 0x0f, 0x00), (0x4c, 0x1e, 0x00),
    (0x19, 0x32, 0x00), (0x00, 0x3b, 0x00), (0x00, 0x3a, 0x1e), (0x00, 0x32, 0x5d),
    (0x00, 0x00, 0x00), (0x00, 0x00, 0x00), (0x00, 0x00, 0x00), (0x00, 0x00, 0x00),
    (0xb6, 0xb6, 0xb6), (0x10, 0x5c, 0xe7), (0x48, 0x3c, 0xdb), (0x74, 0x2a, 0xd0),
    (0xa7, 0x25, 0x9f), (0xb5, 0x2e, 0x5a), (0xa0, 0x46, 0x17), (0x79, 0x57, 0x00),
    (0x46, 0x6e, 0x00), (0x27, 0x78, 0x00), (0x00, 0x76, 0x3e), (0x00, 0x6e, 0x8a),
    (0x00, 0x00, 0x00), (0x00, 0x00, 0x00), (0x00, 0x00, 0x00), (0x00, 0x00, 0x00),
    (0xff, 0xff, 0xff), (0x5f, 0xa8, 0xff), (0x8f, 0x8a, 0xff), (0xbc, 0x78, 0xff),
    (0xec, 0x71, 0xff), (0xff, 0x76, 0xba), (0xff, 0x91, 0x6f), (0xff, 0xa5, 0x29),
    (0xcc, 0xbf, 0x00), (0xa4, 0xca, 0x1c), (0x6d, 0xd8, 0x64), (0x3f, 0xd4, 0xc5),
    (0x00, 0x00, 0x00), (0x00, 0x00, 0x00), (0x00, 0x00, 0x00), (0x00, 0x00, 0x00),
    (0xff, 0xff, 0xff), (0xbd, 0xe2, 0xff), (0xd1, 0xd6, 0xff), (0xe5, 0xce, 0xff),
    (0xf8, 0xcc, 0xff), (0xff, 0xce, 0xed), (0xff, 0xd9, 0xd1), (0xff, 0xe0, 0xbf),
    (0xea, 0xea, 0x9e), (0xd8, 0xef, 0x9e), (0xc4, 0xf3, 0xbd), (0xb7, 0xf2, 0xe6),
    (0x00, 0x00, 0x00), (0x00, 0x00, 0x00), (0x00, 0x00, 0x00), (0x00, 0x00, 0x00),
]

def decode_tile(chr_data: bytes, tile_index: int) -> List[List[int]]:
    """
    Decode a single 8x8 tile from CHR data.
    
    Returns a 2D list of pixel values (0-3).
    """
    offset = tile_index * 16
    tile_low = chr_data[offset:offset + 8]
    tile_high = chr_data[offset + 8:offset + 16]
    
    pixels = []
    for y in range(8):
        row = []
        low_byte = tile_low[y]
        high_byte = tile_high[y]
        for x in range(8):
            bit = 7 - x  # NES stores leftmost pixel in bit 7
            low_bit = (low_byte >> bit) & 1
            high_bit = (high_byte >> bit) & 1
            pixel = (high_bit << 1) | low_bit
            row.append(pixel)
        pixels.append(row)
    
    return pixels

def get_palette_colors(palette_indices: List[int]) -> List[Tuple[int, int, int]]:
    """
    Convert NES palette indices to RGB colors.
    
    Args:
        palette_indices: List of 4 NES color indices (0-63)
    
    Returns:
        List of 4 RGB tuples
    """
    return [NES_PALETTE[i & 0x3f] for i in palette_indices]

def create_tile_image(tile_pixels: List[List[int]], palette: List[Tuple[int, int, int]], scale: int = 1) -> Image.Image:
    """
    Create a PIL Image from decoded tile pixels.
    
    Args:
        tile_pixels: 8x8 grid of pixel values (0-3)
        palette: 4 RGB colors
        scale: Scale factor for output image
    
    Returns:
        PIL Image
    """
    img = Image.new('RGB', (8 * scale, 8 * scale))
    pixels = img.load()
    
    for y in range(8):
        for x in range(8):
            color = palette[tile_pixels[y][x]]
            for dy in range(scale):
                for dx in range(scale):
                    pixels[x * scale + dx, y * scale + dy] = color
    
    return img

def create_sprite_sheet(chr_data: bytes, palette: List[Tuple[int, int, int]], 
                        tiles_per_row: int = 16, scale: int = 1) -> Image.Image:
    """
    Create a sprite sheet from CHR data.
    
    Args:
        chr_data: CHR data bytes
        palette: 4 RGB colors
        tiles_per_row: Number of tiles per row in the sheet
        scale: Scale factor for each tile
    
    Returns:
        PIL Image containing all tiles
    """
    num_tiles = len(chr_data) // 16
    rows = (num_tiles + tiles_per_row - 1) // tiles_per_row
    
    tile_size = 8 * scale
    sheet_width = tiles_per_row * tile_size
    sheet_height = rows * tile_size
    
    sheet = Image.new('RGB', (sheet_width, sheet_height))
    
    for tile_idx in range(num_tiles):
        tile_pixels = decode_tile(chr_data, tile_idx)
        tile_img = create_tile_image(tile_pixels, palette, scale)
        
        row = tile_idx // tiles_per_row
        col = tile_idx % tiles_per_row
        
        x = col * tile_size
        y = row * tile_size
        
        sheet.paste(tile_img, (x, y))
    
    return sheet

def main():
    parser = argparse.ArgumentParser(description="Convert NES CHR data to PNG")
    parser.add_argument("--input", "-i", required=True, help="Input CHR file")
    parser.add_argument("--output", "-o", required=True, help="Output PNG file")
    parser.add_argument("--palette", "-p", type=lambda x: [int(i) for i in x.split(',')],
                        default="0x0f,0x20,0x28,0x30",
                        help="NES palette indices as comma-separated values (default: 0x0f,0x20,0x28,0x30)")
    parser.add_argument("--tiles-per-row", "-t", type=int, default=16, help="Tiles per row (default 16)")
    parser.add_argument("--scale", "-s", type=int, default=1, help="Scale factor (default 1)")
    
    args = parser.parse_args()
    
    input_path = Path(args.input)
    if not input_path.exists():
        print(f"Error: Input file not found: {input_path}", file=sys.stderr)
        sys.exit(1)
    
    chr_data = input_path.read_bytes()
    
    # Ensure we have complete tiles
    if len(chr_data) % 16 != 0:
        print(f"Warning: CHR data length {len(chr_data)} is not a multiple of 16 bytes", file=sys.stderr)
        chr_data = chr_data[:len(chr_data) - (len(chr_data) % 16)]
    
    # Get palette colors
    palette = get_palette_colors(args.palette)
    
    print(f"Converting {len(chr_data) // 16} tiles...")
    
    sheet = create_sprite_sheet(chr_data, palette, args.tiles_per_row, args.scale)
    
    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)
    
    print(f"Saved sprite sheet to {output_path}")
    print(f"Dimensions: {sheet.width}x{sheet.height}")

if __name__ == "__main__":
    main()
