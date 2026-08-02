using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace ScopeRangefinder
{
    internal static class SettingsResetDrawer
    {
        private const float ConfirmWindowSeconds = 3f;

        private static float _armedUntil;

        public static void Draw(ConfigEntryBase entry)
        {
            bool armed = Time.realtimeSinceStartup < _armedUntil;

            Color previousColor = GUI.color;
            if (armed)
            {
                GUI.color = Color.red;
            }

            if (GUILayout.Button(
                    armed ? "Click again to confirm reset!" : "Reset All Settings To Defaults",
                    GUILayout.ExpandWidth(true)))
            {
                if (armed)
                {
                    _armedUntil = 0f;
                    ResetAllSettings();
                }
                else
                {
                    _armedUntil = Time.realtimeSinceStartup + ConfirmWindowSeconds;
                }
            }

            GUI.color = previousColor;
        }

        private static void ResetAllSettings()
        {
            ConfigFile config = Plugin.ConfigInstance;
            if (config == null)
            {
                return;
            }
            bool previousSaveOnSet = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            try
            {
                foreach (ConfigDefinition definition in new List<ConfigDefinition>(config.Keys))
                {
                    if (definition.Section == "Developer" && definition.Key == "ConfigVersion")
                    {
                        continue;
                    }

                    ConfigEntryBase configEntry = config[definition];
                    configEntry.BoxedValue = configEntry.DefaultValue;
                }
            }
            finally
            {
                config.SaveOnConfigSet = previousSaveOnSet;
            }

            config.Save();
            Plugin.LogSource?.LogInfo("All settings were reset to their defaults.");
        }
    }
}
