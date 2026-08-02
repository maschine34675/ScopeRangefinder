# Changelog
## 2.3.0 (in development)

### Added

- Added a font picker to the settings menu: the `Custom Font File` entry is a dropdown showing the current selection; expanded it lists all files in the fonts folder as clickable buttons (one click selects the font, switches the source to `CustomFont`, and collapses the list), with `Open Folder` and `Rescan` buttons. The text field stays available for the `bundle:asset` syntax.
- The mod now ships a curated set of display fonts (SIL Open Font License, family license files included): DSEG7/14 Classic and DSEG14 Modern plus LCD14 Condensed for segment displays; B612 Mono for a real cockpit-derived face; Quantico, Oxanium, and Rajdhani for tactical/HUD looks; and Share Tech Mono plus VT323 for mono/terminal styles. Exact upstream versions and checksums are included in `fonts/FONT-SOURCES.md`.
- Added a zeroing line to the readout (`ShowZeroLine`, on by default): a second row showing the currently effective zero — the auto-zeroed distance, `auto` in continuous mode, or the sight's dial distance when auto zero is off. Line prefixes are configurable (`RNG`/`ZRO` by default, empty = none); rows are left-aligned with the block centered on the automatically grown background plate. While the zeroing line is visible in the scope, the game's corner zeroing panel stays hidden. Disable it to restore the plain single-line RAPTAR look.
- Added a live readout preview at the top of the Scope Text settings: a sample distance rendered with the current font, color, thickness, spacing, glow, outline, and aberration — font tuning without looking through a scope.
- Added a `Reset` button in the General settings that resets every setting of the mod to its default value, guarded by a confirming second click.
- Added a `Developer > Log Scope Keys` option (advanced, off by default): logs the layout key of each sighted scope for hand-editing per-scope overrides in `ScopeRangefinder.layouts.json`. Previously the key was logged unconditionally; the layout editor still shows and copies the key regardless of this option.
- The layout editor's `+`/`-` buttons for OffsetX/OffsetY are now directional arrows (`◀◀ ◀ ▶ ▶▶` / `▼▼ ▼ ▲ ▲▲`), so the click direction matches the movement on screen. Scale keeps `-`/`+`.
- Added style presets: named looks covering all Readout, Scope Text, and Scope Background settings, selectable from a `Style Preset` dropdown in the General settings. One click applies a preset; the current look can be saved under a new name, and own presets can be deleted from the dropdown (guarded by a confirming second click). The file layout mirrors the scope layouts: shipped presets live in a `Styles` section of the read-only `ScopeRangefinder.presets.json` — replaced wholesale on updates, so never edit it — while every preset you save lands in `ScopeRangefinder.styles.json`, which updates never touch (shipped names are reserved). Shipped presets: `RAPTAR EFT Style`, `RAPTAR EFT Style 2`, `RAPTAR Lite ES`, `RAPTAR S`, `LED Display Coral Red`, `Home Video Optical Split`, `DSEG Mini RGB Split`, `DSEG Modern Amber`, `LCD14 Starburst Red`, `B612 Cockpit Phosphor`, `Quantico Tactical Amber`, `Oxanium HUD Cyan`, `Rajdhani Tech Chartreuse`, `Terminal Green`, and `Tech Mono Ice`.
- On the first start after updating from a pre-2.3.0 version, the previous look is saved automatically as the style preset `My Settings (pre-2.3.0)` and the showcase preset `LED Display Coral Red` is applied once, so the new style system is immediately visible. The old look is one click away in the preset list; fresh installs keep the defaults.

- Added a `Black Outline` setting (Scope Text): black outline around the glyphs for contrast against bright backgrounds, `0` = off. Outline is a base feature of the SDF shader, so it works with every SDF font; the glow stays in the pure text color even with an outline active.
- Added a `Chromatic Aberration` setting (Scope Text): color fringes on the readout, displaced in opposite directions — radially away from the scope center, like real lens dispersion, which grows outward from the optical axis. Fringe hues follow the text color: its spectral neighbors (hue ± 40°) for saturated colors, so colored text never washes toward white, blending to the classic red/cyan split for white text. The fringe opacity ramps in over the low slider range, so small values fade the effect in smoothly instead of abruptly brightening the glyph edges. `0` = off. The settings preview shows the effect with a horizontal split; the included `Home Video Optical Split` preset demonstrates a restrained red/cyan treatment.

