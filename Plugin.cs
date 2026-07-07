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
        public const string PluginVersion = "2.1.0";
        public const string PiPDisablerGuid = "com.fiodor.pipdisabler";

        public static ManualLogSource LogSource;
        public static bool PiPDisablerLoaded;
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> MaxDistance;
        public static ConfigEntry<float> UpdateInterval;
        public static ConfigEntry<bool> UseDecimalFormat;
        public static ConfigEntry<string> NoDistanceText;
        public static ConfigEntry<float> MinZoomBlendFactor;
        public static ConfigEntry<float> MinDisplayDistance;
        public static ConfigEntry<float> DisplayOffsetX;
        public static ConfigEntry<float> DisplayOffsetY;
        public static ConfigEntry<float> DisplayShowDelay;
        public static ConfigEntry<Color> ScopeWorldTextColor;
        public static ConfigEntry<string> ScopeFontName;
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
        public static ConfigEntry<bool> RequireWilcoxRaptar;
        public static ConfigEntry<bool> RequireWilcoxRaptarActive;
        internal static ScopeLayoutConfig ScopeLayouts;
        internal static string LegacyDllConflictPath;
        private Harmony _harmony;

        private void Awake()
        {
            LogSource = Logger;

            if (!TryRemoveLegacyRootDll())
            {
                gameObject.AddComponent<LegacyDllConflictWarningGui>();
                return;
            }

            PiPDisablerLoaded = Chainloader.PluginInfos.ContainsKey(PiPDisablerGuid);

            Enabled = Config.Bind("General", "Enabled", true,
                Tagged("Enable Mod", 20, "Show distance readout while aiming through an optic scope."));
            MaxDistance = Config.Bind("General", "MaxDistance", 1500f,
                Tagged("Max Measurement Distance (m)", 10, "Maximum raycast distance in meters."));
            UpdateInterval = Config.Bind("General", "UpdateInterval", 0.1f,
                Tagged("Distance Update Interval (s)", 0, "Seconds between distance measurements."));

            MinZoomBlendFactor = Config.Bind("Activation", "MinZoomBlendFactor", 0.3f,
                Tagged("Minimum Zoom To Activate", 30,
                    "Minimum scope zoom (0-1) before the readout appears. 0 = show as soon as the optic view is active."));
            MinDisplayDistance = Config.Bind("Activation", "MinDisplayDistance", 0f,
                Tagged("Minimum Distance To Activate (m)", 20,
                    "Only show the readout when the target is at least this many meters away. 0 = use zoom activation instead (e.g. 50 for 50m)."));
            RequireWilcoxRaptar = Config.Bind("Activation", "RequireWilcoxRaptar", false,
                Tagged("Require Wilcox RAPTAR Attached", 10,
                    "Only show the readout when the current weapon has a Wilcox RAPTAR ES Tactical Rangefinder attached."));
            RequireWilcoxRaptarActive = Config.Bind("Activation", "RequireWilcoxRaptarActive", true,
                Tagged("Require RAPTAR Switched On", 0,
                    "When RequireWilcoxRaptar is enabled, also require the attached RAPTAR tactical device to be switched on."));

            DisplayShowDelay = Config.Bind("Readout", "ShowDelay", 0.2f,
                Tagged("Show Delay After Aiming (s)", 20, "Seconds to wait after entering the scope before showing the readout."));
            UseDecimalFormat = Config.Bind("Readout", "UseDecimalFormat", false,
                Tagged("Use Decimal Format (045.0)", 10, "Use 000.0 format (Vortex-style) instead of 4-digit meters (RAPTAR-style)."));
            NoDistanceText = Config.Bind("Readout", "NoDistanceText", "----",
                Tagged("No-Target Text", 0, "Text shown when no valid target is hit."));

            ScopeWorldTextColor = Config.Bind("Scope Text", "ScopeWorldTextColor", new Color(0f, 1f, 0f, 1f),
                Tagged("Text Color", 20, "Color and transparency for the scope-bound text."));
            ScopeFontName = Config.Bind("Scope Text", "ScopeFontName", "Consolas",
                Tagged("Font Name", 10, "Preferred installed OS font for the readout."));
            ScopeWorldTextOffsetY = Config.Bind(
                "Scope Text",
                "ScopeWorldTextOffsetY",
                0.007f,
                new ConfigDescription(
                    "Vertical text offset inside the background plate. Useful because different fonts sit at different visual heights.",
                    new AcceptableValueRange<float>(-0.1f, 0.1f),
                    new ConfigurationManagerAttributes { DispName = "Text Vertical Offset", Order = 0 }));

            ScopeWorldBackground = Config.Bind("Scope Background", "ScopeWorldBackground", true,
                Tagged("Enable Background Plate", 30, "Draw a small dark background plate behind the scope-bound readout."));
            ScopeWorldBackgroundWidth = Config.Bind("Scope Background", "ScopeWorldBackgroundWidth", 0.26f,
                Tagged("Background Width", 20, "Width of the optional scope-bound background plate."));
            ScopeWorldBackgroundHeight = Config.Bind("Scope Background", "ScopeWorldBackgroundHeight", 0.11f,
                Tagged("Background Height", 10, "Height of the optional scope-bound background plate."));
            ScopeWorldBackgroundColor = Config.Bind("Scope Background", "ScopeWorldBackgroundColor", new Color(0.03f, 0.10f, 0.03f, 0.35f),
                Tagged("Background Color", 0, "Color and transparency for the optional scope-bound background plate."));

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
                    "Tarkov's ballistics: bullet drop, travel time, and the real dispersion at range."));
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
                "Layout Editor",
                "ToggleEditor",
                new KeyboardShortcut(KeyCode.F8),
                Tagged("Toggle Layout Editor", 0, "Hotkey to show or hide the in-game scope layout editor."));

            DisplayOffsetX = Config.Bind("Legacy Screen Overlay", "OffsetX", 0f,
                Tagged("Overlay Offset X (px)", 10, "Automatic PiP-Disabler fallback only: horizontal offset in pixels."));
            DisplayOffsetY = Config.Bind("Legacy Screen Overlay", "OffsetY", 0f,
                Tagged("Overlay Offset Y (px)", 0, "Automatic PiP-Disabler fallback only: vertical offset in pixels."));
            ScopeLayouts = ScopeLayoutConfig.LoadOrCreate();
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            AutoRangingCompat.TryApply(_harmony);
            gameObject.AddComponent<ScopeRangefinderComponent>();

            if (PiPDisablerLoaded)
            {
                LogSource.LogWarning(
                    "PiP-Disabler detected. Vanilla optic camera is disabled while scoped; " +
                    "ScopeRangefinder will use the fallback screen overlay.");
            }

            LogSource.LogInfo($"{PluginName} v{PluginVersion} loaded (build {BuildInfo.Timestamp}).");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private static ConfigDescription Tagged(string displayName, int order, string description)
        {
            return new ConfigDescription(
                description,
                null,
                new ConfigurationManagerAttributes { DispName = displayName, Order = order });
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
