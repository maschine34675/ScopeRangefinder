using BepInEx;
using System;
using System.IO;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ScopeRangefinder
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(PiPDisablerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.maschine.ScopeRangefinder";
        public const string PluginName = "maschine-ScopeRangefinder";
        public const string PluginVersion = "1.1.0";
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
        public static ConfigEntry<ScopeRenderMode> ScopeRenderMode;
        public static ConfigEntry<float> ScopeLocalOffsetX;
        public static ConfigEntry<float> ScopeLocalOffsetY;
        public static ConfigEntry<float> ScopeWorldScale;
        public static ConfigEntry<Color> ScopeWorldTextColor;
        public static ConfigEntry<string> ScopeFontName;
        public static ConfigEntry<float> ScopeWorldTextOffsetY;
        public static ConfigEntry<bool> ScopeCompensateZoomScale;
        public static ConfigEntry<bool> ScopeWorldBackground;
        public static ConfigEntry<float> ScopeWorldBackgroundWidth;
        public static ConfigEntry<float> ScopeWorldBackgroundHeight;
        public static ConfigEntry<Color> ScopeWorldBackgroundColor;
        public static ConfigEntry<ScopeAntialiasingOverrideMode> ScopeAntialiasingOverride;
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
            ScopeRenderMode defaultRenderMode = PiPDisablerLoaded
                ? global::ScopeRangefinder.ScopeRenderMode.LegacyOverlay
                : global::ScopeRangefinder.ScopeRenderMode.ProjectedOverlay;

            Enabled = Config.Bind("General", "Enabled", true,
                "Show distance readout while aiming through an optic scope.");
            MaxDistance = Config.Bind("General", "MaxDistance", 1500f,
                "Maximum raycast distance in meters.");
            UpdateInterval = Config.Bind("General", "UpdateInterval", 0.1f,
                "Seconds between distance measurements.");
            MinZoomBlendFactor = Config.Bind("Activation", "MinZoomBlendFactor", 0.3f,
                "Minimum scope zoom (0-1) before the readout appears. 0 = show as soon as the optic view is active.");
            MinDisplayDistance = Config.Bind("Activation", "MinDisplayDistance", 0f,
                "Only show the readout when the target is at least this many meters away. 0 = use zoom activation instead (e.g. 50 for 50m).");
            DisplayShowDelay = Config.Bind("Readout", "ShowDelay", 0.2f,
                "Seconds to wait after entering the scope before showing the readout.");
            UseDecimalFormat = Config.Bind("Readout", "UseDecimalFormat", false,
                "Use 000.0 format (Vortex-style) instead of 4-digit meters (RAPTAR-style).");
            NoDistanceText = Config.Bind("Readout", "NoDistanceText", "----",
                "Text shown when no valid target is hit.");
            ScopeRenderMode = Config.Bind("Scope Display", "ScopeRenderMode", defaultRenderMode,
                "ProjectedOverlay is recommended unless PiP-Disabler is installed. ExperimentalInScopeCamera renders into the optic camera. LegacyOverlay uses the old fixed screen overlay.");
            ScopeLocalOffsetX = Config.Bind("Scope Display", "ScopeLocalOffsetX", -0.022f,
                "Local scope-anchor offset in meters, right/left relative to the active optic transform.");
            ScopeLocalOffsetY = Config.Bind("Scope Display", "ScopeLocalOffsetY", -0.014f,
                "Local scope-anchor offset in meters, up/down relative to the active optic transform.");
            ScopeWorldScale = Config.Bind("Scope Display", "ScopeWorldScale", 0.05f,
                "Global scale for the scope-bound readout.");
            ScopeCompensateZoomScale = Config.Bind("Experimental InScopeCamera", "ScopeCompensateZoomScale", true,
                "ExperimentalInScopeCamera only: keep the readout roughly the same apparent size while the optic field of view changes.");
            ScopeWorldTextColor = Config.Bind("Scope Text", "ScopeWorldTextColor", new Color(0f, 1f, 0f, 1f),
                "Color and transparency for the scope-bound text.");
            ScopeFontName = Config.Bind("Scope Text", "ScopeFontName", "Consolas",
                "Preferred installed OS font for the readout.");
            ScopeWorldTextOffsetY = Config.Bind(
                "Scope Text",
                "ScopeWorldTextOffsetY",
                0.007f,
                new ConfigDescription(
                    "Vertical text offset inside the background plate. Useful because different fonts sit at different visual heights.",
                    new AcceptableValueRange<float>(-0.1f, 0.1f)));
            ScopeWorldBackground = Config.Bind("Scope Background", "ScopeWorldBackground", true,
                "Draw a small dark background plate behind the scope-bound readout.");
            ScopeWorldBackgroundWidth = Config.Bind("Scope Background", "ScopeWorldBackgroundWidth", 0.28f,
                "Width of the optional scope-bound background plate.");
            ScopeWorldBackgroundHeight = Config.Bind("Scope Background", "ScopeWorldBackgroundHeight", 0.12f,
                "Height of the optional scope-bound background plate.");
            ScopeWorldBackgroundColor = Config.Bind("Scope Background", "ScopeWorldBackgroundColor", new Color(0.03f, 0.10f, 0.03f, 0.35f),
                "Color and transparency for the optional scope-bound background plate.");
            ScopeAntialiasingOverride = Config.Bind(
                "Experimental InScopeCamera",
                "ScopeAntialiasingOverride",
                ScopeAntialiasingOverrideMode.Off,
                "ExperimentalInScopeCamera only: optional override for the optic camera. FXAA/None can reduce TAA ghosting, but DLSS may still ghost the final image.");
            LayoutEditorToggle = Config.Bind(
                "Layout Editor",
                "ToggleEditor",
                new KeyboardShortcut(KeyCode.F8),
                "Hotkey to show or hide the in-game scope layout editor.");
            DisplayOffsetX = Config.Bind("Legacy Screen Overlay", "OffsetX", 0f,
                "LegacyOverlay only: horizontal offset in pixels.");
            DisplayOffsetY = Config.Bind("Legacy Screen Overlay", "OffsetY", 0f,
                "LegacyOverlay only: vertical offset in pixels.");
            RequireWilcoxRaptar = Config.Bind("Activation", "RequireWilcoxRaptar", false,
                "Only show the readout when the current weapon has a Wilcox RAPTAR ES Tactical Rangefinder attached.");
            RequireWilcoxRaptarActive = Config.Bind("Activation", "RequireWilcoxRaptarActive", true,
                "When RequireWilcoxRaptar is enabled, also require the attached RAPTAR tactical device to be switched on.");

            ScopeLayouts = ScopeLayoutConfig.LoadOrCreate();
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            gameObject.AddComponent<ScopeRangefinderComponent>();

            if (PiPDisablerLoaded)
            {
                LogSource.LogWarning(
                    "PiP-Disabler detected. Vanilla optic camera is disabled while scoped; " +
                    "ProjectedOverlay is disabled and ScopeRangefinder will use LegacyOverlay if needed.");
            }

            LogSource.LogInfo($"{PluginName} v{PluginVersion} loaded.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
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
                $"CONFLICT: remove old ScopeRangefinder DLL from {path} - v1.1.0 is inactive!",
                _style);
        }
    }
}
