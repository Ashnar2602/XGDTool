# XGDTool
XGDTool is an original Xbox and Xbox 360 disc utility written in C# on .NET 8, with both GUI and CLI workflows.

Version 2.0 is a full rewrite of the original C++ codebase, with the goal of keeping the feature set, simplifying maintenance, and improving speed.

## Highlights
- Fast conversion pipeline between Xbox disc formats.
- All conversion happens in memory, zero temp files are required. Each input format is written directly to the chosen output format.
- Low memory footprint, the CLI app usually runs at around 30MB total.
- Comprehensive built-in title database for accurate renaming, v2.0 does away with online database lookup.
- Unified app entry point that supports both GUI and CLI launch.
- Reauthor mode for compact output image layouts.
- Scrub mode to clear and trim unused sectors.
- Multithreaded CCI and CSO compression.
- Batch input handling from one or more paths.
- Automatic input format detection and validation.
- Automatic detection of split file parts with numbered suffixes (name.1.iso name.2.iso etc.).
- Handles low-level format details like sector-level transforms, directory metadata authoring, and container conversion.

## Current Format Support
- **Extracted**: i.e. XEX, XBE, HDD Ready
- **ISO**: Redump, XISO
- **GOD**: Games On Demand
- **CCI**: LZ4 Compressed XISO
- **CSO**: LZ4 Compressed XISO
- **ZAR**: ZSTD compressed filesystem container

## Performance
XGDTool version 2.0 is significantly faster than the legacy 1.0 version, especially for compression-heavy outputs and unused sector detection (scrubbing).

Actual throughput still depends on source format, storage speed, CPU, and selected options (for example, scrub and reauthor).

### Benchmarks
Conducted with an AMD Ryzen 7 5800X (3.80 GHz, 8-Core), 3600 MT/s DDR4, NMVe PCIe 4.0

- **Halo: CE (Rev 2)** - Redump ISO to CCI, Scrub/Trim
| XGDTool v2.0.0 | XGDTool v1.0.0 | Repackinator v2.0.4 | 
| ---- | ---- | ---- |
| 0 | 0 | 0 |

## CLI Usage
Run from the built app:

```text
XGDTool.exe <command> [options]
```

On non-Windows with dotnet:

```text
dotnet run --project XGDTool -- <command> [options]
```

### Commands
- `extract`
Extract image contents to a directory.
- `xiso`
Convert to Xbox compatible ISO image.
- `god`
Convert to Games on Demand format.
- `cci`
Convert to CCI compressed ISO format.
- `cso`
Convert to CSO compressed ISO format.
- `zar`
Convert to ZAR compressed file format.
- `autoxbox`
Automatically choose options suited for original Xbox.
- `autoxbox360`
Automatically choose options suited for Xbox 360.
- `autoxemu`
Automatically choose options suited for Xemu original Xbox emulator.
- `autoxenia`
Automatically choose options suited for Xenia Xbox 360 emulator.

### Common Options
- `--input`, `-i`
  - Required.
  - One or more input paths (file or directory depending on command).
- `--output`, `-o`
  - Optional.
  - Output directory. Defaults to current directory.

### Conversion Options
- `--scrub`, `-s`
  - Scrub output image of unused data.
- `--reauthor`, `-r`
  - Reauthor filesystem metadata and layout, produces the smallest image possible.
- `--split`, `-S`
  - Split output image into 4 GB parts, for use on FATX filesystem. 
  - **Note:** Auto-enabled for CCI and CSO formats.
- `--xbe`, `-x`
  - Generate attach XBE output where supported.
- `--rename`, `-n`
  - Rename output XBE to disc label or provided name.
  - **Note:** Only works when using `extract` command or `--xbe` option
- `--media`, `-m`
  - Patch XBE allowed media flags.
- `--sysupdate`, `-u`
  - Skip writing the `$SystemUpdate` directory to Xbox 360 output.
- `--icon`, `-c`
  - Set XBE title icon or GOD icon from a file path.

### Which Options Apply To Which Commands
- `extract`
    - `--rename`, `--icon`, `--media`, `--sysupdate`
- `xiso`
    - `--scrub`, `--split`, `--reauthor`, `--xbe`, `--rename`, `--icon`, `--sysupdate`
- `god`
    - `--scrub`, `--reauthor`, `--rename`, `--icon`, `--sysupdate`
- `cci`
    - `--scrub`, `--reauthor`, `--xbe`, `--rename`, `--icon`
- `cso`
    - `--scrub`, `--reauthor`, `--xbe`, `--rename`, `--icon`
- `zar`
    - `--sysupdate`
- auto commands
    - `--input`, `--output` only

### Examples
Extract files:

```text
XGDTool.exe extract -i "D:\Games\Game.iso" -o "D:\Out"
```

Create reauthored XISO:

```text
XGDTool.exe xiso -i "D:\Games\Game.1.iso" -o "D:\Out" -r
```

Create split CCI:

```text
XGDTool.exe cci -i "D:\Games\Game.iso" -o "D:\Out" -S
```

Auto target for Xenia:

```text
XGDTool.exe autoxenia -i "D:\Games\Game.iso" -o "D:\Out"
```

## GUI Usage
Run with no CLI arguments to launch the GUI.

```text
dotnet run --project XGDTool
```

## Build
This solution targets .NET 8.

Requirements:
- .NET SDK 8.0+

Build solution:

```text
dotnet build XGDTool.sln
```

Run GUI:

```text
dotnet run --project XGDTool
```

Run CLI:

```text
dotnet run --project XGDTool -- xiso -i "<input>" -o "<output>"
```

Publish:

```text
dotnet publish XGDTool/XGDTool.csproj -c Release
```

## Project Layout
- XGDTool: app entry point (routes to GUI when no args, CLI when args are present)
- XGDTool.CLI: System.CommandLine command surface
- XGDTool.GUI: Avalonia UI
- XGDTool.Lib: core readers, writers, converters, and format logic

## Status
The C# rewrite is actively developed and stable for day-to-day use. If you run into an issue, please open an issue with:
- Input format and command used
- Full CLI command line
- Log output or exception text
- Whether scrub and reauthor were enabled
