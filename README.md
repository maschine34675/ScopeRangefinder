# maschine-ScopeRangefinder

## Overview

Adds a compact rangefinder readout to magnified optic scopes in SPT. The display is rendered inside the scope view, follows the optic while aiming, and can be adjusted per scope.

Also adds auto zero: zero the optic to the measured distance, to the meter, with no distance limit, and accounting for the loaded ammo, weapon accuracy, and every other dynamic factor the game itself uses for calibration. No more picking the nearest fixed dial step.

The mod includes layout presets for vanilla scopes and an in-game layout editor for fine tuning. The look of the readout is fully styleable — game, system, and custom fonts (several display fonts included), glow, colors, and a second zeroing row — with one-click style presets and a live preview in the settings menu.

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
- Meters/yards unit toggle with optional unit suffix, like on real rangefinders
- Optional zeroing line: a second readout row showing the currently effective zero (`RNG`/`ZRO` prefixes configurable); the game's corner zeroing panel stays hidden while it is visible
- Style presets: shipped looks applied with one click, own looks saved and managed from the settings menu
- Renders with the game's own Bender font (the RAPTAR display font) by default; system and custom fonts selectable
- Ships a curated set of display fonts (7-/14-/16-segment, cockpit, tactical/HUD, VCR, terminal, and mono; licensed under SIL OFL 1.1 or CC0 1.0), selectable from a font picker with live preview in the settings menu
- Crisp SDF text at any magnification with adjustable thickness, spacing, and a layered soft glow
- All characters are rendered monospaced, so the readout width never wobbles while digits change
- Configurable text and background color, transparency, and size
- Optional background plate behind the readout
- Auto zero: precise, meter-accurate zeroing to the measured distance, per hotkey or continuously, instead of the nearest fixed dial step
- Optional predicted bullet trajectory and impact dispersion ring, a great way to build a feel for Tarkov's ballistics
- Makes BetterZeroing, ExtendedZeroRanges, and AutoRanging unnecessary; compatible with all three if installed anyway (see Notes)
- Fallback screen overlay mode for PiP-Disabler compatibility
- Minimal performance impact (one raycast every 0.1 s while scoped)

## Requirements

- SPT 4.0.13 with BepInEx (tested against 4.0.13; newer game builds may need a mod update, since internal game methods are hooked)
- Client-side installation only

## Installation

1. Place the mod folder here:

   `BepInEx/plugins/maschine-ScopeRangefinder/`

2. The folder should contain:

   - `maschine-ScopeRangefinder.dll`
   - `ScopeRangefinder.presets.json`
   - `fonts/` (bundled display fonts and their licenses)

   `ScopeRangefinder.layouts.json` (scope layout overrides) and `ScopeRangefinder.styles.json` (own style presets) are created at runtime and survive updates.

3. Start SPT.

4. Check `BepInEx/LogOutput.log` for:

   `maschine-ScopeRangefinder v2.3.0 loaded (build ...).`

When updating from 2.2.0 or older, the first start saves your previous look as the style preset `My Settings (pre-2.3.0)` and applies the showcase preset once, so the new style system is visible right away. Your old look is one click away in the `Style Preset` dropdown.

If you update from 1.0.0 and still have `BepInEx/plugins/maschine-ScopeRangefinder.dll`, this version tries to remove that old file automatically. If Windows blocks removal, the mod shows a red conflict warning and stays inactive until the old DLL is removed manually.

## Configuration

Main config file:

`BepInEx/config/com.maschine.ScopeRangefinder.cfg`

Shipped preset file (scope layouts and style presets, read-only, replaced by updates):

`BepInEx/plugins/maschine-ScopeRangefinder/ScopeRangefinder.presets.json`

User scope override file:

`BepInEx/plugins/maschine-ScopeRangefinder/ScopeRangefinder.layouts.json`

User style preset file:

`BepInEx/plugins/maschine-ScopeRangefinder/ScopeRangefinder.styles.json`

## In-Game Layout Editor

Default hotkey:

`F8`

The editor shows the current scope key and lets you adjust:

- `OffsetX` (arrow buttons `◀`/`▶`, matching the movement on screen)
- `OffsetY` (arrow buttons `▼`/`▲`)
- `Scale` (`-`/`+`)

