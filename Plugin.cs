using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace ScopeRangefinder
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.maschine.ScopeRangefinder";
        public const string PluginName = "maschine-ScopeRangefinder";
        public const string PluginVersion = "1.0.0";

        public static ManualLogSource LogSource;
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

        private void Awake()
        {
            LogSource = Logger;

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
            DisplayOffsetX = Config.Bind("Display", "OffsetX", 0f,
                "Additional horizontal offset in pixels (added to the built-in default position).");
            DisplayOffsetY = Config.Bind("Display", "OffsetY", 0f,
                "Additional vertical offset in pixels (added to the built-in default position).");
            DisplayShowDelay = Config.Bind("Display", "ShowDelay", 0.2f,
                "Seconds to wait after entering the scope before showing the readout.");
            UseDecimalFormat = Config.Bind("Display", "UseDecimalFormat", false,
                "Use 000.0 format (Vortex-style) instead of 4-digit meters (RAPTAR-style).");
            NoDistanceText = Config.Bind("Display", "NoDistanceText", "----",
                "Text shown when no valid target is hit.");

            gameObject.AddComponent<ScopeRangefinderComponent>();
            LogSource.LogInfo($"{PluginName} v{PluginVersion} loaded.");
        }
    }
}
