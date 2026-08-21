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
            ScopeRangefinderComponent.InvalidateStyleOverrideCache();
        }

        private sealed class UserStylesFile
        {
            public int? Version { get; set; }
            public Dictionary<string, Dictionary<string, string>> Styles { get; set; }
        }

        private const int SharedPresetVersion = 1;
        private sealed class SharedPresetDocument
        {
            public int ScopeRangefinderStyle { get; set; }
            public string Name { get; set; }
            public IDictionary<string, string> Values { get; set; }
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
        internal static bool TryGetPresetValues(string name, out Dictionary<string, string> values)
        {
            if (LoadShippedPresets().TryGetValue(name, out values))
            {
                return true;
            }

            return LoadUserStyles().Styles.TryGetValue(name, out values);
        }

        public static bool Apply(string name)
        {
            return TryGetPresetValues(name, out Dictionary<string, string> values)
                ? ApplyValues(name, values)
                : ApplyValues(name, null);
        }
        public static bool MatchesCurrent(string name)
        {
            if (string.IsNullOrEmpty(name)
                || !TryGetPresetValues(name, out Dictionary<string, string> values)
                || values == null)
            {
                return false;
            }

            ConfigFile config = Plugin.ConfigInstance;
            if (config == null)
            {
                return false;
            }

            foreach (ConfigDefinition definition in new List<ConfigDefinition>(config.Keys))
            {
                if (!IsCovered(definition))
                {
                    continue;
                }

                ConfigEntryBase entry = config[definition];
                object expected = entry.DefaultValue;
                if (values.TryGetValue($"{definition.Section}.{definition.Key}", out string serialized))
                {
                    try
                    {
                        expected = TomlTypeConverter.ConvertToValue(serialized, entry.SettingType);
                        AcceptableValueBase acceptableValues = entry.Description?.AcceptableValues;
                        if (acceptableValues != null)
                        {
                            expected = acceptableValues.Clamp(expected);
                        }
                    }
                    catch
                    {
                        expected = entry.DefaultValue;
                    }
                }
                string actualText;
                string expectedText;
                try
                {
                    actualText = TomlTypeConverter.ConvertToString(entry.BoxedValue, entry.SettingType);
                    expectedText = TomlTypeConverter.ConvertToString(expected, entry.SettingType);
                }
                catch
                {
                    return false;
                }

                if (!string.Equals(actualText, expectedText, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
        public static string ExportToJson(string name, Dictionary<string, string> values)
        {
            var document = new SharedPresetDocument
            {
                ScopeRangefinderStyle = SharedPresetVersion,
                Name = name,
                Values = new SortedDictionary<string, string>(values, StringComparer.OrdinalIgnoreCase)
            };
            return JsonConvert.SerializeObject(document, Formatting.Indented);
        }
        public static Dictionary<string, string> CaptureCurrentValues()
        {
            ConfigFile config = Plugin.ConfigInstance;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (config == null)
            {
                return values;
            }

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

            return values;
        }
        public static bool TryCapturePresetValues(string name, out Dictionary<string, string> values)
        {
            values = null;
            if (!TryGetPresetValues(name, out Dictionary<string, string> presetValues) || presetValues == null)
            {
                return false;
            }

            ConfigFile config = Plugin.ConfigInstance;
            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (config == null)
            {
                return false;
            }

            foreach (ConfigDefinition definition in new List<ConfigDefinition>(config.Keys))
            {
                if (!IsCovered(definition))
                {
                    continue;
                }

                ConfigEntryBase entry = config[definition];
                string key = $"{definition.Section}.{definition.Key}";
                values[key] = presetValues.TryGetValue(key, out string serialized)
                    ? serialized
                    : TomlTypeConverter.ConvertToString(entry.DefaultValue, entry.SettingType);
            }

            return true;
        }
        public static bool TryImportFromJson(string json, out string importedName, out string error)
        {
            importedName = null;
            error = null;
            if (string.IsNullOrEmpty(json) || json.IndexOf('{') < 0)
            {
                error = "clipboard does not contain a preset";
                return false;
            }

            SharedPresetDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<SharedPresetDocument>(json);
            }
            catch (Exception exception)
            {
                error = "clipboard is not valid preset JSON";
                Plugin.LogSource?.LogWarning($"Could not parse a shared style preset: {exception.Message}");
                return false;
            }
            if (document == null || document.ScopeRangefinderStyle < SharedPresetVersion)
            {
                error = "clipboard is not a ScopeRangefinder style";
                return false;
            }

            if (document.Values == null || document.Values.Count == 0)
            {
                error = "shared preset contains no settings";
                return false;
            }

            ConfigFile config = Plugin.ConfigInstance;
            if (config == null)
            {
                error = "config is not ready";
                return false;
            }
            var accepted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int rejected = 0;
            foreach (ConfigDefinition definition in new List<ConfigDefinition>(config.Keys))
            {
                if (!IsCovered(definition))
                {
                    continue;
                }

                string key = $"{definition.Section}.{definition.Key}";
                if (!document.Values.TryGetValue(key, out string serialized))
                {
                    continue;
                }

                try
                {
                    TomlTypeConverter.ConvertToValue(serialized, config[definition].SettingType);
                    accepted[key] = serialized;
                }
                catch (Exception exception)
                {
                    rejected++;
                    Plugin.LogSource?.LogWarning(
                        $"Shared style preset: dropping invalid value '{serialized}' for {key}: {exception.Message}");
                }
            }

            if (accepted.Count == 0)
            {
                error = "no usable settings in the shared preset";
                return false;
            }

            string baseName = string.IsNullOrWhiteSpace(document.Name)
                ? "Imported Preset"
                : document.Name.Trim();
            string uniqueName = MakeUniqueUserPresetName(baseName);

            UserStylesFile file = LoadUserStyles();
            file.Styles[uniqueName] = accepted;
            if (!WriteUserStyles(file))
            {
                error = "could not write the preset file";
                return false;
            }

            importedName = uniqueName;
            Plugin.LogSource?.LogInfo(
                $"Imported shared style preset as '{uniqueName}' ({accepted.Count} settings"
                + (rejected > 0 ? $", {rejected} invalid dropped" : string.Empty) + ").");
            return true;
        }

        private static string MakeUniqueUserPresetName(string baseName)
        {
            var taken = new HashSet<string>(ListPresetNames(), StringComparer.OrdinalIgnoreCase);
            if (!taken.Contains(baseName))
            {
                return baseName;
            }
            for (int suffix = 2; ; suffix++)
            {
                string candidate = $"{baseName} ({suffix})";
                if (!taken.Contains(candidate))
                {
                    return candidate;
                }
            }
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
            ScopeRangefinderComponent.InvalidateStyleOverrideCache();
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
            ScopeRangefinderComponent.InvalidateStyleOverrideCache();
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
