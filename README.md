# maschine-ScopeRangefinder

Adds a compact rangefinder readout to magnified optic scopes in SPT. The display is rendered inside the scope view, follows the optic while aiming, and can be adjusted per scope.

The mod includes layout presets for vanilla scopes and an in-game layout editor for fine tuning.

## Features

- Range readout while aiming through magnified optics
- Scope-bound display that moves with the optic view
- Works with all optic scopes, including thermal and night vision
- Included vanilla scope layout presets
- Per-scope user layout overrides with `OffsetX`, `OffsetY`, and `Scale`
- In-game layout editor with live editing, save, reset, and copy scope key
- Optional Wilcox RAPTAR ES requirement
- Optional requirement for the attached RAPTAR to be switched on
- RAPTAR-style `0123` or decimal `045.0` readout format
- Configurable text and background color, transparency, size, and font.
- Optional background plate behind the readout
- Readout is drawn after the optic camera's anti-aliasing, so TAA cannot smear it
- Fallback screen overlay mode for PiP-Disabler compatibility
- Minimal performance impact (one raycast every 0.1 s while scoped)

## Requirements

- SPT 4.0.13 or newer with BepInEx
- Client-side installation only

## Installation

1. Place the mod folder here:

   `BepInEx/plugins/maschine-ScopeRangefinder/`

2. The folder should contain:

   - `maschine-ScopeRangefinder.dll`
   - `ScopeRangefinder.presets.json`
   - `ScopeRangefinder.layouts.json`

3. Start SPT.

4. Check `BepInEx/LogOutput.log` for:

   `maschine-ScopeRangefinder v2.0.0 loaded.`

If you update from 1.0.0 and still have `BepInEx/plugins/maschine-ScopeRangefinder.dll`, version 2.0.0 tries to remove that old file automatically. If Windows blocks removal, the mod shows a red conflict warning and stays inactive until the old DLL is removed manually.

## Configuration

Main config file:

`BepInEx/config/com.maschine.ScopeRangefinder.cfg`

Shipped scope preset file:

`BepInEx/plugins/maschine-ScopeRangefinder/ScopeRangefinder.presets.json`

User scope override file:

`BepInEx/plugins/maschine-ScopeRangefinder/ScopeRangefinder.layouts.json`

## In-Game Layout Editor

Default hotkey:

`F8`

The editor shows the current scope key and lets you adjust:

- `OffsetX`
- `OffsetY`
- `Scale`

Buttons:

- `Save`: writes the current scope layout to `ScopeRangefinder.layouts.json`
- `Reset`: removes the current user override and falls back to shipped presets/global defaults
- `Copy`: copies the current scope key to the clipboard
- `Close`: hides the editor

## Layout JSON

`ScopeRangefinder.presets.json` contains shipped presets and may be replaced by mod updates.
`ScopeRangefinder.layouts.json` contains user overrides and is not overwritten by builds or updates.
User overrides take priority over shipped presets.

Both files use the same format and scope template IDs as keys:

```json
{
  "Version": 3,
  "Default": {
    "OffsetX": 0,
    "OffsetY": 0,
    "Scale": 0
  },
  "Scopes": {
    "example_scope_template_id": {
      "OffsetX": 0,
      "OffsetY": 0,
      "Scale": 0
    }
  }
}
```

Only these three values are used per scope:

- `OffsetX`: horizontal placement inside the scope, normalized to the scope canvas size
- `OffsetY`: vertical placement inside the scope, normalized to the scope canvas size
- `Scale`: size adjustment inside the scope. `0` means standard size

The included preset JSON contains vanilla scope keys with neutral default values.
If either installed layout file has no `Version` field or an unsupported version,
the mod replaces that file with current defaults on startup.

## Config Sections

### General

| Key | Default | Description |
| --- | --- | --- |
| `Enabled` | `true` | Enables or disables the mod |
| `MaxDistance` | `1500` | Maximum measurement distance in meters |
| `UpdateInterval` | `0.1` | Seconds between distance updates while scoped |

### Activation

| Key | Default | Description |
| --- | --- | --- |
| `MinZoomBlendFactor` | `0.3` | Minimum zoom blend before the readout appears. `0` shows it as soon as the optic view is active |
| `MinDisplayDistance` | `0` | Only show the readout when the measured target is at least this far away. `0` disables this condition |
| `RequireWilcoxRaptar` | `false` | Only show the readout when a Wilcox RAPTAR ES is attached |
| `RequireWilcoxRaptarActive` | `true` | When RAPTAR is required, also require it to be switched on |

When both RAPTAR options are enabled, the readout is shown whenever the attached RAPTAR is active. This overrides the zoom and minimum distance activation checks.

### Readout

| Key | Default | Description |
| --- | --- | --- |
| `ShowDelay` | `0.2` | Delay after entering the scope before showing the readout |
| `UseDecimalFormat` | `false` | `false` = `0123`, `true` = `045.0` |
| `NoDistanceText` | `----` | Text shown when no valid target is hit |

### Scope Text

| Key | Default | Description |
| --- | --- | --- |
| `ScopeWorldTextColor` | green, semi-transparent | Text color and transparency |
| `ScopeFontName` | `Consolas` | Preferred installed OS font for the readout |
| `ScopeWorldTextOffsetY` | `0.007` | Vertical text offset inside the background plate |

### Scope Background

| Key | Default | Description |
| --- | --- | --- |
| `ScopeWorldBackground` | `true` | Enables the background plate |
| `ScopeWorldBackgroundWidth` | `0.26` | Background plate width |
| `ScopeWorldBackgroundHeight` | `0.11` | Background plate height. This does not change text size |
| `ScopeWorldBackgroundColor` | dark green, semi-transparent | Background color and transparency |

### Layout Editor

| Key | Default | Description |
| --- | --- | --- |
| `ToggleEditor` | `F8` | Shows or hides the in-game layout editor |

### Legacy Screen Overlay

These options only matter when PiP-Disabler is installed and the mod automatically uses the fallback screen overlay.

| Key | Default | Description |
| --- | --- | --- |
| `OffsetX` | `0` | Horizontal screen overlay offset |
| `OffsetY` | `0` | Vertical screen overlay offset |

## Notes

- Red dots, holographics, and iron sights are not affected.
- The mod measures distance from the active optic camera direction.
- The readout does not change weapon zeroing, ballistics, or point of impact.
- With PiP-Disabler installed, the vanilla optic camera is unavailable while scoped, so the mod automatically uses the fallback screen overlay.

## Credits

Built for SPT using BepInEx and Harmony.
