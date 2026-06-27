# maschine-ScopeRangefinder

Adds a compact rangefinder readout to magnified optic scopes in SPT. The display is rendered inside the scope view, follows the optic while aiming, and can be adjusted per scope.

The mod includes layout presets for vanilla scopes and an in-game layout editor for fine tuning.

## Features

- Range readout while aiming through magnified optics
- Scope-bound display that moves with the optic view
- Optional projected overlay mode for TAA/DLSS setups where the optic-camera display jitters
- Included vanilla scope layout presets
- Per-scope layout file with `OffsetX`, `OffsetY`, and `Scale`
- In-game layout editor with live editing, save, reset, and copy scope key
- Optional Wilcox RAPTAR ES requirement
- Optional requirement for the attached RAPTAR to be switched on
- RAPTAR-style `0123` or decimal `045.0` readout format
- Configurable text color, text transparency, background color, and background transparency
- Optional background plate behind the readout
- Optional optic-camera anti-aliasing override to reduce TAA ghosting
- Fallback screen overlay mode for compatibility

## Requirements

- SPT 4.0.13 or newer with BepInEx
- Client-side installation only

## Installation

1. Place the mod folder here:

   `BepInEx/plugins/maschine-ScopeRangefinder/`

2. The folder should contain:

   - `maschine-ScopeRangefinder.dll`
   - `ScopeRangefinder.layouts.json`

3. Start SPT.

4. Check `BepInEx/LogOutput.log` for:

   `maschine-ScopeRangefinder v1.1.0 loaded.`

If you update from 1.0.0 and still have `BepInEx/plugins/maschine-ScopeRangefinder.dll`, version 1.1.0 tries to remove that old file automatically. If Windows blocks removal, the mod shows a red conflict warning and stays inactive until the old DLL is removed manually.

## Configuration

Main config file:

`BepInEx/config/com.maschine.ScopeRangefinder.cfg`

Scope layout file:

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
- `Reset`: removes the current scope-specific layout and falls back to global defaults
- `Copy`: copies the current scope key to the clipboard
- `Close`: hides the editor

## Layout JSON

The layout file uses scope template IDs as keys:

```json
{
  "Default": {
    "OffsetX": null,
    "OffsetY": null,
    "Scale": null
  },
  "Scopes": {
    "example_scope_template_id": {
      "OffsetX": -0.022,
      "OffsetY": -0.014,
      "Scale": 0.05
    }
  }
}
```

Only these three values are used per scope:

- `OffsetX`: horizontal placement inside the scope
- `OffsetY`: vertical placement inside the scope
- `Scale`: display size inside the scope

The included JSON already contains presets for vanilla scopes.

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

### Scope Display

| Key | Default | Description |
| --- | --- | --- |
| `ScopeRenderMode` | `ProjectedOverlay` | Selects the render path: `ProjectedOverlay`, `ExperimentalInScopeCamera`, or `LegacyOverlay`. Defaults to `LegacyOverlay` when PiP-Disabler is detected |
| `ScopeLocalOffsetX` | `-0.022` | Global horizontal fallback offset |
| `ScopeLocalOffsetY` | `-0.014` | Global vertical fallback offset |
| `ScopeWorldScale` | `0.05` | Global fallback display scale |

Per-scope JSON values override the global fallback offset and scale.

Render modes:

- `ProjectedOverlay`: recommended default without PiP-Disabler. Draws the readout as normal UI projected from the optic anchor.
- `ExperimentalInScopeCamera`: renders into the optic camera. This can look more physically integrated, but may show TAA/DLSS artifacts.
- `LegacyOverlay`: old fixed screen overlay.

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
| `ScopeWorldBackgroundWidth` | `0.28` | Background plate width |
| `ScopeWorldBackgroundHeight` | `0.12` | Background plate height. This does not change text size |
| `ScopeWorldBackgroundColor` | dark green, semi-transparent | Background color and transparency |

### Experimental InScopeCamera

| Key | Default | Description |
| --- | --- | --- |
| `ScopeCompensateZoomScale` | `true` | Keeps the experimental optic-camera display size and offsets more consistent across variable zoom levels |
| `ScopeAntialiasingOverride` | `Off` | Optional optic-camera anti-aliasing override for `ExperimentalInScopeCamera`. `FXAA` can reduce TAA ghosting on the scope readout |

Available values:

- `Off`
- `FXAA`
- `None`

If you use `ExperimentalInScopeCamera` with TAA or DLSS and see trails on the readout while moving, try `FXAA`.

### Layout Editor

| Key | Default | Description |
| --- | --- | --- |
| `ToggleEditor` | `F8` | Shows or hides the in-game layout editor |

### Legacy Screen Overlay

These options only matter when `ScopeRenderMode = LegacyOverlay`.

| Key | Default | Description |
| --- | --- | --- |
| `OffsetX` | `0` | Horizontal screen overlay offset |
| `OffsetY` | `0` | Vertical screen overlay offset |

## Notes

- Red dots, holographics, and iron sights are not affected.
- The mod measures distance from the active optic camera direction.
- The readout does not change weapon zeroing, ballistics, or point of impact.
- With PiP-Disabler installed, `ProjectedOverlay` is disabled and the mod uses `LegacyOverlay`. `ExperimentalInScopeCamera` needs the vanilla optic camera and also falls back to `LegacyOverlay`.

## Credits

Built for SPT using BepInEx and Harmony.


![options](https://github.com/maschine34675/ScopeRangefinder/blob/main/examplepictures/options.png?raw=true)
