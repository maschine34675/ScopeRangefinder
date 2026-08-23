# maschine-ScopeRangefinder

## Overview

Adds a compact rangefinder readout to magnified optic scopes. The display is rendered inside the scope view, follows the optic while aiming, and can be adjusted per scope.

Also adds auto zero: zero the optic to the measured distance, to the meter, with no distance limit, and accounting for the loaded ammo, weapon accuracy, and every other dynamic factor the game itself uses for calibration. No more picking the nearest fixed dial step.

The mod includes layout presets for vanilla scopes and an in-game editor (F8) for fine tuning. The look of the readout is fully styleable - game, system, and custom fonts (several display fonts included), glow, colors, and a second zeroing row - with one-click style presets (globally or per scope) and a live preview, all in that one editor window.

## Features

- Range readout while aiming through magnified optics
- Scope-bound display that moves with the optic view
- Works with all optic scopes, including thermal and night vision, plus configurable non-magnified sights (the Milkor M2A1 reflex by default, for grenade launchers)
- Included vanilla scope layout presets
- Per-scope user layout overrides with `OffsetX`, `OffsetY`, and `Scale`
- In-game rangefinder editor (F8): per-scope layout with live editing, the style preset browser, and every style option in one window
- Optional Wilcox RAPTAR ES requirement
- Optional requirement for the attached RAPTAR to be switched on
- RAPTAR-style `0123` or decimal `045.0` readout format
- Meters/yards unit toggle with optional unit suffix, like on real rangefinders
- Optional zeroing line: a second readout row showing the currently effective zero (`RNG`/`ZRO` prefixes configurable); the game's corner zeroing panel stays hidden while it is visible
- Style presets: shipped looks applied with one click - to all scopes or to a single scope - own looks saved and managed from the same list
- Renders with the game's own Bender font (the RAPTAR display font) by default; system and custom fonts selectable
- Ships a curated set of display fonts (7-/14-/16-segment, cockpit, tactical/HUD, VCR, terminal; licensed under SIL OFL 1.1 or CC0 1.0), selectable from a font picker with live preview in the editor
- Crisp SDF text at any magnification with adjustable thickness, spacing, and a layered soft glow
- All characters are rendered monospaced, so the readout width never wobbles while digits change
- Configurable text and background color, transparency, and size
- Optional background plate behind the readout
- Auto zero: precise, meter-accurate zeroing to the measured distance, per hotkey or continuously, instead of the nearest fixed dial step
- Optional predicted bullet trajectory and impact dispersion ring, a great way to build a feel for Tushonka's ballistics
- Makes BetterZeroing, ExtendedZeroRanges, and AutoRanging unnecessary; compatible with all three if installed anyway (see Notes)
- Fallback screen overlay mode for PiP-Disabler compatibility

## Requirements

- SPT 4.1 with BepInEx (tested against 4.1.1; newer game builds may need a mod update, since internal game methods are hooked)
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

   `maschine-ScopeRangefinder v3.3.0 loaded (build ...).`

Fresh installs start with the showcase preset (`LED Display Coral Red`) applied — the defaults are its values. When updating from 2.2.0 or older, the first start saves your previous look as the style preset `My Settings (pre-2.3.0)` and applies the showcase preset once; your old look stays one click away in the editor's preset list. Updates from 2.3.x keep your look untouched.

If you update from 1.0.0 and still have `BepInEx/plugins/maschine-ScopeRangefinder.dll`, this version tries to remove that old file automatically. If Windows blocks removal, the mod shows a red conflict warning and stays inactive until the old DLL is removed manually.

## Configuration

Everything visual — style presets, fonts, colors, readout rows, background plate — is configured in the mod's own in-game editor (`F8`, see below). The F12 settings menu keeps only the non-style categories: General, Activation, Auto Zero, and Developer. All settings still live under their unchanged keys in the config file, so existing configs, hand edits, and style presets keep working.

Main config file:

`BepInEx/config/com.maschine.ScopeRangefinder.cfg`

Shipped preset file (scope layouts and style presets, read-only, replaced by updates):

`BepInEx/plugins/maschine-ScopeRangefinder/ScopeRangefinder.presets.json`

User scope override file:

`BepInEx/plugins/maschine-ScopeRangefinder/ScopeRangefinder.layouts.json`

