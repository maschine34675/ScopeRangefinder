using System;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;

namespace ScopeRangefinder
{
    internal static class StylePresetDrawer
    {
        private static string[] _cachedPresetNames;
        private static bool[] _cachedPresetIsBuiltin;
        private static bool _expanded;
        private static string _saveName = string.Empty;
        private static string _status = string.Empty;
        private static string _deleteArmedName;
        private static Action _pendingLayoutChange;

        private static void Defer(Action change)
        {
            _pendingLayoutChange += change;
        }

        public static void Draw(ConfigEntryBase entry)
        {
            if (_pendingLayoutChange != null && Event.current.type == EventType.Layout)
            {
                Action pending = _pendingLayoutChange;
                _pendingLayoutChange = null;
                pending();
            }

            if (_cachedPresetNames == null)
            {
                Rescan();
            }

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            string currentValue = entry.BoxedValue as string ?? string.Empty;
            string headerText = (_expanded ? "▼ " : "▶ ")
                + (string.IsNullOrEmpty(currentValue) ? "(select a preset)" : currentValue);
            if (GUILayout.Button(headerText, GUILayout.ExpandWidth(true)))
            {
                Defer(() =>
                {
                    _expanded = !_expanded;
                    _status = string.Empty;
                    _deleteArmedName = null;
                });
            }

            if (!_expanded)
            {
                GUILayout.EndVertical();
                return;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Folder", GUILayout.ExpandWidth(false)))
            {
                string pluginDirectory = Path.GetDirectoryName(StylePresets.UserStylesPath);
                Application.OpenURL("file:///" + pluginDirectory.Replace('\\', '/'));
            }

            if (GUILayout.Button("Rescan", GUILayout.ExpandWidth(false)))
            {
                Defer(Rescan);
            }

            GUILayout.EndHorizontal();

            if (_cachedPresetNames.Length == 0)
            {
                GUILayout.Label("No presets found. Save the current look below.");
            }

            for (int nameIndex = 0; nameIndex < _cachedPresetNames.Length; nameIndex++)
            {
                string name = _cachedPresetNames[nameIndex];
                bool isSelected = string.Equals(name, currentValue, StringComparison.OrdinalIgnoreCase);
                Color previousColor = GUI.color;
                if (isSelected)
                {
                    GUI.color = Color.green;
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(name, GUILayout.ExpandWidth(true)))
                {
                    if (StylePresets.Apply(name))
                    {
                        entry.BoxedValue = name;
                        Defer(() =>
                        {
                            _expanded = false;
                            _deleteArmedName = null;
                        });
                    }
                    else
                    {
                        string failedName = name;
                        Defer(() =>
                        {
                            _status = $"Could not apply '{failedName}' (see log).";
                            _deleteArmedName = null;
                        });
                    }
                }

                GUI.color = previousColor;
                if (!_cachedPresetIsBuiltin[nameIndex])
                {
                    DrawDeleteButton(name, currentValue, entry);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            _saveName = GUILayout.TextField(_saveName ?? string.Empty, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Save Current As", GUILayout.ExpandWidth(false)))
            {
                string trimmed = (_saveName ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    Defer(() => _status = "Enter a preset name first.");
                }
                else if (StylePresets.IsBuiltin(trimmed))
                {
                    Defer(() => _status = $"'{trimmed}' is a shipped preset; pick a different name.");
                }
                else if (StylePresets.SaveCurrent(trimmed))
                {
                    entry.BoxedValue = trimmed;
                    _saveName = string.Empty;
                    Defer(() =>
                    {
                        _status = $"Saved '{trimmed}'.";
                        Rescan();
                    });
                }
                else
                {
                    Defer(() => _status = $"Could not save '{trimmed}' (see log).");
                }
            }

            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_status))
            {
                GUILayout.Label(_status);
            }

            GUILayout.EndVertical();
        }

        private static void DrawDeleteButton(string name, string currentValue, ConfigEntryBase entry)
        {
            bool armed = string.Equals(name, _deleteArmedName, StringComparison.OrdinalIgnoreCase);
            Color previousColor = GUI.color;
            if (armed)
            {
                GUI.color = Color.red;
            }

            if (GUILayout.Button("✕", GUILayout.Width(26f)))
            {
                if (!armed)
                {
                    Defer(() =>
                    {
                        _deleteArmedName = name;
                        _status = $"Click ✕ again to delete '{name}'.";
                    });
                }
                else if (StylePresets.Delete(name))
                {
                    if (string.Equals(name, currentValue, StringComparison.OrdinalIgnoreCase))
                    {
                        entry.BoxedValue = string.Empty;
                    }

                    Defer(() =>
                    {
                        _deleteArmedName = null;
                        _status = $"Deleted '{name}'.";
                        Rescan();
                    });
                }
                else
                {
                    Defer(() =>
                    {
                        _deleteArmedName = null;
                        _status = $"Could not delete '{name}' (see log).";
                    });
                }
            }

            GUI.color = previousColor;
        }

        private static void Rescan()
        {
            StylePresets.InvalidateShippedCache();
            _cachedPresetNames = StylePresets.ListPresetNames();
            _cachedPresetIsBuiltin = new bool[_cachedPresetNames.Length];
            for (int i = 0; i < _cachedPresetNames.Length; i++)
            {
                _cachedPresetIsBuiltin[i] = StylePresets.IsBuiltin(_cachedPresetNames[i]);
            }
        }
    }
}
