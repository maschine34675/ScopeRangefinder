# Bundled font sources and licenses

This directory is the font manifest for release packages. It contains exactly
the 14 external font files referenced by `ScopeRangefinder.presets.json`, plus
their required license and archive-information files. `RAPTAR EFT Style` uses
EFT's built-in GameBender font and therefore requires no redistributed font.

The larger `fonts-alle` working pool is intentionally not part of the release.
`ScopeRangefinder.csproj` packages only this `fonts` directory. All binaries
listed here are redistributed unchanged.

Colors below use the presets' `RRGGBBAA` text/background values. “Background
off” means that the color remains configured while the background is disabled.

## Release font set

| Font file | Preset and palette | Purpose | Source and license |
| --- | --- | --- | --- |
| `DigitTech14-Italic.otf` | `LED Display Coral Red` — `#FF543AAA` / `#00000029` (background off) | Slanted 14-segment rangefinder display | [Digit Tech](https://ggbot.itch.io/digit-tech-font), CC0 1.0 — `DigitTech-LICENSE.txt` |
| `vcr-osd-replayed.ttf` | `VCR Chromatic` — `#F4F8FFFF` / `#02070A52` (background off) | VCR/camcorder OSD with chromatic aberration | [VCR OSD Replayed](https://fontstruct.com/fontstructions/show/2738536/vcr-osd-replayed), OFL 1.1 — `VCR-OSD-Replayed-LICENSE.txt` and `VCR-OSD-Replayed-README.txt` |
| `DSEG7ClassicMini-Italic.ttf` | `DSEG7 Mini RGB Split` — `#F4F8FFFF` / `#02070A52` (background off) | Compact seven-segment RGB-split display | [DSEG 0.46](https://github.com/keshikan/DSEG/releases/tag/v0.46), OFL 1.1 — `DSEG-LICENSE.txt` |
| `B612Mono-Regular.ttf` | `B612 Cockpit Phosphor` — `#A7FF83E1` / `#07160759` | Aircraft-cockpit-inspired phosphor display | [B612 1.008](https://github.com/polarsys/b612/tree/1.008), OFL 1.1 — `B612Mono-LICENSE.txt` |
| `DSEG14ClassicMini-Regular.ttf` | `DSEG14 Classic Amber` — `#FF6100FF` / `#1A120359` | Compact classic 14-segment display | [DSEG 0.46](https://github.com/keshikan/DSEG/releases/tag/v0.46), OFL 1.1 — `DSEG-LICENSE.txt` |
| `DigitTech16-Regular.otf` | `DT16 Cyrillic` — `#FF7300D4` / `#00000029` (background off) | Segmented display for the uppercase labels `РАСТ` and `НОЛЬ` | [Digit Tech](https://ggbot.itch.io/digit-tech-font), CC0 1.0 — `DigitTech-LICENSE.txt` |
| `LCD14Condensed.otf` | `LCD14 Starburst Red` — `#FF4B36FF` / `#1A050459` | Narrow 14-segment starburst display | [lcd-font at commit `686587f`](https://github.com/ctrlcctrlv/lcd-font/tree/686587fa2876c64878bf625d0a3de04331d9e58d), OFL 1.1 — `LCD14-LICENSE.txt` |
| `Oxanium-Medium.ttf` | `Oxanium HUD Cyan` — `#67E8FFFF` / `#05141A57` | Readable futuristic HUD face | [Oxanium 2.000](https://github.com/sevmeyer/oxanium/tree/2.000), OFL 1.1 — `Oxanium-LICENSE.txt` |
| `Quantico-Regular.ttf` | `Quantico Tactical Amber` — `#FFBD66AA` / `#1A100559` | Angular military-inspired lettering | [Google Fonts at commit `90abd17`](https://github.com/google/fonts/tree/90abd17b4f97671435798b6147b698aa9087612f/ofl/quantico), OFL 1.1 — `Quantico-LICENSE.txt` |
| `Rajdhani-Regular.ttf` | `Rajdhani Tech Chartreuse` — `#CBFF75FF` / `#10180759` | Compact squared technical lettering | [Google Fonts at commit `9d1ce2f`](https://github.com/google/fonts/tree/9d1ce2fc3c335cca32b6db00c19f55d57b0a68fe/ofl/rajdhani), OFL 1.1 — `Rajdhani-LICENSE.txt` |
| `ShareTechMono-Regular.ttf` | `Tech Mono Ice` — `#D8ECFFFF` / `#0F0F0F59` | Clean technical monospace display | [Google Fonts](https://github.com/google/fonts/tree/main/ofl/sharetechmono), OFL 1.1 — `ShareTechMono-LICENSE.txt` |
| `VT323-Regular.ttf` | `Terminal Green` — `#00FF41FF` / `#05140566` | CRT/terminal-style display | [Google Fonts](https://github.com/google/fonts/tree/main/ofl/vt323), OFL 1.1 — `VT323-LICENSE.txt` |
| `DigitTech7-Italic.otf` | `RAPTAR Lite ES` — `#3DFF3CFF` / `#00000041` | Hardware-like seven-segment readout | [Digit Tech](https://ggbot.itch.io/digit-tech-font), CC0 1.0 — `DigitTech-LICENSE.txt` |
| `HomeVideo-Regular.ttf` | `RAPTAR S` — `#FFFFFFFF` / `#00000053` | Compact VHS/VCR display face | [Home Video 0.8](https://ggbot.itch.io/home-video-font), CC0 1.0 — `HomeVideo-LICENSE.txt` |

## Required accompanying files

The release includes the following license or archive-information files:

- `B612Mono-LICENSE.txt`
- `DigitTech-LICENSE.txt`
- `DSEG-LICENSE.txt`
- `HomeVideo-LICENSE.txt`
- `LCD14-LICENSE.txt`
- `Oxanium-LICENSE.txt`
- `Quantico-LICENSE.txt`
- `Rajdhani-LICENSE.txt`
- `ShareTechMono-LICENSE.txt`
- `VCR-OSD-Replayed-LICENSE.txt`
- `VCR-OSD-Replayed-README.txt`
- `VT323-LICENSE.txt`

## SHA-256

```text
B612Mono-Regular.ttf b98cb96cc8a6206dae08c063d60902df7e6d40f86139ebdb97256704253c9c69
DigitTech14-Italic.otf 083485382f5ce496eb7fef37d818c3f8f7b6d1aef4a36ed7382f15be2677eb86
DigitTech16-Regular.otf 2f66be7364c370fcfce2e38d30b129de3a2bb5728fab91a2e0cacc6df13c5055
DigitTech7-Italic.otf b0db6d1372f705922485ec003a3cee06f7de64ee23067639bd14e34d8278dd2e
DSEG14ClassicMini-Regular.ttf fe156bdd112465c8e87f709016e0616fb9fce5a0bbee90e56b42573a5fb02fb0
DSEG7ClassicMini-Italic.ttf 87e035904811308d22306ed3afb1b96f0de631fb59f8e46c6662181df4d24d54
HomeVideo-Regular.ttf e73d67f92457bd05d24c42f43156bc422e98982e9e62d267ad6d1a3cc595bb01
LCD14Condensed.otf ff4ac145cebb8c83e56d2bc39d281c7d2504f410f63f4becb6ffc5af6317198b
Oxanium-Medium.ttf d0676de4894cd22591b4bb538dae5b8e06c44e0fb943300a7cff3945fe643689
Quantico-Regular.ttf 7f27dfb0658914ac570bf1da36a2527f10eefd41d25a8f9603d52957d61c075d
Rajdhani-Regular.ttf 6e1fc228a8318251a6e569502ec57bac1e4656c582f92f59ccecc4688e039b98
ShareTechMono-Regular.ttf 9ceab1f87414829af259c0f537573ae03ef7dd3147c0b27a36a1a0beb6732677
vcr-osd-replayed.ttf d257ab9504c62ea20d89f20c88e53bb485fd5139f22d97abbc330c86438d7dc6
VT323-Regular.ttf cf4de751ada78ceac033dbe16a687742939995b77bc2a052ae17a4957958594d
```