Double arrows step ten times as far; values can also be typed directly.

Buttons:

- `Save`: writes the current scope layout to `ScopeRangefinder.layouts.json`
- `Reset`: removes the current user override and falls back to shipped presets/global defaults
- `Copy`: copies the current scope key to the clipboard
- `Close`: hides the editor

## Layout JSON

`ScopeRangefinder.presets.json` contains shipped presets and may be replaced by mod updates.
`ScopeRangefinder.layouts.json` contains user overrides and is not overwritten by builds or updates.
Per-scope user overrides take priority over shipped presets. The global `Default` entry only applies to scopes without a specific entry — it does not override shipped per-scope values.

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

## Style Presets

A style preset is a named look covering every setting of the Readout, Scope Text, and Scope Background sections. The `Style Preset` dropdown in the General settings applies a preset with one click; `Save Current As` stores the current look under a new name, and own presets can be deleted from the list (confirming second click).

Two sources, mirroring the layout files:

- Shipped presets live in the `Styles` section of the read-only `ScopeRangefinder.presets.json` and are replaced by updates — never edit them there; apply one, tweak it, and save it under an own name instead. Shipped names are reserved.
- Own presets all live in `ScopeRangefinder.styles.json`, which updates never touch.

Shipped presets: `RAPTAR EFT Style` (vanilla-inspired game-font look); `RAPTAR Lite ES` and `RAPTAR S` (rangefinder hardware looks); `LED Display Coral Red`, `DSEG7 Mini RGB Split`, `DSEG14 Classic Amber`, `DT16 Cyrillic`, and `LCD14 Starburst Red` (segment displays); `VCR Chromatic` (VCR OSD with restrained red/cyan lens dispersion); `B612 Cockpit Phosphor`, `Quantico Tactical Amber`, `Oxanium HUD Cyan`, and `Rajdhani Tech Chartreuse` (cockpit/HUD looks); `Terminal Green` (VT323); and `Tech Mono Ice` (Share Tech Mono, decimal format).

Preset values use the same format as the `.cfg` file, keyed by `Section.Key`:

```json
{
  "Version": 1,
  "Styles": {
    "My Preset": {
      "Readout.ShowZeroLine": "true",
      "Scope Text.ScopeWorldTextColor": "00FF00FF"
    }
  }
}
```

Covered settings missing from a preset are reset to their defaults when it is applied, so every preset is a complete, reproducible look.

## Config Sections

### General

| Key | Default | Description |
| --- | --- | --- |
| `Enabled` | `true` | Enables or disables the mod |
| `StylePreset` | (empty) | Dropdown with shipped and own style presets; records the last applied one. See Style Presets |
| `MaxDistance` | `1500` | Maximum measurement distance in meters |
| `ToggleEditor` | `F8` | Hotkey that shows or hides the in-game layout editor |
| `UpdateInterval` | `0.1` | Seconds between distance updates while scoped |
| `ResetAllSettings` | — | Button that resets every setting of the mod to its default, guarded by a confirming second click |

### Activation

| Key | Default | Description |
| --- | --- | --- |
| `MinZoomBlendFactor` | `0` | Minimum zoom blend before the readout appears. `0` shows it as soon as the optic view is active |
| `ShowDelay` | `0.2` | Delay after entering the scope before showing the readout |
| `MinDisplayDistance` | `0` | Only show the readout when the measured target is at least this far away. `0` disables this condition |
| `RequireWilcoxRaptar` | `false` | Only show the readout when a Wilcox RAPTAR ES is attached |
| `RequireWilcoxRaptarActive` | `true` | When RAPTAR is required, also require it to be switched on |

When both RAPTAR options are enabled, the readout is shown whenever the attached RAPTAR is active. This overrides the zoom and minimum distance activation checks.

### Readout

