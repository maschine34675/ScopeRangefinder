using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace ScopeRangefinder
{
    internal sealed class ScopeLayoutConfig
    {
        public ScopeLayoutEntry Default { get; set; } = new ScopeLayoutEntry();
        public Dictionary<string, ScopeLayoutEntry> Scopes { get; set; } = new Dictionary<string, ScopeLayoutEntry>(StringComparer.OrdinalIgnoreCase);

        public static string ConfigPath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "BepInEx",
            "plugins",
            "maschine-ScopeRangefinder",
            "ScopeRangefinder.layouts.json");

        public static ScopeLayoutConfig LoadOrCreate()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    ScopeLayoutConfig created = CreateDefault();
                    Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                    File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(created, Formatting.Indented));
                    return created;
                }

                ScopeLayoutConfig loaded = JsonConvert.DeserializeObject<ScopeLayoutConfig>(File.ReadAllText(ConfigPath));
                return loaded ?? CreateDefault();
            }
            catch (Exception ex)
            {
                Plugin.LogSource?.LogWarning($"Failed to load scope layout config '{ConfigPath}': {ex.Message}");
                return CreateDefault();
            }
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
            if (string.IsNullOrEmpty(key) || Scopes == null)
            {
                return null;
            }

            return Scopes.TryGetValue(key, out ScopeLayoutEntry entry) ? entry : null;
        }

        public void SetForScope(string key, ScopeLayoutEntry entry)
        {
            if (string.IsNullOrEmpty(key) || entry == null)
            {
                return;
            }

            Scopes ??= new Dictionary<string, ScopeLayoutEntry>(StringComparer.OrdinalIgnoreCase);
            Scopes[key] = entry;
        }

        public void ResetScope(string key)
        {
            if (string.IsNullOrEmpty(key) || Scopes == null)
            {
                return;
            }

            Scopes.Remove(key);
        }

        public bool Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(this, Formatting.Indented));
                return true;
            }
            catch (Exception ex)
            {
                Plugin.LogSource?.LogWarning($"Failed to save scope layout config '{ConfigPath}': {ex.Message}");
                return false;
            }
        }

        private static ScopeLayoutConfig CreateDefault()
        {
            return new ScopeLayoutConfig
            {
                Default = new ScopeLayoutEntry
                {
                    OffsetX = null,
                    OffsetY = null,
                    Scale = null
                },
                Scopes = new Dictionary<string, ScopeLayoutEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    ["example_scope_template_id"] = new ScopeLayoutEntry
                    {
                        OffsetX = -0.022f,
                        OffsetY = -0.014f,
                        Scale = 0.05f
                    }
                }
            };
        }
    }

    internal sealed class ScopeLayoutEntry
    {
        public float? OffsetX { get; set; }
        public float? OffsetY { get; set; }
        public float? Scale { get; set; }

        public static ScopeLayoutEntry Merge(ScopeLayoutEntry fallback, ScopeLayoutEntry specific)
        {
            fallback ??= new ScopeLayoutEntry();
            specific ??= new ScopeLayoutEntry();

            return new ScopeLayoutEntry
            {
                OffsetX = specific.OffsetX ?? fallback.OffsetX,
                OffsetY = specific.OffsetY ?? fallback.OffsetY,
                Scale = specific.Scale ?? fallback.Scale
            };
        }
    }
}
