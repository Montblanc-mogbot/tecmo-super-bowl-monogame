#!/usr/bin/env python3
"""
Extract CHR (character/tile) data from NES ROM files.

NES uses 2bpp planar format:
- Each 8x8 tile = 16 bytes
- First 8 bytes: low bit plane (bit 0)
- Next 8 bytes: high bit plane (bit 1)
- Combined to form 4-color pixels (0-3)
"""

import argparse
import sys
from pathlib import Path

def extract_chr(rom_data: bytes, address: int, size: int) -> bytes:
    """
    Extract CHR data from ROM at specified address.
    
    Args:
        rom_data: Full ROM data as bytes
        address: Starting address in ROM
        size: Number of bytes to extract (should be multiple of 16)
    
    Returns:
        CHR data as bytes
    """
    if address + size > len(rom_data):
        raise ValueError(f"Address {address:#x} + size {size} exceeds ROM size {len(rom_data)}")
    
    return rom_data[address:address + size]

def get_prg_bank_address(bank_number: int) -> int:
    """
    Calculate ROM address for a given PRG bank (16KB banks).
    
    NES header is 16 bytes, then PRG-ROM starts.
    Each bank is 16KB (0x4000 bytes).
    """
    header_size = 16
    bank_size = 0x4000
    return header_size + (bank_number * bank_size)

def extract_chr_bank(rom_data: bytes, bank_number: int) -> bytes:
    """
    Extract CHR data from a specific bank assuming standard layout.
    
    For CHR-ROM games, tile data is often in the PRG-ROM banks
    and needs to be extracted based on the game's specific layout.
    """
    # This is game-specific - for Tecmo Super Bowl, CHR data
    # is spread across multiple banks. See rom_map_reference.md
    address = get_prg_bank_address(bank_number)
    
    # Extract 8KB (typical CHR bank size)
    return extract_chr(rom_data, address, 8192)

def main():
    parser = argparse.ArgumentParser(description="Extract CHR data from NES ROM")
    parser.add_argument("--rom", "-r", required=True, help="Path to NES ROM file")
    parser.add_argument("--bank", "-b", type=int, help="PRG bank number (16KB)")
    parser.add_argument("--address", "-a", type=lambda x: int(x, 0), help="Direct address (hex or decimal)")
    parser.add_argument("--size", "-s", type=lambda x: int(x, 0), default=8192, help="Size in bytes (default 8192)")
    parser.add_argument("--output", "-o", required=True, help="Output file path")
    
    args = parser.parse_args()
    
    # Read ROM
    rom_path = Path(args.rom)
    if not rom_path.exists():
        print(f"Error: ROM file not found: {rom_path}", file=sys.stderr)
        sys.exit(1)
    
    rom_data = rom_path.read_bytes()
    
    # Determine extraction address
    if args.address is not None:
        address = args.address
    elif args.bank is not None:
        address = get_prg_bank_address(args.bank)
    else:
        print("Error: Must specify either --bank or --address", file=sys.stderr)
        sys.exit(1)
    
    print(f"Extracting {args.size} bytes from address {address:#x}...")
    
    try:
        chr_data = extract_chr(rom_data, address, args.size)
        
        # Write output
        output_path = Path(args.output)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_bytes(chr_data)
        
        print(f"Extracted {len(chr_data)} bytes to {output_path}")
        print(f"This represents {len(chr_data) // 16} tiles (8x8)")
        
    except ValueError as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