| Key | Default | Description |
| --- | --- | --- |
| `DistanceUnit` | `Meters` | Displayed unit (`Meters`/`Yards`), like the unit toggle on real rangefinders. Auto zero always works on the true metric distance |
| `ShowUnitSuffix` | `false` | Append the unit to the readout (`0123m` / `0135yd`). The vanilla RAPTAR shows bare digits |
| `UseDecimalFormat` | `false` | `false` = `0123`, `true` = `045.0` |
| `ShowZeroLine` | `true` | Second readout row showing the currently effective zero: the auto-zeroed distance, `auto` in continuous mode, or the sight's dial distance when auto zero is off. Hides the game's corner zeroing panel while visible. Disable for the plain single-line RAPTAR look |
| `RangeLinePrefix` | `RNG` | Prefix for the measured distance row when the zeroing line is shown. Empty = none |
| `ZeroLinePrefix` | `ZRO` | Prefix for the zeroing row. Empty = none |
| `NoDistanceText` | `----` | Text shown when no valid target is hit |

### Scope Text

| Key | Default | Description |
| --- | --- | --- |
The section starts with a live preview: the readout rendered with the current font, color, thickness, spacing, glow, outline, and aberration — style tuning without looking through a scope.

| Key | Default | Description |
| --- | --- | --- |
| `ScopeWorldTextColor` | green, semi-transparent | Text color and transparency |
| `ScopeFontSource` | `GameBender` | `GameBender` = the game's own Bender font, exactly as on the RAPTAR display. `SystemFont` = installed OS font. `CustomFont` = font file from the plugin's fonts folder |
| `ScopeTextThickness` | `0` | Stroke weight: negative = thinner, positive = bolder (SDF fonts) |
| `ScopeTextSpacing` | `0` | Extra character spacing, useful for tight 7-segment fonts |
| `ScopeTextGlow` | `0` | Soft glow around the text in its own color, like an illuminated display: three stacked silhouette passes approximating a real glow falloff. `0` = off |
| `ScopeTextOutline` | `0` | Black outline around the glyphs, for contrast against bright backgrounds. `0` = off (SDF fonts) |
| `ScopeTextAberration` | `0` | Chromatic aberration: color fringes displaced in opposite directions along the radial axis from the scope center, like lens dispersion. Fringe hues follow the text color (red/cyan for white text). `0` = off (SDF fonts) |
| `ScopeFontName` | `Consolas` | OS font for `SystemFont`: family name as shown in Windows (`Lucida Console`) or file name (`lucon.ttf`); machine-wide and per-user fonts are found |
| `CustomFontFile` | (empty) | For `CustomFont`: dropdown listing the files in `BepInEx/plugins/maschine-ScopeRangefinder/fonts/`, or type a `.ttf`/`.otf`/bundle name manually (`file:assetname` selects one of several). Picking a file switches the font source automatically |
| `ScopeWorldTextOffsetY` | `0.004` | Vertical text offset inside the background plate |

Bundled fonts (SIL OFL 1.1 or CC0 1.0; matching license and archive-information files included):

- Segment displays: `DigitTech7-Italic.otf`, `DigitTech14-Italic.otf`, `DigitTech16-Regular.otf`, `DSEG7ClassicMini-Italic.ttf`, `DSEG14ClassicMini-Regular.ttf`, and `LCD14Condensed.otf`
- Cockpit/tactical/HUD: `B612Mono-Regular.ttf`, `Quantico-Regular.ttf`, `Oxanium-Medium.ttf`, and `Rajdhani-Regular.ttf`
- VCR/terminal/mono: `vcr-osd-replayed.ttf`, `HomeVideo-Regular.ttf`, `ShareTechMono-Regular.ttf`, and `VT323-Regular.ttf`

Exact upstream versions, checksums, and matching color palettes are documented in [`fonts/FONT-SOURCES.md`](fonts/FONT-SOURCES.md). Drop additional `.ttf`/`.otf` files or TMP font asset bundles into the same folder.

### Scope Background

| Key | Default | Description |
| --- | --- | --- |
| `ScopeWorldBackground` | `true` | Enables the background plate |
| `ScopeWorldBackgroundWidth` | `0.26` | Background plate width |
| `ScopeWorldBackgroundHeight` | `0.11` | Background plate height. This does not change text size |
| `ScopeWorldBackgroundColor` | dark green, semi-transparent | Background color and transparency |

### Auto Zero

