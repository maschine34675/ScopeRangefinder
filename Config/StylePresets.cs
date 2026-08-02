using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using Newtonsoft.Json;

namespace ScopeRangefinder
{
    internal static class StylePresets
    {
        private static readonly string[] CoveredSections = { "Readout", "Scope Text", "Scope Background" };
        private static readonly string[] ExcludedKeys = { "FontPreview" };

        public static string UserStylesPath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "BepInEx",
            "plugins",
            "maschine-ScopeRangefinder",
            "ScopeRangefinder.styles.json");

        private sealed class ShippedPresetsSection
        {
            public Dictionary<string, Dictionary<string, string>> Styles { get; set; }
        }
        private static Dictionary<string, Dictionary<string, string>> _cachedShippedPresets;
        public static void InvalidateShippedCache()
        {
            _cachedShippedPresets = null;
        }

        private sealed class UserStylesFile
        {
            public int? Version { get; set; }
            public Dictionary<string, Dictionary<string, string>> Styles { get; set; }
        }

        public static string[] ListPresetNames()
        {
            var names = new List<string>(LoadShippedPresets().Keys);
            var seen = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

            foreach (string name in LoadUserStyles().Styles.Keys)
            {
                if (seen.Add(name))
                {
                    names.Add(name);
                }
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names.ToArray();
        }

        public static bool IsBuiltin(string name)
        {
            return LoadShippedPresets().ContainsKey(name);
        }

        public static bool Apply(string name)
        {
            if (LoadShippedPresets().TryGetValue(name, out Dictionary<string, string> shippedValues))
            {
                return ApplyValues(name, shippedValues);
            }

            return LoadUserStyles().Styles.TryGetValue(name, out Dictionary<string, string> userValues)
                ? ApplyValues(name, userValues)
                : ApplyValues(name, null);
        }

        public static bool SaveCurrent(string name)
        {
            if (IsBuiltin(name))
            {
                Plugin.LogSource?.LogWarning(
                    $"Style preset name '{name}' is reserved by a shipped preset and cannot be saved over.");
                return false;
            }

            ConfigFile config = Plugin.ConfigInstance;
            var values = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (ConfigDefinition definition in new List<ConfigDefinition>(config.Keys))
            {
                if (!IsCovered(definition))
                {
                    continue;
                }

                ConfigEntryBase entry = config[definition];
                values[$"{definition.Section}.{definition.Key}"] =
                    TomlTypeConverter.ConvertToString(entry.BoxedValue, entry.SettingType);
            }

            UserStylesFile file = LoadUserStyles();
            file.Styles[name] = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
            if (!WriteUserStyles(file))
            {
                return false;
            }

            Plugin.LogSource?.LogInfo($"Saved style preset '{name}'.");
            return true;
        }

        public static bool Delete(string name)
        {
            if (IsBuiltin(name))
            {
                return false;
            }

            UserStylesFile file = LoadUserStyles();
            if (!file.Styles.Remove(name))
            {
                return false;
            }

            if (!WriteUserStyles(file))
            {
                return false;
            }

            Plugin.LogSource?.LogInfo($"Deleted style preset '{name}'.");
            return true;
        }

        private static bool ApplyValues(string name, Dictionary<string, string> values)
        {
            if (values == null)
            {
                Plugin.LogSource?.LogWarning($"Style preset '{name}' does not exist or is not a JSON object.");
                return false;
            }

            ConfigFile config = Plugin.ConfigInstance;
            bool previousSaveOnSet = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            int invalidValues = 0;
            try
            {
                foreach (ConfigDefinition definition in new List<ConfigDefinition>(config.Keys))
                {
                    if (!IsCovered(definition))
                    {
                        continue;
                    }

                    ConfigEntryBase entry = config[definition];
                    if (!values.TryGetValue($"{definition.Section}.{definition.Key}", out string serialized))
                    {
                        entry.BoxedValue = entry.DefaultValue;
                        continue;
                    }

                    try
                    {
                        entry.BoxedValue = TomlTypeConverter.ConvertToValue(serialized, entry.SettingType);
                    }
                    catch (Exception exception)
                    {
                        entry.BoxedValue = entry.DefaultValue;
                        invalidValues++;
                        Plugin.LogSource?.LogWarning(
                            $"Style preset '{name}': invalid value '{serialized}' for " +
                            $"{definition.Section}.{definition.Key}: {exception.Message}");
                    }
                }
            }
            finally
            {
                config.SaveOnConfigSet = previousSaveOnSet;
            }

            config.Save();
            if (invalidValues > 0)
            {
                Plugin.LogSource?.LogWarning(
                    $"Applied style preset '{name}' with {invalidValues} invalid value(s) reset to defaults.");
            }
            else
            {
                Plugin.LogSource?.LogInfo($"Applied style preset '{name}'.");
            }

            return true;
        }

        private static Dictionary<string, Dictionary<string, string>> LoadShippedPresets()
        {
            if (_cachedShippedPresets != null)
            {
                return _cachedShippedPresets;
            }

            try
            {
                if (!File.Exists(ScopeLayoutConfig.PresetPath))
                {
                    return _cachedShippedPresets = EmptyStyles();
                }

                ShippedPresetsSection parsed = JsonConvert.DeserializeObject<ShippedPresetsSection>(
                    File.ReadAllText(ScopeLayoutConfig.PresetPath));
                return _cachedShippedPresets = parsed?.Styles == null
                    ? EmptyStyles()
                    : new Dictionary<string, Dictionary<string, string>>(parsed.Styles, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception exception)
            {
                Plugin.LogSource?.LogWarning($"Could not load shipped style presets: {exception.Message}");
                return _cachedShippedPresets = EmptyStyles();
            }
        }

        private static UserStylesFile LoadUserStyles()
        {
            try
            {
                if (File.Exists(UserStylesPath))
                {
                    UserStylesFile parsed = JsonConvert.DeserializeObject<UserStylesFile>(
                        File.ReadAllText(UserStylesPath));
                    if (parsed != null)
                    {
                        parsed.Styles = parsed.Styles == null
                            ? EmptyStyles()
                            : new Dictionary<string, Dictionary<string, string>>(parsed.Styles, StringComparer.OrdinalIgnoreCase);
                        return parsed;
                    }
                }
            }
            catch (Exception exception)
            {
                JsonFileSafety.BackupBroken(UserStylesPath);
                Plugin.LogSource?.LogWarning($"Could not load user style presets: {exception.Message}");
            }

            return new UserStylesFile { Version = 1, Styles = EmptyStyles() };
        }

        private static bool WriteUserStyles(UserStylesFile file)
        {
            try
            {
                file.Version = 1;
                Directory.CreateDirectory(Path.GetDirectoryName(UserStylesPath));
                JsonFileSafety.WriteAtomic(UserStylesPath, JsonConvert.SerializeObject(file, Formatting.Indented));
                return true;
            }
            catch (Exception exception)
            {
                Plugin.LogSource?.LogWarning($"Could not save user style presets: {exception.Message}");
                return false;
            }
        }

        private static Dictionary<string, Dictionary<string, string>> EmptyStyles()
        {
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsCovered(ConfigDefinition definition)
        {
            return Array.IndexOf(CoveredSections, definition.Section) >= 0
                && Array.IndexOf(ExcludedKeys, definition.Key) < 0;
        }
    }
}
