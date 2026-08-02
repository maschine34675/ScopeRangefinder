using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;

namespace ScopeRangefinder
{
    internal static class FontPickerDrawer
    {
        private static string[] _cachedFontFiles;
        private static bool _expanded;
        private const float EditCommitDelaySeconds = 0.6f;
        private static string _pendingEditValue;
        private static float _pendingEditDeadline;
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

            if (_cachedFontFiles == null)
            {
                RescanFonts();
            }

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            string currentValue = entry.BoxedValue as string ?? string.Empty;
            if (_pendingEditValue != null
                && Time.realtimeSinceStartup >= _pendingEditDeadline
                && Event.current.type == EventType.Layout)
            {
                entry.BoxedValue = _pendingEditValue;
                currentValue = _pendingEditValue;
                _pendingEditValue = null;
            }
            string headerText = (_expanded ? "▼ " : "▶ ")
                + (string.IsNullOrEmpty(currentValue) ? "(select a font file)" : currentValue);
            if (GUILayout.Button(headerText, GUILayout.ExpandWidth(true)))
            {
                Defer(() => _expanded = !_expanded);
            }

            if (!_expanded)
            {
                GUILayout.EndVertical();
                return;
            }
            string displayedValue = _pendingEditValue ?? currentValue;
            string editedValue = GUILayout.TextField(displayedValue, GUILayout.ExpandWidth(true));
            if (editedValue != displayedValue)
            {
                _pendingEditValue = editedValue;
                _pendingEditDeadline = Time.realtimeSinceStartup + EditCommitDelaySeconds;
            }

            if (_pendingEditValue != null)
            {
                currentValue = _pendingEditValue;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Folder", GUILayout.ExpandWidth(false)))
            {
                Application.OpenURL("file:///" + ScopeDisplayStyle.GetFontsDirectory().Replace('\\', '/'));
            }

            if (GUILayout.Button("Rescan", GUILayout.ExpandWidth(false)))
            {
                Defer(RescanFonts);
            }

            GUILayout.EndHorizontal();

            if (_cachedFontFiles.Length == 0)
            {
                GUILayout.Label("No font files found. Drop .ttf/.otf files or TMP font bundles into the fonts folder, then Rescan.");
                GUILayout.EndVertical();
                return;
            }
            string currentFile = currentValue;
            int separatorIndex = currentFile.IndexOf(':');
            if (separatorIndex > 0)
            {
                currentFile = currentFile.Substring(0, separatorIndex);
            }

            currentFile = currentFile.Trim();

            foreach (string fileName in _cachedFontFiles)
            {
                bool isSelected = string.Equals(fileName, currentFile, StringComparison.OrdinalIgnoreCase);
                Color previousColor = GUI.color;
                if (isSelected)
                {
                    GUI.color = Color.green;
                }

                if (GUILayout.Button(fileName, GUILayout.ExpandWidth(true)))
                {
                    _pendingEditValue = null;
                    entry.BoxedValue = fileName;
                    Plugin.ScopeFontSource.Value = ScopeFontSource.CustomFont;
                    Defer(() => _expanded = false);
                }

                GUI.color = previousColor;
            }

            GUILayout.EndVertical();
        }

        private static void RescanFonts()
        {
            ScopeDisplayStyle.InvalidateFontCaches();
            try
            {
                var fontFiles = new List<string>();
                foreach (string path in Directory.GetFiles(ScopeDisplayStyle.GetFontsDirectory()))
                {
                    string extension = Path.GetExtension(path).ToLowerInvariant();
                    if (extension == ".txt" || extension == ".md")
                    {
                        continue;
                    }

                    fontFiles.Add(Path.GetFileName(path));
                }

                fontFiles.Sort(StringComparer.OrdinalIgnoreCase);
                _cachedFontFiles = fontFiles.ToArray();
            }
            catch (Exception exception)
            {
                Plugin.LogSource?.LogWarning($"Could not scan the fonts folder: {exception.Message}");
                _cachedFontFiles = Array.Empty<string>();
            }
        }
    }
}