Zeroes the active optic to the measured distance, to the meter, with no distance limit, instead of the nearest fixed dial step. Accounts for the loaded ammo and every other dynamic factor the game's own calibration uses. The original zeroing is restored whenever auto zero releases control, and using the zeroing dial manually always hands control back to the player.

| Key | Default | Description |
| --- | --- | --- |
| `AutoZeroEnabled` | `false` | Master switch for auto zero |
| `AutoZeroMode` | `Hotkey` | `Hotkey` zeroes once per key press and keeps that zero until re-pressed, the dial is used manually, or the sight changes. `Continuous` follows the measured distance while aiming |
| `AutoZeroHotkey` | `J` | Zeroes the optic to the currently measured distance |
| `AutoZeroTransitionTime` | `0.35` | Seconds to smoothly blend to a new zero instead of snapping. `0` = instant |
| `ShowTrajectoryPreview` | `false` | Draw the predicted bullet trajectory up to the measured distance. A good way to learn Tarkov's ballistics: bullet drop, travel time, and real dispersion at range |
| `AutoZeroTrajectoryNearColor` | green, nearly transparent | Trajectory color at the muzzle. Keep the alpha low so near segments do not block the view |
| `AutoZeroTrajectoryFarColor` | amber, opaque | Trajectory color at the far end |
| `AutoZeroImpactSpreadCircle` | `true` | Ring at the impact point showing the maximum shot dispersion (weapon accuracy, durability, ammo, buffs, overheat) |
| `AutoZeroSpreadCircleColor` | red-orange | Color of the dispersion ring |

Notes:

- The zeroing panel stays clearly distinguishable between modes: continuous shows a static `auto` (no distance, since the in-scope readout already shows it live); hotkey shows just the applied distance, for example `412m`.
- The trajectory ends at the measured target; the visible far end marks the predicted impact point.
- Everything inside the dispersion ring can be hit; nothing outside of it. The ring uses the game's own spread formula.
- BetterZeroing, ExtendedZeroRanges, and AutoRanging are no longer needed once you use auto zero, since it already zeroes more precisely and without the dial's distance limit. All three remain compatible if you keep them installed: BetterZeroing and ExtendedZeroRanges work fine alongside auto zero with no configuration, and AutoRanging is automatically paused while `AutoZeroEnabled` is on so the two mods do not fight over the zeroing (it works normally again whenever auto zero is off).

### Legacy Screen Overlay

The fallback screen overlay (used while PiP-Disabler actually suppresses the vanilla optic camera) has no dedicated options. It honors the shared style options: text color, font (game and system fonts; custom font files are a TMP feature and fall back to the game font), black outline, background plate toggle/color/size, vertical text offset, the zeroing line, and all readout format options — so style presets restyle the overlay too. SDF-bound options (thickness, glow, letter spacing, chromatic aberration, TMP font bundles) only affect the in-scope display.

The layout editor positions the overlay per scope: offsets and scale are stored under `overlay:`-prefixed keys in `ScopeRangefinder.layouts.json`, separate from the in-scope layouts. Shipped in-scope presets do not apply to the overlay.

### Developer

Advanced options, hidden unless the settings menu shows advanced settings.

| Key | Default | Description |
| --- | --- | --- |
| `LogScopeKeys` | `false` | Log the layout key of each sighted scope, for hand-editing `ScopeRangefinder.layouts.json`. The layout editor shows and copies the same key regardless |
| `LogLoadedFonts` | `false` | Log all loaded font assets plus the RAPTAR display font once per session, as an aid for identifying game fonts |
| `ConfigVersion` | — | Internal marker driving one-time migrations on updates; not meant to be edited |

## Notes

- Red dots, holographics, and iron sights are not affected.
- The mod measures distance from the active optic camera direction.
- The readout itself never changes weapon zeroing, ballistics, or point of impact; only enabling auto zero does, and only for the optic it's applied to.
- With PiP-Disabler installed, the mod follows its runtime state per scope: while PiP is actually suppressed, the fallback screen overlay is used; scopes on PiP-Disabler's bypass list (or with its global toggle off) get the full in-scope readout.

## Credits

Built for SPT using BepInEx and Harmony.