### Changed

- The readout now shows regardless of zoom by default: `MinZoomBlendFactor` default changed from `0.3` to `0`. Existing config files keep their saved value.
- `ShowDelay` moved from the Readout section to Activation, where it belongs conceptually — it controls when the readout appears, not what it shows. It is therefore no longer part of style presets. A previously customized value resets to its default once due to the section move.
- The layout editor hotkey (`ToggleEditor`) moved from its own section into General. A previously customized binding resets to `F8` once due to the section move.
- The readout now forces monospacing for every character, not just digits (the RAPTAR routine only fixes digit widths): line prefixes and unit suffixes align in columns across both readout rows regardless of the selected font — even the game's Bender is not naturally monospaced.
- Reworked the text glow: three stacked silhouette passes with staggered falloff — narrow and bright at the glyph edge, wide and faint outside — approximate the Gaussian falloff of a real glow far better than the previous single pass, and the dilate now stays at the glyph edge so the glow no longer bolds the letters. (The SDF shader's built-in underlay was evaluated as a single-pass alternative and rendered identically, so the mod sticks with the approach that cannot be affected by shader-variant stripping.)

## 2.2.0

### Added

- The readout now uses the game's own Bender font — the exact TMP SDF asset the RAPTAR display and most of the game UI render with (`ScopeFontSource`, default `GameBender`; `SystemFont` remains selectable). No external font files needed.
- Added a `Text Thickness` setting: continuous stroke weight adjustment of the readout text (negative = thinner, positive = bolder), made possible by SDF rendering. This replaces a dedicated bold font variant.
- Added a `CustomFont` source: drop a `.ttf`/`.otf` file or a TMP font asset bundle into `BepInEx/plugins/maschine-ScopeRangefinder/fonts/` and select it via `Custom Font File`. Bundles with several fonts support `bundlefile:FontAssetName` to pick one; available names are logged.
- Added a `Letter Spacing` setting: extra character spacing for fonts with tight digit cells, such as 7-segment fonts.
- Added a `Text Glow` setting: soft glow around the readout text in its own color, like an illuminated display. `0` = off; requires an SDF font.
- Added a `Distance Unit` setting (meters/yards), like the unit toggle on real rangefinders, plus an optional unit suffix on the readout (`0123m` / `0135yd`, off by default like the vanilla RAPTAR). Auto zero always works on the true metric distance; the zeroing panel shows the selected unit.
- `SystemFont` now also finds per-user installed fonts (`%LOCALAPPDATA%\Microsoft\Windows\Fonts`), which Unity's OS font list misses — machine-wide fonts like Times New Roman worked, per-user installs like Roboto did not.
- `System Font Name` accepts the family name as shown in Windows (resolved through the registry font table, e.g. `Lucida Console`) in addition to file names (e.g. `lucon.ttf`).
- A one-time log hint explains when `Text Thickness` has no effect because the selected font asset is bitmap-rendered instead of SDF.
- Fixed `SystemFont`/custom font creation throwing every frame: TMP's font asset creation requires the mobile SDF shader, which the game never loads. The shader cache is now seeded with the game's regular Distance Field shader, and failures fall back to the game font with a single log warning.
- Added a `Developer > LogLoadedFonts` option (advanced, off by default) that logs all loaded font assets plus the RAPTAR display font once per session, as an aid for identifying game fonts.

### Changed

- The readout text migrated from TextMesh to TextMeshPro: crisp SDF edges at any magnification, and digits are monospaced through the same routine the RAPTAR display uses, so the readout width no longer wobbles while digits change.
- The default readout font is now the game's Bender instead of Consolas. Set `ScopeFontSource` to `SystemFont` to restore the previous look (system fonts are converted to dynamic TMP assets at runtime).

## 2.1.0

### Added

- Added auto zero (`AutoZeroEnabled`, off by default): zeroes the active optic to the measured distance, to the meter, with no distance limit, accounting for the loaded ammo and every other dynamic factor the game's own calibration uses. Two modes (`AutoZeroMode`):
  - `Hotkey` (default): pressing `AutoZeroHotkey` (default `J`) zeroes once to the currently measured distance. The zero persists across unscoping until re-pressed, the zeroing dial is used manually, or the sight changes.
  - `Continuous`: the optic follows the measured distance while aiming; the original zeroing is restored on unscope.
- Added a smooth zeroing transition (`AutoZeroTransitionTime`, default `0.35`s): on larger distance jumps the view eases to the new zero instead of snapping. `0` restores the hard jump.
- Added an optional predicted bullet trajectory preview (`ShowTrajectoryPreview`), a good way to build an intuitive feel for Tarkov's ballistics: bullet drop, travel time, and real dispersion at range. The line follows the measured distance and ends at the target, fading from a transparent near color to an opaque far color and widening with distance so the arc stays readable when viewed from behind the weapon.
- Added a dispersion ring at the trajectory impact point (`AutoZeroImpactSpreadCircle`): shows the maximum shot spread at the measured distance using the game's own formula (weapon accuracy, barrel durability, ammo factor, buffs, overheat). The cone is projected onto the hit surface (a circle on flat walls, a stretched ellipse on oblique surfaces) and drawn with a permanent depth test, so it can never be swallowed by the surface it sits on.
- Added AutoRanging compatibility: when the AutoRanging mod is installed, its ranging is automatically paused while `AutoZeroEnabled` is on, since the two mods would otherwise fight over the sight zeroing. AutoRanging keeps working normally whenever auto zero is off.
- Verified compatibility with BetterZeroing and ExtendedZeroRanges. Auto zero makes all three of AutoRanging, BetterZeroing, and ExtendedZeroRanges unnecessary, since it already zeroes more precisely than any of them; all three remain compatible if kept installed. Auto zero no longer restores a stale backup when the game or a mod regenerates the sight calibration points (for example on ammo changes with BetterZeroing installed) — it re-reads the regenerated values instead.
- The zeroing panel keeps the two auto zero modes visually distinct: continuous shows a static `auto` (no distance, since the in-scope readout already shows it live); hotkey shows just the applied distance, e.g. `412m`.

### Changed

- Settings menu (BepInEx ConfigurationManager) entries now show friendly names in a sensible order; the underlying `.cfg` keys are unchanged, so existing config files keep working.
- Source files reorganized into `Patches/`, `Component/`, and `Config/` folders for readability. No behavior changes.

## 2.0.0

### Added

- Added a new in-scope render path that draws the readout through the optic camera in view space, the same way the vanilla reticle is drawn.
- Added separate shipped preset and user override layout files. User overrides take priority over shipped presets.
- Added layout JSON versioning for the new preset/override file format.
- Added automatic mouse cursor unlock while the in-game layout editor is open.

### Fixed

- Fixed `ScopeFontName` changes not being applied to the in-scope readout. Font, text color, text offset, and background settings now apply live without a restart.
- Fixed the readout text and background plate scaling at different rates when changing the per-scope `Scale` value. Both now scale uniformly.
- Fixed the readout jittering sideways at high magnification far from the map origin. All previous in-scope render paths positioned the readout through world space, where float precision at large map coordinates is only about 0.1 mm; high scope zoom magnified that into visible pixel steps. The new render path uses an identity view matrix and small view-space coordinates, so the readout is rigidly locked to the optic image.
- Fixed TAA smearing the readout while panning. The readout is now drawn after the optic camera's post-processing, so temporal anti-aliasing never touches it and the scope image keeps its configured anti-aliasing.

### Changed

- The view-space command buffer render path is now the only in-scope render path.
- PiP-Disabler compatibility is now automatic through a simple fallback screen overlay.
- `ScopeRangefinder.presets.json` is now updated by builds/mod updates, while `ScopeRangefinder.layouts.json` is kept for user overrides.
- `OffsetX`, `OffsetY`, and `Scale` now default to `0` for the optic-camera display. `Scale = 0` means standard size.
- Scope placement is now controlled by per-scope layout JSON only.
- Missing or unsupported layout JSON versions are replaced with current defaults on startup.

### Removed

- Removed the legacy `ScreenSpaceCamera` and `ScopeMesh` render paths and the `ScopeDisplayMode` config entry; only the stable view-space path and the PiP-Disabler screen overlay fallback remain.
- Removed `ScopeAntialiasingOverride` and `SuppressOpticTaaJitter`. They are obsolete now that the readout is drawn after the optic camera's anti-aliasing; the optic camera keeps its vanilla AA behavior.
- Removed global scope offset/scale config entries.

### Notes

- This release contains breaking config and layout changes, so it is versioned as `2.0.0`.
- Existing user layout files from older versions are replaced if their layout version is missing or unsupported.
- Config entries from older versions (for example the removed `Optic Camera Modes` section) may remain in existing config files. They are ignored and can be removed manually.

## 1.1.0

### Added

- Added an in-scope rangefinder display that moves with the optic view.
- Added layout presets for vanilla magnified scopes.
- Added per-scope layout support through `ScopeRangefinder.layouts.json`.
- Added an in-game layout editor with live `OffsetX`, `OffsetY`, and `Scale` editing.
- Added a hotkey for the layout editor. The default is `F8`.
- Added buttons to save, reset, and copy the current scope key from the layout editor.
- Added optional Wilcox RAPTAR ES requirement.
- Added optional RAPTAR active-state requirement.
- Added configurable text and background colors with transparency support.
- Added optional optic-camera anti-aliasing override to reduce TAA ghosting.
- Added `ProjectedOverlay` scope render mode for setups where the optic-camera display jitters with TAA or DLSS.
- Added `ScopeFontName` so the readout font can be selected from installed OS fonts.
- Added configurable `ScopeWorldTextOffsetY` for font-dependent text alignment.
- Added a compatibility fallback for PiP-Disabler.

### Changed

- The default display now renders inside the active optic instead of using a fixed screen overlay.
- The BepInEx config menu has been reorganized into clearer sections.
- Legacy screen overlay offsets are now clearly marked and only apply when scope binding is disabled.
- Per-scope layouts now only use `OffsetX`, `OffsetY`, and `Scale`.
- The build output now installs the DLL and layout JSON into `BepInEx/plugins/maschine-ScopeRangefinder/`.
- The project layout JSON no longer overwrites an existing user layout file during builds.
- `ScopeWorldBackgroundHeight` now only changes the background plate height, not the projected overlay text size.
- Removed the old jitter tuning options that are no longer needed with `ProjectedOverlay`: `ScopeDisplaySmoothing`, `StabilizeTransparentScopeDisplay`, `DistanceDisplaySmoothing`, and `DistanceDisplayDeadband`.
- Reworked render mode selection into `ProjectedOverlay`, `ExperimentalInScopeCamera`, and `LegacyOverlay`, with `ProjectedOverlay` as the default.
- Reorganized the BepInEx menu into separate scope text, scope background, and experimental optic-camera sections.
- Added startup protection for upgrades from 1.0.0: the old root-level DLL is removed automatically when possible, or a clear conflict warning is shown.
- Disabled `ProjectedOverlay` when PiP-Disabler is detected; PiP-Disabler uses `LegacyOverlay` instead.

### Improved

- Improved readout sharpness.
- Improved visual style to feel closer to a device display.
- Improved behavior across variable zoom scopes.
- Reduced text trails when using TAA by allowing the optic camera to use FXAA.
- Mouse clicks inside the layout editor no longer fire the weapon.

### Notes

- If you already have an older config file, unused old entries may remain in it. They can be left alone or removed manually.
- If the readout leaves trails with TAA or DLSS enabled, try setting `ScopeAntialiasingOverride` to `FXAA`.
- If the in-scope display still jitters, try setting `ScopeRenderMode` to `ProjectedOverlay`.

## 1.0.0

- Initial release.
- Added a configurable rangefinder readout while aiming through magnified optics.
- Added distance updates, display delay, numeric format options, and screen overlay positioning.