User style preset file:

`BepInEx/plugins/maschine-ScopeRangefinder/ScopeRangefinder.styles.json`

## In-Game Editor

Default hotkey:

`F8`

One window for everything visual. The `Layout` section and per-scope preset assignments need an active scope (aim through one to see changes live); the global style can be tuned any time, with the built-in preview.

`Layout (this scope)` section (the window header shows the scope key, `Copy` copies it):

- `OffsetX` (arrow buttons `◀`/`▶`, matching the movement on screen)
- `OffsetY` (arrow buttons `▼`/`▲`)
- `Scale` (`-`/`+`)
- `Anchor`: a 3×3 grid choosing which point of the readout the offsets pin. With the default center, a readout that gains a row grows evenly up and down — parked in a corner, it walks off the edge and needs new offsets. With a corner or edge anchor (bottom-left for a bottom-left readout) the block grows away from the pinned point instead: taller upward, wider rightward, never off the edge, whatever the row count, unit, or ballistics mode. Switching the anchor keeps the readout where it is on screen; it only changes how future growth behaves.
- `Save Scope` / `Reset Scope` right below them: these write or remove exactly this scope's entry in `ScopeRangefinder.layouts.json` — its offsets, scale, and style assignment. Global style changes save themselves, so these two buttons never concern them.

Double arrows step ten times as far; values can also be typed directly.

`Style` section:

- The two lines at the top show the current situation at a glance: which preset the global style came from — with `(modified)` behind the name once its values no longer match that preset — and whether the current scope has its own preset assigned (`Clear` removes the assignment).
- `Browse & apply presets`: one list for both levels, switched by `Apply to all scopes` / `Only this scope`. In all-scopes mode a click applies the preset to the global style, like picking a theme. In this-scope mode a click assigns the preset to the current scope only — previewed live, written by `Save Scope`; the `(global style)` row removes the assignment. `Save Current As` stores the current global style as a new preset; own presets can be deleted with `✕` (confirming second click).
- A live preview of the readout, always rendered exactly like the scope shows it.
- `Readout options`, `Text options`, `Background options`: every style setting as direct controls (sliders, toggles, color channels, the font picker). These always edit the global style; while the current scope shows an assigned preset, a hint in the window says so — clear the assignment to tune what you see.

`Close` at the bottom hides the editor. Global style changes (options, applied presets) save to the config on their own.

## Sharing Styles

Looks can be passed around as a single block of JSON on the clipboard:

- `Copy` next to `Global style` copies the current look, including unsaved tweaks — the usual case when showing off a style.
- `⧉` on a preset row copies that preset.
- `Paste Shared Preset` imports whatever is on the clipboard as a new user preset and reports the name it got; click it in the list to apply it.

Imports are always stored under a free name (`My Preset (2)` and so on), never overwrite an existing or shipped preset, and are validated setting by setting against the running version — unknown keys and invalid values are dropped with a log note instead of ending up in the config. The copied text is a plain, readable JSON document, so it can be posted in a forum or chat as-is:

```json
{
  "ScopeRangefinderStyle": 1,
  "Name": "My Preset",
  "Values": {
    "Readout.ShowZeroLine": "true",
    "Scope Text.ScopeWorldTextColor": "00FF00FF"
  }
}
```

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
- `StylePreset` (optional): name of a style preset applied while aiming through this scope; omitted = global style
- `Anchor` (optional): which point of the readout the offsets position — `Center` (default, omitted), `TopLeft`, `Top`, `TopRight`, `Left`, `Right`, `BottomLeft`, `Bottom`, `BottomRight`

The included preset JSON contains vanilla scope keys with neutral default values.
If either installed layout file has no `Version` field or an unsupported version,
the mod replaces that file with current defaults on startup.

## Style Presets

A style preset is a named look covering every setting of the Readout, Scope Text, and Scope Background sections. Presets are browsed and applied from the in-game editor (F8): one click applies a preset to the global style or, in `Only this scope` mode, to the current scope only; `Save Current As` stores the current global style under a new name, and own presets can be deleted from the list (confirming second click).

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

