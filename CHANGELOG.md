# Changelog

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
