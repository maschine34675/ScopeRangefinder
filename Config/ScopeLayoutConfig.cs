using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace ScopeRangefinder
{
    internal sealed class ScopeLayoutConfig
    {
        private const int CurrentVersion = 3;

        public int? Version { get; set; }
        public ScopeLayoutEntry Default { get; set; } = new ScopeLayoutEntry();
        public Dictionary<string, ScopeLayoutEntry> Scopes { get; set; } = new Dictionary<string, ScopeLayoutEntry>(StringComparer.OrdinalIgnoreCase);
        [JsonIgnore]
        private ScopeLayoutFile _presets;
        [JsonIgnore]
        private ScopeLayoutFile _userLayouts;

        public static string PresetPath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "BepInEx",
            "plugins",
            "maschine-ScopeRangefinder",
            "ScopeRangefinder.presets.json");

        public static string UserPath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "BepInEx",
            "plugins",
            "maschine-ScopeRangefinder",
            "ScopeRangefinder.layouts.json");

        public static ScopeLayoutConfig LoadOrCreate()
        {
            ScopeLayoutFile presets = LoadOrReplace(
                PresetPath,
                CreateDefaultPresetFile,
                "preset");
            MarkReadOnly(PresetPath);
            ScopeLayoutFile userLayouts = LoadOrReplace(
                UserPath,
                CreateEmptyUserFile,
                "user");

            return new ScopeLayoutConfig
            {
                Version = CurrentVersion,
                Default = ScopeLayoutEntry.Merge(presets.Default, userLayouts.Default),
                Scopes = MergeScopes(presets, userLayouts),
                _presets = presets,
                _userLayouts = userLayouts
            };
        }

        public ScopeLayoutEntry GetForScope(string key)
        {
            if (!string.IsNullOrEmpty(key) && Scopes != null && Scopes.TryGetValue(key, out ScopeLayoutEntry entry))
            {
                return ScopeLayoutEntry.Merge(Default, entry);
            }

            return Default ?? new ScopeLayoutEntry();
        }

        public ScopeLayoutEntry GetRawForScope(string key)
        {
            if (string.IsNullOrEmpty(key) || _userLayouts?.Scopes == null)
            {
                return null;
            }

            return _userLayouts.Scopes.TryGetValue(key, out ScopeLayoutEntry entry) ? entry : null;
        }

        public void SetForScope(string key, ScopeLayoutEntry entry)
        {
            if (string.IsNullOrEmpty(key) || entry == null)
            {
                return;
            }

            _userLayouts ??= CreateEmptyUserFile();
            _userLayouts.Scopes ??= new Dictionary<string, ScopeLayoutEntry>(StringComparer.OrdinalIgnoreCase);
            _userLayouts.Scopes[key] = entry;
            RefreshMergedLayouts();
        }

        public void ResetScope(string key)
        {
            if (string.IsNullOrEmpty(key) || _userLayouts?.Scopes == null)
            {
                return;
            }

            _userLayouts.Scopes.Remove(key);
            RefreshMergedLayouts();
        }

        public bool Save()
        {
            try
            {
                _userLayouts ??= CreateEmptyUserFile();
                _userLayouts.Version = CurrentVersion;
                Directory.CreateDirectory(Path.GetDirectoryName(UserPath));
                JsonFileSafety.WriteAtomic(UserPath, JsonConvert.SerializeObject(_userLayouts, Formatting.Indented));
                return true;
            }
            catch (Exception ex)
            {
                Plugin.LogSource?.LogWarning($"Failed to save scope layout config '{UserPath}': {ex.Message}");
                return false;
            }
        }

        private void RefreshMergedLayouts()
        {
            Default = ScopeLayoutEntry.Merge(_presets?.Default, _userLayouts?.Default);
            Scopes = MergeScopes(_presets, _userLayouts);
        }

        private static ScopeLayoutFile LoadOrReplace(
            string path,
            Func<ScopeLayoutFile> createReplacement,
            string label)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return ReplaceWithDefault(path, createReplacement, $"{label} layout file is missing");
                }

                ScopeLayoutFile loaded = JsonConvert.DeserializeObject<ScopeLayoutFile>(File.ReadAllText(path));
                if (loaded == null)
                {
                    return ReplaceWithDefault(path, createReplacement, $"{label} layout file could not be read");
                }

                if (loaded.Version != CurrentVersion)
                {
                    return ReplaceWithDefault(
                        path,
                        createReplacement,
                        $"{label} layout file version {loaded.Version} is not supported");
                }

                loaded.Scopes ??= new Dictionary<string, ScopeLayoutEntry>(StringComparer.OrdinalIgnoreCase);
                loaded.Default ??= new ScopeLayoutEntry();
                return loaded;
            }
            catch (Exception ex)
            {
                Plugin.LogSource?.LogWarning($"Failed to load scope layout config '{path}': {ex.Message}");
                return ReplaceWithDefault(path, createReplacement, $"{label} layout file could not be loaded");
            }
        }

        private static Dictionary<string, ScopeLayoutEntry> MergeScopes(
            ScopeLayoutFile presets,
            ScopeLayoutFile userLayouts)
        {
            var merged = new Dictionary<string, ScopeLayoutEntry>(StringComparer.OrdinalIgnoreCase);

            AddMergedScopes(merged, presets?.Default, presets?.Scopes);
            AddMergedScopes(merged, userLayouts?.Default, userLayouts?.Scopes);
            return merged;
        }

        private static void AddMergedScopes(
            Dictionary<string, ScopeLayoutEntry> target,
            ScopeLayoutEntry defaultEntry,
            Dictionary<string, ScopeLayoutEntry> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (KeyValuePair<string, ScopeLayoutEntry> pair in source)
            {
                if (string.IsNullOrEmpty(pair.Key))
                {
                    continue;
                }

                target.TryGetValue(pair.Key, out ScopeLayoutEntry existing);
                target[pair.Key] = ScopeLayoutEntry.Merge(
                    ScopeLayoutEntry.Merge(defaultEntry, existing),
                    pair.Value);
            }
        }

        private static void MarkReadOnly(string path)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReadOnly) == 0)
                {
                    File.SetAttributes(path, attributes | FileAttributes.ReadOnly);
                }
            }
            catch
            {
            }
        }

        private static ScopeLayoutFile CreateDefaultPresetFile()
        {
            return new ScopeLayoutFile
            {
                Version = CurrentVersion,
                Default = new ScopeLayoutEntry
                {
                    OffsetX = 0f,
                    OffsetY = 0f,
                    Scale = 0f
                },
                Scopes = new Dictionary<string, ScopeLayoutEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    ["example_scope_template_id"] = new ScopeLayoutEntry
                    {
                        OffsetX = 0f,
                        OffsetY = 0f,
                        Scale = 0f
                    }
                }
            };
        }

        private static ScopeLayoutFile CreateEmptyUserFile()
        {
            return new ScopeLayoutFile
            {
                Version = CurrentVersion,
                Default = new ScopeLayoutEntry(),
                Scopes = new Dictionary<string, ScopeLayoutEntry>(StringComparer.OrdinalIgnoreCase)
            };
        }

        private static ScopeLayoutFile ReplaceWithDefault(
            string path,
            Func<ScopeLayoutFile> createReplacement,
            string reason)
        {
            ScopeLayoutFile replacement = createReplacement();

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                JObject output = JObject.FromObject(replacement);
                if (File.Exists(path))
                {
                    try
                    {
                        JToken styles = JObject.Parse(File.ReadAllText(path))["Styles"];
                        if (styles != null)
                        {
                            output["Styles"] = styles;
                        }
                    }
                    catch
                    {
                    }
                    JsonFileSafety.BackupBroken(path);
                    File.SetAttributes(path, FileAttributes.Normal);
                }

                JsonFileSafety.WriteAtomic(path, output.ToString(Formatting.Indented));
                Plugin.LogSource?.LogWarning(
                    $"Replaced ScopeRangefinder layout config '{path}' with version {CurrentVersion} defaults: {reason}.");
            }
            catch (Exception ex)
            {
                Plugin.LogSource?.LogWarning(
                    $"Failed to replace scope layout config '{path}': {ex.Message}");
            }

            return replacement;
        }
    }

    internal sealed class ScopeLayoutFile
    {
        public int? Version { get; set; }
        public ScopeLayoutEntry Default { get; set; } = new ScopeLayoutEntry();
        public Dictionary<string, ScopeLayoutEntry> Scopes { get; set; } = new Dictionary<string, ScopeLayoutEntry>(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class ScopeLayoutEntry
    {
        public float? OffsetX { get; set; }
        public float? OffsetY { get; set; }
        public float? Scale { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string StylePreset { get; set; }

        public static ScopeLayoutEntry Merge(ScopeLayoutEntry fallback, ScopeLayoutEntry specific)
        {
            fallback ??= new ScopeLayoutEntry();
            specific ??= new ScopeLayoutEntry();

            return new ScopeLayoutEntry
            {
                OffsetX = specific.OffsetX ?? fallback.OffsetX,
                OffsetY = specific.OffsetY ?? fallback.OffsetY,
                Scale = specific.Scale ?? fallback.Scale,
                StylePreset = specific.StylePreset ?? fallback.StylePreset
            };
        }
    }
}
