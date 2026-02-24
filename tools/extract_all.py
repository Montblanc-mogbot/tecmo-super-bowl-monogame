#!/usr/bin/env python3
"""
Master asset extraction script for Tecmo Super Bowl.

Extracts all graphics, palettes, and tile data from the NES ROM.
"""

import argparse
import subprocess
import sys
from pathlib import Path

# Bank locations for CHR data (from rom_map_reference.md)
# These need to be verified against the actual ROM
CHR_BANKS = {
    "player_sprites": {"bank": 10, "address": None, "size": 8192},
    "bg_tiles_11_12": {"bank": 11, "address": None, "size": 8192},
    "bg_tiles_12_13": {"bank": 12, "address": None, "size": 8192},
}

def run_extractor(tool: str, args: list) -> bool:
    """Run an extraction tool with arguments."""
    tool_path = Path(__file__).parent / tool
    cmd = [sys.executable, str(tool_path)] + args
    print(f"Running: {' '.join(cmd)}")
    result = subprocess.run(cmd, capture_output=True, text=True)
    if result.returncode != 0:
        print(f"Error: {result.stderr}", file=sys.stderr)
        return False
    print(result.stdout)
    return True

def extract_all(rom_path: Path, output_dir: Path) -> bool:
    """
    Extract all assets from the ROM.
    
    This is a scaffold - actual bank addresses need to be determined
    from the disassembly documentation.
    """
    raw_dir = output_dir / "raw"
    raw_dir.mkdir(parents=True, exist_ok=True)
    
    print("=" * 60)
    print("Tecmo Super Bowl Asset Extraction")
    print("=" * 60)
    
    # Extract CHR data from each bank
    for name, info in CHR_BANKS.items():
        output_file = raw_dir / f"{name}.chr"
        
        args = [
            "--rom", str(rom_path),
            "--bank", str(info["bank"]),
            "--size", str(info["size"]),
            "--output", str(output_file)
        ]
        
        if info["address"] is not None:
            args.extend(["--address", hex(info["address"])])
        
        if not run_extractor("extract_chr.py", args):
            print(f"Warning: Failed to extract {name}")
            continue
    
    print("=" * 60)
    print("Extraction complete")
    print(f"Raw assets saved to: {raw_dir}")
    print("=" * 60)
    
    return True

def main():
    parser = argparse.ArgumentParser(description="Extract all assets from Tecmo Super Bowl ROM")
    parser.add_argument("--rom", "-r", required=True, help="Path to NES ROM file")
    parser.add_argument("--output", "-o", default="assets", help="Output directory (default: assets)")
    
    args = parser.parse_args()
    
    rom_path = Path(args.rom)
    if not rom_path.exists():
        print(f"Error: ROM file not found: {rom_path}", file=sys.stderr)
        sys.exit(1)
    
    output_dir = Path(args.output)
    
    if not extract_all(rom_path, output_dir):
        sys.exit(1)

if __name__ == "__main__":
    main()
