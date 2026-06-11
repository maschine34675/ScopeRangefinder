maschine-ScopeRangefinder

Adds a built-in laser rangefinder readout to any magnified optic scope in SPT — similar to the Wilcox RAPTAR ES or Vortex Ranger 1500, but integrated directly into your scope view. No extra item required.



Features

Universal scope support — works with all optic scopes (IsOptic), not tied to specific scope prefabs

Distance readout — raycast-based measurement from the optic camera crosshair

LCD-style display — compact green readout with a dark background panel, positioned below the reticle

Flexible activation

By zoom level (MinZoomBlendFactor)

By target distance (MinDisplayDistance, e.g. only show from 50 m)

Configurable display

RAPTAR-style (0123) or Vortex-style (045.0) number format

Adjustable position via built-in defaults + config offsets

Show delay to sync with scope zoom animation

Lightweight — no Harmony patches, minimal performance impact (one raycast every 0.1 s while scoped)

Requirements

SPT with BepInEx

Client-side mod only

Installation

Build or download maschine-ScopeRangefinder.dll

Place it in BepInEx/plugins/

Start SPT — check BepInEx/LogOutput.log for: maschine-ScopeRangefinder v1.0.0 loaded.

Configuration

Config file: BepInEx/config/com.maschine.ScopeRangefinder.cfg



Section	Key	Default	Description

General

Enabled

true

Enable/disable the mod

General

MaxDistance

1500

Max raycast range (meters)

General

UpdateInterval

0.1

Seconds between measurements

Activation

MinZoomBlendFactor

0.3

Min zoom before showing (0 = as soon as optic is active)

Activation

MinDisplayDistance

0

Only show when target ≥ X meters (0 = disabled)

Display

ShowDelay

0.2

Delay after entering scope before readout appears

Display

OffsetX / OffsetY

0

Extra pixel offset added to built-in position

Display

UseDecimalFormat

false

false = 0123, true = 045.0

Display

NoDistanceText

\----

Text when no target is hit

Note: OffsetX / OffsetY are additional offsets. The built-in default position is hardcoded in the mod.



What it does not do

No automatic zeroing / elevation adjustment

Not embedded in individual scope 3D models (screen overlay approach for universal compatibility)

Red dot / holographic / iron sights are not affected