Per-scope assignments (the editor's `Only this scope` mode) are applied as an override while aiming through that scope, without touching the global settings — for example an mrad hold-unit preset for mrad/FFP reticles and a centimeters preset for everything else.

## Config Sections

### General

| Key | Default | Description |
| --- | --- | --- |
| `Enabled` | `true` | Enables or disables the mod |
| `StylePreset` | (empty) | Records the last preset applied to the global style; managed from the in-game editor (F8). See Style Presets |
| `MaxDistance` | `1500` | Maximum measurement distance in meters |
| `ToggleEditor` | `F8` | Hotkey that shows or hides the in-game rangefinder editor |
| `UpdateInterval` | `0.1` | Seconds between distance updates while scoped |
| `ResetAllSettings` | — | Button that resets every setting of the mod to its default, guarded by a confirming second click |

### Activation

| Key | Default | Description |
| --- | --- | --- |
| `MinZoomBlendFactor` | `0` | Minimum zoom blend before the readout appears. `0` shows it as soon as the optic view is active |
| `ShowDelay` | `0.2` | Delay after entering the scope before showing the readout |
| `MinDisplayDistance` | `0` | Only show the readout when the measured target is at least this far away. `0` disables this condition |
| `NonMagnifiedSights` | Milkor M2A1 reflex | Comma-separated template IDs of non-magnified sights that also show the readout, through the screen overlay. Empty = magnified optics only |
| `RequireWilcoxRaptar` | `false` | Only show the readout when a Wilcox RAPTAR ES is attached |
| `RequireWilcoxRaptarActive` | `true` | When RAPTAR is required, also require it to be switched on |

When both RAPTAR options are enabled, the readout is shown whenever the attached RAPTAR is active. This overrides the zoom and minimum distance activation checks.

### Measurement

The raw ray is exact but brittle: sweeping across a target's edge, a rock rim, or a window frame makes it flip between the target and whatever lies behind it — or nothing — several times a second. Real rangefinders answer that with scan modes and a hold time, and so does this section. Everything here is off by default, so the stock behavior is unchanged until you opt in. All of it applies to the whole measurement — the readout, the ballistics line, auto zero, and the distance other mods read — so a continuous auto zero, for instance, stops snapping back on every dropout once a hold time is set.

| Key | Default | Description |
| --- | --- | --- |
| `HoldTime` | `0` | Seconds to keep the last valid distance when the ray briefly finds no target. `0` = off; 1–2 s works well |
| `ScanMode` | `Off` | `Near` holds the nearest, `Far` the farthest valid target seen within the scan window, so the reticle only has to brush a target instead of resting on it. Near suits targets in front of terrain or buildings, Far targets behind cover edges, grass, or glass |
| `ScanWindow` | `0.5` | Seconds a scan reading stays the winner before a newer one can replace it |
| `FreezeHotkey` | unbound | Holds the current distance until pressed again or the sight is lowered: a single-shot reading you can keep while shifting to the target. A frozen reading shows `°` behind the distance. It never touches the zeroing |

The following three style sections are edited in the in-game editor (F8) and no longer appear in the F12 menu. Their keys stay in the `.cfg` unchanged and are listed here for hand-editing and for writing preset files.

### Readout

| Key | Default | Description |
| --- | --- | --- |
| `DistanceUnit` | `Meters` | Displayed unit (`Meters`/`Yards`), like the unit toggle on real rangefinders. Auto zero always works on the true metric distance |
| `ShowUnitSuffix` | `true` | Append the unit to the readout (`0123m` / `0135yd`). The vanilla RAPTAR shows bare digits |
| `UseDecimalFormat` | `false` | `false` = `0123`, `true` = `045.0` |
| `ShowZeroLine` | `true` | Second readout row showing the currently effective zero: the auto-zeroed distance, `auto` in continuous mode, or the sight's dial distance when auto zero is off. Hides the game's corner zeroing panel while visible. Disable for the plain single-line RAPTAR look |
| `BallisticsLine` | `Hold` | Third readout row with a firing solution for the loaded round at the measured distance, computed with the game's own ballistics. `Hold`: vertical hold versus the current dial zero (positive = hold above the target). `Dial`: best zeroing stop of the active sight under the `DIA` prefix, shown without the unit suffix (the zeroing row above states it), plus the residual hold at that stop in compact form (e.g. `DIA 0350+1.2`) whenever it exceeds 0.15 mil — below that the stop is simply right. While auto zero drives the zero, or on a sight without usable stops, the row keeps its `DIA` prefix but shows the plain hold instead, since no stop can be recommended |
| `BallisticsHoldUnit` | `MinutesOfAngle` | Unit for hold values: milliradians (`mil`), minutes of angle (`moa`), or centimeters at the measured distance (`cm`) |
| `RangeLinePrefix` | `RNG` | Prefix for the measured distance row when the zeroing line is shown. Empty = none |
| `ZeroLinePrefix` | `ZRO` | Prefix for the zeroing row. Empty = none |
| `NoDistanceText` | `----` | Text shown when no valid target is hit |

### Scope Text

| Key | Default | Description |
| --- | --- | --- |
| `ScopeWorldTextColor` | coral red, semi-transparent | Text color and transparency |
| `ScopeFontSource` | `CustomFont` | `GameBender` = the game's own Bender font, exactly as on the RAPTAR display. `SystemFont` = installed OS font. `CustomFont` = font file from the plugin's fonts folder |
| `ScopeTextThickness` | `0` | Stroke weight: negative = thinner, positive = bolder (SDF fonts) |
| `ScopeTextSpacing` | `0` | Extra character spacing, useful for tight 7-segment fonts |
| `ScopeTextGlow` | `0.18` | Soft glow around the text in its own color, like an illuminated display: three stacked silhouette passes approximating a real glow falloff. `0` = off |
| `ScopeTextOutline` | `0` | Black outline around the glyphs, for contrast against bright backgrounds. `0` = off (SDF fonts) |
| `ScopeTextAberration` | `0` | Chromatic aberration: color fringes displaced in opposite directions along the radial axis from the scope center, like lens dispersion. Fringe hues follow the text color (red/cyan for white text). `0` = off (SDF fonts) |
| `ScopeFontName` | `Consolas` | OS font for `SystemFont`: family name as shown in Windows (`Lucida Console`) or file name (`lucon.ttf`); machine-wide and per-user fonts are found |
| `CustomFontFile` | `DigitTech14-Italic.otf` | For `CustomFont`: a file in `BepInEx/plugins/maschine-ScopeRangefinder/fonts/`, cycled through in the editor or typed manually (`file:assetname` selects one asset of a TMP bundle). Picking a file switches the font source automatically |
| `ScopeWorldTextOffsetY` | `0.004` | Vertical text offset inside the background plate |

Bundled fonts (SIL OFL 1.1 or CC0 1.0; matching license and archive-information files included):

- Segment displays: `DigitTech7-Italic.otf`, `DigitTech14-Italic.otf`, `DigitTech16-Regular.otf`, `DSEG7ClassicMini-Italic.ttf`, `DSEG14ClassicMini-Regular.ttf`, and `LCD14Condensed.otf`
- Cockpit/tactical/HUD: `B612Mono-Regular.ttf`, `Quantico-Regular.ttf`, `Oxanium-Medium.ttf`, and `Rajdhani-Regular.ttf`
- VCR/terminal/mono: `vcr-osd-replayed.ttf`, `HomeVideo-Regular.ttf`, `ShareTechMono-Regular.ttf`, and `VT323-Regular.ttf`

Exact upstream versions, checksums, and matching color palettes are documented in [`fonts/FONT-SOURCES.md`](fonts/FONT-SOURCES.md). Drop additional `.ttf`/`.otf` files or TMP font asset bundles into the same folder.

### Scope Background

| Key | Default | Description |
| --- | --- | --- |
| `ScopeWorldBackground` | `false` | Enables the background plate |
| `ScopeWorldBackgroundWidth` | `0.31` | Background plate width |
| `ScopeWorldBackgroundHeight` | `0.11` | Background plate height. This does not change text size |
| `ScopeWorldBackgroundColor` | black, mostly transparent | Background color and transparency |

### Auto Zero

Zeroes the active optic to the measured distance, to the meter, with no distance limit, instead of the nearest fixed dial step. Accounts for the loaded ammo and every other dynamic factor the game's own calibration uses. The original zeroing is restored whenever auto zero releases control, and using the zeroing dial manually always hands control back to the player.

| Key | Default | Description |
| --- | --- | --- |
| `AutoZeroEnabled` | `false` | Master switch for auto zero |
| `AutoZeroMode` | `Hotkey` | `Hotkey` zeroes once per key press and keeps that zero until re-pressed, the dial is used manually, or the sight changes. `Continuous` follows the measured distance while aiming |
| `AutoZeroHotkey` | `J` | Zeroes the optic to the currently measured distance |
| `AutoZeroTransitionTime` | `0.35` | Seconds to smoothly blend to a new zero instead of snapping. `0` = instant |
| `ShowTrajectoryPreview` | `false` | Draw the predicted bullet trajectory up to the measured distance. A good way to learn Tushonka's ballistics: bullet drop, travel time, and real dispersion at range |
| `AutoZeroTrajectoryNearColor` (advanced) | green, nearly transparent | Trajectory color at the muzzle. Keep the alpha low so near segments do not block the view |
| `AutoZeroTrajectoryFarColor` (advanced) | amber, opaque | Trajectory color at the far end |
| `AutoZeroImpactSpreadCircle` (advanced) | `true` | Ring at the impact point showing the maximum shot dispersion (weapon accuracy, durability, ammo, buffs, overheat) |
| `AutoZeroSpreadCircleColor` (advanced) | red-orange | Color of the dispersion ring |

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

## API for other mods

Other client mods can read the distance this mod last measured, for example to
make a distance-based mechanic use the exact measured range instead of the
sight's zeroing steps.

```csharp
public static class ScopeRangefinder.RangefinderApi
{
    public static float LastMeasuredDistanceMeters { get; }  // 0 = no valid measurement
    public static float LastMeasurementTime { get; }         // UnityEngine.Time.time of that measurement
}
```

The type name, the property names, and their types are a stable contract, so
reading them by reflection needs no hard dependency on this mod:

```csharp
Type api = Type.GetType("ScopeRangefinder.RangefinderApi, maschine-ScopeRangefinder");
float meters = (float)api?.GetProperty("LastMeasuredDistanceMeters").GetValue(null) ?? 0f;
float when = (float)api?.GetProperty("LastMeasurementTime").GetValue(null) ?? 0f;
if (meters > 0f && Time.time - when < 3f)
{
}
```

Resolve the type once and cache it. If `Type.GetType` returns `null` because the
assembly cannot be resolved by its simple name in your context, fall back to
searching the loaded assemblies for `maschine-ScopeRangefinder`; both work, and
either way a missing mod just leaves the type `null`.

Semantics:

- `LastMeasuredDistanceMeters` is the true metric distance, independent of the
  `DistanceUnit` display setting.
- It is `0` whenever there is no valid measurement: no target hit, the player is
  not aiming through a supported sight, the mod is disabled, or the raid ended.
  It is written only while a sight is in use and cleared as soon as aiming stops,
  so a consumer that needs the value beyond that has to latch it itself.
- "Supported sight" means any magnified optic plus the non-magnified sights
  listed under `NonMagnifiedSights` — by default the Milkor M2A1 reflex sight, so
  a grenade launcher aimed through it measures like a scope. A consumer that
  needs a specific sight to work should say so in its own documentation rather
  than assume the default list is unchanged.
- `LastMeasurementTime` advances on every valid measurement and also while the
  player holds a frozen reading (see the `Measurement` section), so a frozen
  reading counts as current rather than stale. How fresh a measurement has to
  be is up to the consumer; this mod enforces no limit.
- Measurements are taken while scoped at the `UpdateInterval` rate (0.1 s by
  default).

## Notes

- Red dots, holographics, and iron sights are not affected, with one configurable exception: the sights listed under `NonMagnifiedSights` (by default the Milkor M2A1 reflex sight, so grenade launchers get a measured distance). They have no optic camera, so they use the screen overlay and are positioned with the layout editor (F8) like the overlay path in general.
- The mod measures distance from the active optic camera direction.
- The readout itself never changes weapon zeroing, ballistics, or point of impact; only enabling auto zero does, and only for the optic it's applied to.
- With PiP-Disabler installed, the mod follows its runtime state per scope: while PiP is actually suppressed, the fallback screen overlay is used; scopes on PiP-Disabler's bypass list (or with its global toggle off) get the full in-scope readout.

## Credits

Built for SPT using BepInEx and Harmony.
