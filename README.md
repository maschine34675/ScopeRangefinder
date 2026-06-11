# maschine-ScopeRangefinder

Adds a built-in laser rangefinder readout to **any magnified optic scope** in SPT — similar to the Wilcox RAPTAR ES or Vortex Ranger 1500, but integrated directly into your scope view. No extra item required.

## Features

- **Universal scope support** — works with all optic scopes, not tied to specific scope prefabs
- **Distance readout** — raycast-based measurement from the optic camera crosshair
- **LCD-style display** — compact green readout with a dark background panel, positioned below the reticle
- **Flexible activation**
  - By zoom level (`MinZoomBlendFactor`)
  - By target distance (`MinDisplayDistance`, e.g. only show from 50 m)
- **Configurable display**
  - RAPTAR-style (`0123`) or Vortex-style (`045.0`) number format
  - Adjustable position via built-in defaults plus config offsets
  - Show delay to sync with the scope zoom animation
- **Lightweight** — no Harmony patches, minimal performance impact (one raycast every 0.1 s while scoped)

## Requirements

- SPT with BepInEx
- Client-side mod only

## Installation

1. Build or download `maschine-ScopeRangefinder.dll`
2. Place it in `BepInEx/plugins/`
3. Start SPT
4. Check `BepInEx/LogOutput.log` for: `maschine-ScopeRangefinder v1.0.0 loaded.`

## Configuration

Config file: `BepInEx/config/com.maschine.ScopeRangefinder.cfg`

### General

| Key | Default | Description |
| --- | --- | --- |
| `Enabled` | `true` | Enable or disable the mod |
| `MaxDistance` | `1500` | Maximum raycast range in meters |
| `UpdateInterval` | `0.1` | Seconds between distance measurements |

### Activation

| Key | Default | Description |
| --- | --- | --- |
| `MinZoomBlendFactor` | `0.3` | Minimum scope zoom (0–1) before the readout appears. `0` = as soon as the optic view is active |
| `MinDisplayDistance` | `0` | Only show when the target is at least this many meters away. `0` = disabled |

When `MinDisplayDistance` is greater than `0`, zoom activation is ignored and only distance is used.

### Display

| Key | Default | Description |
| --- | --- | --- |
| `ShowDelay` | `0.2` | Seconds to wait after entering the scope before showing the readout |
| `OffsetX` | `0` | Additional horizontal offset in pixels (added to the built-in default position) |
| `OffsetY` | `0` | Additional vertical offset in pixels (added to the built-in default position) |
| `UseDecimalFormat` | `false` | `false` = `0123` (RAPTAR-style), `true` = `045.0` (Vortex-style) |
| `NoDistanceText` | `----` | Text shown when no valid target is hit |

`OffsetX` and `OffsetY` are **additional** offsets on top of the built-in default position hardcoded in the mod.

## What it does not do

- No automatic zeroing or elevation adjustment
- Not embedded in individual scope 3D models (uses a screen overlay for universal compatibility)
- Red dots, holographics, and iron sights are not affected

## Credits

Built for SPT using BepInEx.
