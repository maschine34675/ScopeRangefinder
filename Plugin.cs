using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.IO;
using UnityEngine;

namespace ScopeRangefinder
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(PiPDisablerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(AutoRangingCompat.AutoRangingGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.maschine.ScopeRangefinder";
        public const string PluginName = "maschine-ScopeRangefinder";
        public const string PluginVersion = "3.2.0";
        public const string PiPDisablerGuid = "com.fiodor.pipdisabler";
        private const string MilkorReflexSightTemplateId = "6284bd5f95250a29bc628a30";

        public static ManualLogSource LogSource;
        public static bool PiPDisablerLoaded;
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> MaxDistance;
        public static ConfigEntry<float> UpdateInterval;
        public static ConfigEntry<bool> UseDecimalFormat;
        public static ConfigEntry<DistanceUnit> DistanceUnit;
        public static ConfigEntry<bool> ShowUnitSuffix;
        public static ConfigEntry<string> NoDistanceText;
        public static ConfigEntry<bool> ShowZeroLine;
        public static ConfigEntry<BallisticsLineMode> BallisticsLine;
        public static ConfigEntry<HoldUnit> BallisticsHoldUnit;
        public static ConfigEntry<string> RangeLinePrefix;
        public static ConfigEntry<string> ZeroLinePrefix;
        public static ConfigEntry<float> MinZoomBlendFactor;
        public static ConfigEntry<float> MinDisplayDistance;
        public static ConfigEntry<float> DisplayShowDelay;
        public static ConfigEntry<Color> ScopeWorldTextColor;
        public static ConfigEntry<ScopeFontSource> ScopeFontSource;
        public static ConfigEntry<string> ScopeFontName;
        public static ConfigEntry<string> CustomFontFile;
        public static ConfigEntry<float> ScopeTextThickness;
        public static ConfigEntry<float> ScopeTextSpacing;
        public static ConfigEntry<float> ScopeTextGlow;
        public static ConfigEntry<float> ScopeTextOutline;
        public static ConfigEntry<float> ScopeTextAberration;
        public static ConfigEntry<float> ScopeWorldTextOffsetY;
        public static ConfigEntry<bool> ScopeWorldBackground;
        public static ConfigEntry<float> ScopeWorldBackgroundWidth;
        public static ConfigEntry<float> ScopeWorldBackgroundHeight;
        public static ConfigEntry<Color> ScopeWorldBackgroundColor;
        public static ConfigEntry<bool> AutoZeroEnabled;
        public static ConfigEntry<AutoZeroMode> AutoZeroMode;
        public static ConfigEntry<KeyboardShortcut> AutoZeroHotkey;
        public static ConfigEntry<float> AutoZeroTransitionTime;
        public static ConfigEntry<bool> ShowTrajectoryPreview;
        public static ConfigEntry<Color> AutoZeroTrajectoryNearColor;
        public static ConfigEntry<Color> AutoZeroTrajectoryFarColor;
        public static ConfigEntry<bool> AutoZeroImpactSpreadCircle;
        public static ConfigEntry<Color> AutoZeroSpreadCircleColor;
        public static ConfigEntry<KeyboardShortcut> LayoutEditorToggle;
        public static ConfigEntry<string> NonMagnifiedSights;
        public static ConfigEntry<bool> RequireWilcoxRaptar;
        public static ConfigEntry<bool> RequireWilcoxRaptarActive;
        public static ConfigEntry<bool> LogLoadedFonts;
        public static ConfigEntry<bool> LogScopeKeys;
        public static ConfigEntry<string> SelectedStylePreset;
        internal static ConfigEntry<string> ConfigVersion;
        internal static ScopeLayoutConfig ScopeLayouts;
        internal static ConfigFile ConfigInstance;
        internal static string LegacyDllConflictPath;
        private Harmony _harmony;

        private void Awake()
        {
            LogSource = Logger;
            ConfigInstance = Config;
            bool configExisted = File.Exists(Config.ConfigFilePath);

            if (!TryRemoveLegacyRootDll())
            {
                gameObject.AddComponent<LegacyDllConflictWarningGui>();
                return;
            }

            PiPDisablerLoaded = Chainloader.PluginInfos.ContainsKey(PiPDisablerGuid);

            Enabled = Config.Bind("General", "Enabled", true,
                Tagged("Enable Mod", 20, "Show distance readout while aiming through an optic scope."));
            SelectedStylePreset = Config.Bind("General", "StylePreset", "",
                new ConfigDescription(
                    "Last style preset applied to the global style. Presets are browsed and applied from the " +
                    "in-game rangefinder editor (F8). Shipped presets live in ScopeRangefinder.presets.json " +
                    "(replaced on update); presets you save land in ScopeRangefinder.styles.json, which updates " +
                    "never touch.",
                    null,
                    new ConfigurationManagerAttributes { Browsable = false }));
            Config.Bind("General", "ResetAllSettings", false,
                new ConfigDescription(
                    "Resets every setting of this mod to its default value. Requires a confirming second click.",
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = "Reset",
                        Order = -10,
                        HideDefaultButton = true,
                        CustomDrawer = SettingsResetDrawer.Draw
                    }));
            MaxDistance = Config.Bind("General", "MaxDistance", 1500f,
                Tagged("Max Measurement Distance (m)", 10, "Maximum raycast distance in meters."));
            UpdateInterval = Config.Bind("General", "UpdateInterval", 0.1f,
                Tagged("Distance Update Interval (s)", 0, "Seconds between distance measurements."));

            MinZoomBlendFactor = Config.Bind("Activation", "MinZoomBlendFactor", 0f,
                Tagged("Minimum Zoom To Activate", 30,
                    "Minimum scope zoom (0-1) before the readout appears. 0 = show as soon as the optic view is active."));
            DisplayShowDelay = Config.Bind("Activation", "ShowDelay", 0.2f,
                Tagged("Show Delay After Aiming (s)", 20, "Seconds to wait after entering the scope before showing the readout."));
            MinDisplayDistance = Config.Bind("Activation", "MinDisplayDistance", 0f,
                Tagged("Minimum Distance To Activate (m)", 25,
                    "Only show the readout when the target is at least this many meters away. 0 = use zoom activation instead (e.g. 50 for 50m)."));
            NonMagnifiedSights = Config.Bind(
                "Activation",
                "NonMagnifiedSights",
                MilkorReflexSightTemplateId,
                Tagged("Non-Magnified Sights", 10,
                    "Comma-separated item template IDs of non-magnified sights (red dots, holographics) that " +
                    "should also show the readout. They have no optic camera, so they use the screen overlay — " +
                    "position it per sight with the layout editor (F8). " +
                    "Default is the Milkor M2A1 reflex sight, so a grenade launcher aimed through it produces a " +
                    "measured distance other mods can read. Empty = magnified optics only."));
            RequireWilcoxRaptar = Config.Bind("Activation", "RequireWilcoxRaptar", false,
                Tagged("Require Wilcox RAPTAR Attached", 5,
                    "Only show the readout when the current weapon has a Wilcox RAPTAR ES Tactical Rangefinder attached."));
            RequireWilcoxRaptarActive = Config.Bind("Activation", "RequireWilcoxRaptarActive", true,
                Tagged("Require RAPTAR Switched On", 0,
                    "When RequireWilcoxRaptar is enabled, also require the attached RAPTAR tactical device to be switched on."));

            DistanceUnit = Config.Bind("Readout", "DistanceUnit", ScopeRangefinder.DistanceUnit.Meters,
                HiddenStyleEntry(
                    "Unit for the displayed distance, like the unit toggle on real rangefinders. " +
                    "Auto zero always works on the true metric distance regardless of this setting."));
            ShowUnitSuffix = Config.Bind("Readout", "ShowUnitSuffix", true,
                HiddenStyleEntry(
                    "Append the unit to the readout (e.g. 0123m / 0135yd). The vanilla RAPTAR shows bare digits."));
            UseDecimalFormat = Config.Bind("Readout", "UseDecimalFormat", false,
                HiddenStyleEntry("Use 000.0 format (Vortex-style) instead of 4-digit meters (RAPTAR-style)."));
            ShowZeroLine = Config.Bind("Readout", "ShowZeroLine", true,
                HiddenStyleEntry(
                    "Second readout line showing the currently effective zero: the auto-zeroed distance, " +
                    "'auto' in continuous mode, or the sight's dial distance when auto zero is off. " +
                    "The background plate grows automatically."));
            BallisticsLine = Config.Bind("Readout", "BallisticsLine", BallisticsLineMode.Off,
                HiddenStyleEntry(
                    "Third readout row with a firing solution for the loaded round at the measured distance. " +
                    "Hold shows the vertical hold versus the current dial zero (positive = hold above the target, like mil turrets); " +
                    "Dial recommends the best zeroing stop of the active sight plus the residual hold at that stop. " +
                    "Computed with the game's own ballistics, so ammo, weapon mods, and drag are all accounted for."));
            BallisticsHoldUnit = Config.Bind("Readout", "BallisticsHoldUnit", HoldUnit.Milliradians,
                HiddenStyleEntry(
                    "Unit for hold values on the ballistics line: milliradians (no suffix, mil-turret convention), " +
                    "minutes of angle, or centimeters at the measured distance."));
            RangeLinePrefix = Config.Bind("Readout", "RangeLinePrefix", "RNG",
                HiddenStyleEntry(
                    "Prefix for the measured distance line when the zeroing line is shown. Empty = no prefix."));
            ZeroLinePrefix = Config.Bind("Readout", "ZeroLinePrefix", "ZRO",
                HiddenStyleEntry("Prefix for the zeroing line. Empty = no prefix."));
            NoDistanceText = Config.Bind("Readout", "NoDistanceText", "----",
                HiddenStyleEntry("Text shown when no valid target is hit."));
            ScopeWorldTextColor = Config.Bind("Scope Text", "ScopeWorldTextColor",
                new Color(1f, 84f / 255f, 58f / 255f, 170f / 255f),
                HiddenStyleEntry("Color and transparency for the scope-bound text."));
            ScopeFontSource = Config.Bind("Scope Text", "ScopeFontSource", ScopeRangefinder.ScopeFontSource.CustomFont,
                HiddenStyleEntry(
                    "GameBender is the game's own Bender font, exactly as used by the RAPTAR display and most of the game UI. " +
                    "SystemFont uses the installed OS font selected under ScopeFontName. " +
                    "CustomFont loads the file selected under CustomFontFile from the plugin's fonts folder."));
            ScopeFontName = Config.Bind("Scope Text", "ScopeFontName", "Consolas",
                HiddenStyleEntry(
                    "Installed OS font for the readout, by family name as shown in Windows (e.g. 'Lucida Console') " +
                    "or by file name (e.g. 'lucon.ttf'). Machine-wide and per-user fonts are found. " +
                    "Only used when ScopeFontSource is set to SystemFont."));
            CustomFontFile = Config.Bind("Scope Text", "CustomFontFile", "DigitTech14-Italic.otf",
                HiddenStyleEntry(
                    "File name of a .ttf/.otf font or a TMP font asset bundle (filename:assetname) inside " +
                    "BepInEx/plugins/maschine-ScopeRangefinder/fonts/. Only used when ScopeFontSource is set to CustomFont. " +
                    "Picking a font in the rangefinder editor (F8) switches the font source automatically."));
            ScopeTextThickness = Config.Bind(
                "Scope Text",
                "ScopeTextThickness",
                0f,
                HiddenStyleEntry(
                    "Stroke weight of the readout text. Negative = thinner, positive = bolder.",
                    new AcceptableValueRange<float>(-0.4f, 0.4f)));
            ScopeTextSpacing = Config.Bind(
                "Scope Text",
                "ScopeTextSpacing",
                0f,
                HiddenStyleEntry(
                    "Extra spacing between characters. Useful for fonts with tight digit cells, like 7-segment fonts.",
                    new AcceptableValueRange<float>(-10f, 40f)));
            ScopeTextGlow = Config.Bind(
                "Scope Text",
                "ScopeTextGlow",
                0.1830986f,
                HiddenStyleEntry(
                    "Soft glow around the readout text in its own color, like an illuminated display. 0 = off. " +
                    "Requires an SDF font (all fonts except bitmap-baked asset bundles).",
                    new AcceptableValueRange<float>(0f, 1f)));
            ScopeTextOutline = Config.Bind(
                "Scope Text",
                "ScopeTextOutline",
                0f,
                HiddenStyleEntry(
                    "Black outline around the glyphs, for contrast against bright backgrounds. 0 = off. " +
                    "Requires an SDF font (all fonts except bitmap-baked asset bundles).",
                    new AcceptableValueRange<float>(0f, 0.4f)));
            ScopeTextAberration = Config.Bind(
                "Scope Text",
                "ScopeTextAberration",
                0f,
                HiddenStyleEntry(
                    "Chromatic aberration: color fringes on the readout, displaced in opposite directions along the " +
                    "radial axis from the scope center, like real lens dispersion. Fringe hues follow the text color " +
                    "(its spectral neighbors; red/cyan for white text). 0 = off. Requires an SDF font.",
                    new AcceptableValueRange<float>(0f, 1f)));
            ScopeWorldTextOffsetY = Config.Bind(
                "Scope Text",
                "ScopeWorldTextOffsetY",
                0.004f,
                HiddenStyleEntry(
                    "Vertical text offset inside the background plate. Useful because different fonts sit at different visual heights.",
                    new AcceptableValueRange<float>(-0.1f, 0.1f)));

            ScopeWorldBackground = Config.Bind("Scope Background", "ScopeWorldBackground", false,
                HiddenStyleEntry("Draw a small dark background plate behind the scope-bound readout."));
            ScopeWorldBackgroundWidth = Config.Bind("Scope Background", "ScopeWorldBackgroundWidth", 0.26f,
                HiddenStyleEntry("Width of the optional scope-bound background plate."));
            ScopeWorldBackgroundHeight = Config.Bind("Scope Background", "ScopeWorldBackgroundHeight", 0.11f,
                HiddenStyleEntry("Height of the optional scope-bound background plate."));
            ScopeWorldBackgroundColor = Config.Bind("Scope Background", "ScopeWorldBackgroundColor",
                new Color(0f, 0f, 0f, 41f / 255f),
                HiddenStyleEntry("Color and transparency for the optional scope-bound background plate."));

            AutoZeroEnabled = Config.Bind("Auto Zero", "AutoZeroEnabled", false,
                Tagged("Enable Auto Zero", 80,
                    "Zero the active optic to the measured distance, to the meter, accounting for the loaded ammo, weapon, and range. " +
                    "The original zeroing is restored whenever auto zero releases control."));
            AutoZeroMode = Config.Bind(
                "Auto Zero",
                "AutoZeroMode",
                ScopeRangefinder.AutoZeroMode.Hotkey,
                Tagged("Zeroing Mode", 70,
                    "Hotkey zeroes once per key press and keeps that zero until re-pressed, the zeroing dial is used manually, or the sight changes. " +
                    "Continuous follows the measured distance while aiming."));
            AutoZeroHotkey = Config.Bind(
                "Auto Zero",
                "AutoZeroHotkey",
                new KeyboardShortcut(KeyCode.J),
                Tagged("Zero Hotkey", 60, "Zeroes the optic to the currently measured distance."));
            AutoZeroTransitionTime = Config.Bind(
                "Auto Zero",
                "AutoZeroTransitionTime",
                0.35f,
                new ConfigDescription(
                    "Seconds to smoothly blend the zeroing to a new measured distance instead of snapping. 0 = instant.",
                    new AcceptableValueRange<float>(0f, 2f),
                    new ConfigurationManagerAttributes { DispName = "Zero Transition Time (s)", Order = 50 }));
            ShowTrajectoryPreview = Config.Bind("Auto Zero", "ShowTrajectoryPreview", false,
                Tagged("Show Trajectory & Impact Preview", 40,
                    "Draw the predicted bullet trajectory up to the measured distance, a great way to build an intuitive feel for " +
                    "Tushonka's ballistics: bullet drop, travel time, and the real dispersion at range."));
            AutoZeroTrajectoryNearColor = Config.Bind("Auto Zero", "AutoZeroTrajectoryNearColor", new Color(0f, 1f, 0.25f, 0.02f),
                Tagged("Trajectory Color (Near)", 30,
                    "Trajectory color at the muzzle. Keep the alpha low so the near segments do not block the view downrange."));
            AutoZeroTrajectoryFarColor = Config.Bind("Auto Zero", "AutoZeroTrajectoryFarColor", new Color(1f, 0.60f, 0f, 0.9f),
                Tagged("Trajectory Color (Far / Impact)", 20,
                    "Trajectory color at the far end. The line blends from the near color to this color with increasing distance."));
            AutoZeroImpactSpreadCircle = Config.Bind("Auto Zero", "AutoZeroImpactSpreadCircle", true,
                Tagged("Show Dispersion Ring", 10,
                    "Ring at the impact point showing the maximum shot dispersion at that distance. " +
                    "Uses the game's own formula: weapon accuracy, barrel durability, ammo factor, buffs, and overheat."));
            AutoZeroSpreadCircleColor = Config.Bind("Auto Zero", "AutoZeroSpreadCircleColor", new Color(1f, 0.25f, 0.1f, 0.85f),
                Tagged("Dispersion Ring Color", 0, "Color of the impact dispersion ring."));

            LayoutEditorToggle = Config.Bind(
                "General",
                "ToggleEditor",
                new KeyboardShortcut(KeyCode.F8),
                Tagged("Toggle Rangefinder Editor", 5,
                    "Hotkey to show or hide the in-game rangefinder editor: per-scope layout, " +
                    "style presets, and all style options."));

            LogScopeKeys = Config.Bind(
                "Developer",
                "LogScopeKeys",
                false,
                new ConfigDescription(
                    "Log the layout key of each scope when it is sighted, for editing per-scope overrides in " +
                    "ScopeRangefinder.layouts.json by hand. The layout editor also shows and copies the same key.",
                    null,
                    new ConfigurationManagerAttributes { DispName = "Log Scope Keys", Order = 10, IsAdvanced = true }));
            LogLoadedFonts = Config.Bind(
                "Developer",
                "LogLoadedFonts",
                false,
                new ConfigDescription(
                    "Log all loaded font assets (TextMeshPro and legacy) plus the RAPTAR display font to LogOutput.log " +
                    "once per session on the first scope use. Development aid for picking game fonts.",
                    null,
                    new ConfigurationManagerAttributes { DispName = "Log Loaded Fonts", Order = 0, IsAdvanced = true }));
            ConfigVersion = Config.Bind(
                "Developer",
                "ConfigVersion",
                "",
                new ConfigDescription(
                    "Last plugin version that ran with this config file; drives one-time migrations. Not meant to be edited.",
                    null,
                    new ConfigurationManagerAttributes { Browsable = false }));
            ScopeLayouts = ScopeLayoutConfig.LoadOrCreate();
            MigrateConfigIfNeeded(configExisted);
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            AutoRangingCompat.TryApply(_harmony);
            gameObject.AddComponent<ScopeRangefinderComponent>();

            if (PiPDisablerLoaded)
            {
                LogSource.LogInfo(
                    "PiP-Disabler detected. While it suppresses the vanilla optic camera, ScopeRangefinder " +
                    "uses the fallback screen overlay; bypassed or disabled scopes get the full in-scope readout.");
            }

            LogSource.LogInfo($"{PluginName} v{PluginVersion} loaded (build {BuildInfo.Timestamp}).");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private const string ShowcaseStylePreset = "LED Display Coral Red";
        private static void MigrateConfigIfNeeded(bool configExisted)
        {
            if (ConfigVersion.Value == PluginVersion)
            {
                return;
            }

            if (!configExisted)
            {
                if (StylePresets.IsBuiltin(ShowcaseStylePreset) && StylePresets.Apply(ShowcaseStylePreset))
                {
                    SelectedStylePreset.Value = ShowcaseStylePreset;
                }
                else
                {
                    LogSource.LogError(
                        $"Showcase style preset '{ShowcaseStylePreset}' is missing from the shipped presets; " +
                        "fresh install keeps the bind defaults.");
                }
            }
            else if (string.IsNullOrEmpty(ConfigVersion.Value))
            {
                const string backupName = "My Settings (pre-2.3.0)";
                if (!StylePresets.IsBuiltin(ShowcaseStylePreset))
                {
                    LogSource.LogError(
                        $"Showcase style preset '{ShowcaseStylePreset}' is missing from the shipped presets; " +
                        "skipping the showcase step.");
                }
                else if (StylePresets.SaveCurrent(backupName) && StylePresets.Apply(ShowcaseStylePreset))
                {
                    SelectedStylePreset.Value = ShowcaseStylePreset;
                    LogSource.LogInfo(
                        $"Updated from a pre-2.3.0 config: previous look saved as style preset '{backupName}', " +
                        $"showcase preset '{ShowcaseStylePreset}' applied.");
                }
            }

            ConfigVersion.Value = PluginVersion;
        }
        private static ConfigDescription Tagged(string displayName, int order, string description)
        {
            return new ConfigDescription(
                description,
                null,
                new ConfigurationManagerAttributes { DispName = displayName, Order = order });
        }
        private static ConfigDescription HiddenStyleEntry(
            string description,
            AcceptableValueBase acceptableValues = null)
        {
            return new ConfigDescription(
                description,
                acceptableValues,
                new ConfigurationManagerAttributes { Browsable = false });
        }

        private static bool TryRemoveLegacyRootDll()
        {
            string currentAssemblyPath = typeof(Plugin).Assembly.Location;
            string legacyRootPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "BepInEx",
                "plugins",
                "maschine-ScopeRangefinder.dll");

            if (!File.Exists(legacyRootPath)
                || string.Equals(
                    Path.GetFullPath(legacyRootPath),
                    Path.GetFullPath(currentAssemblyPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                File.Delete(legacyRootPath);
                LogSource.LogWarning($"Removed legacy ScopeRangefinder DLL from old install path: {legacyRootPath}");
                return true;
            }
            catch (Exception ex)
            {
                LegacyDllConflictPath = legacyRootPath;
                LogSource.LogError("============================================================");
                LogSource.LogError("CONFLICT: legacy ScopeRangefinder DLL detected!");
                LogSource.LogError($"Remove this old file manually: {legacyRootPath}");
                LogSource.LogError($"Automatic removal failed: {ex.Message}");
                LogSource.LogError("ScopeRangefinder has NOT been activated.");
                LogSource.LogError("============================================================");
                return false;
            }
        }
    }

    internal class LegacyDllConflictWarningGui : MonoBehaviour
    {
        private GUIStyle _style;

        private void Awake()
        {
            _style = new GUIStyle
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState { textColor = Color.red },
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
        }

        private void OnGUI()
        {
            float width = 760f;
            float height = 58f;
            string path = Plugin.LegacyDllConflictPath ?? "BepInEx/plugins/maschine-ScopeRangefinder.dll";
            GUI.Label(
                new Rect(Screen.width / 2f - width / 2f, 16f, width, height),
                $"CONFLICT: remove old ScopeRangefinder DLL from {path} - v{Plugin.PluginVersion} is inactive!",
                _style);
        }
    }
}
