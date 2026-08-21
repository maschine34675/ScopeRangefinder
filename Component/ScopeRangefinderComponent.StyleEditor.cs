using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
        private const float StyleLabelWidth = 110f;
        private const float StyleValueWidth = 46f;
        private const float StyleEditCommitDelaySeconds = 0.6f;
        private const float StyleSaveDebounceSeconds = 0.6f;

        private const float StyleComparisonInterval = 0.5f;
        private const float PresetRowButtonWidth = 46f;
        private const float PresetDeleteButtonWidth = 26f;

        private bool _styleSectionExpanded = true;
        private bool _presetListExpanded;
        private bool _presetApplyToScope;
        private bool _readoutOptionsExpanded;
        private bool _textOptionsExpanded;
        private bool _backgroundOptionsExpanded;
        private bool _textColorExpanded;
        private bool _backgroundColorExpanded;
        private bool _fontFileListExpanded;
        private string _presetSaveName = string.Empty;
        private string _armedDeletePreset;
        private string[] _styleEditorPresetNames;
        private bool[] _styleEditorPresetIsBuiltin;
        private string[] _styleEditorFontFiles;
        private GUIStyle _wrappedLabelStyle;
        private GUIStyle _indentBoxStyle;
        private bool _globalStyleMatchesPreset;
        private bool _styleComparisonDirty = true;
        private string _comparedPresetName;
        private float _nextStyleComparisonAt;

        private GUIStyle WrappedLabelStyle =>
            _wrappedLabelStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
        private void InvalidateStyleComparison()
        {
            _styleComparisonDirty = true;
        }

        private void RefreshStyleComparisonIfDue()
        {
            string presetName = Plugin.SelectedStylePreset.Value;
            if (string.Equals(presetName, _comparedPresetName, StringComparison.Ordinal)
                && (!_styleComparisonDirty || Time.realtimeSinceStartup < _nextStyleComparisonAt))
            {
                return;
            }

            _styleComparisonDirty = false;
            _comparedPresetName = presetName;
            _globalStyleMatchesPreset = StylePresets.MatchesCurrent(presetName);
            _nextStyleComparisonAt = Time.realtimeSinceStartup + StyleComparisonInterval;
        }
        private void BeginStyleIndent()
        {
            _indentBoxStyle ??= new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(6, 6, 4, 4),
                margin = new RectOffset(10, 2, 0, 2)
            };
            GUILayout.BeginVertical(_indentBoxStyle);
        }

        private void EndStyleIndent()
        {
            GUILayout.EndVertical();
        }

        private struct PendingTextEdit
        {
            public string Value;
            public float Deadline;
        }
        private readonly Dictionary<ConfigEntryBase, PendingTextEdit> _pendingStyleTextEdits =
            new Dictionary<ConfigEntryBase, PendingTextEdit>();
        private float _styleConfigSaveDueAt = float.PositiveInfinity;

        private void RefreshStyleEditorPresets()
        {
            _styleEditorPresetNames = StylePresets.ListPresetNames();
            _styleEditorPresetIsBuiltin = new bool[_styleEditorPresetNames.Length];
            for (int i = 0; i < _styleEditorPresetNames.Length; i++)
            {
                _styleEditorPresetIsBuiltin[i] = StylePresets.IsBuiltin(_styleEditorPresetNames[i]);
            }
        }
        private void FlushStyleConfigSave()
        {
            if (Time.realtimeSinceStartup < _styleConfigSaveDueAt)
            {
                return;
            }

            _styleConfigSaveDueAt = float.PositiveInfinity;
            Plugin.ConfigInstance?.Save();
        }

        private void SetStyleValue<T>(ConfigEntry<T> entry, T value)
        {
            ConfigFile config = Plugin.ConfigInstance;
            bool previousSaveOnSet = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            try
            {
                entry.Value = value;
            }
            finally
            {
                config.SaveOnConfigSet = previousSaveOnSet;
            }

            _styleConfigSaveDueAt = Time.realtimeSinceStartup + StyleSaveDebounceSeconds;
            InvalidateStyleComparison();
        }
        private void CommitDueStyleTextEdits(bool force)
        {
            if (_pendingStyleTextEdits.Count == 0)
            {
                return;
            }

            List<ConfigEntryBase> committed = null;
            foreach (KeyValuePair<ConfigEntryBase, PendingTextEdit> pair in _pendingStyleTextEdits)
            {
                if (!force && Time.realtimeSinceStartup < pair.Value.Deadline)
                {
                    continue;
                }

                ConfigFile config = Plugin.ConfigInstance;
                bool previousSaveOnSet = config.SaveOnConfigSet;
                config.SaveOnConfigSet = false;
                try
                {
                    pair.Key.BoxedValue = pair.Value.Value;
                }
                finally
                {
                    config.SaveOnConfigSet = previousSaveOnSet;
                }

                _styleConfigSaveDueAt = Mathf.Min(
                    _styleConfigSaveDueAt,
                    Time.realtimeSinceStartup + StyleSaveDebounceSeconds);
                InvalidateStyleComparison();
                (committed ??= new List<ConfigEntryBase>()).Add(pair.Key);
            }

            if (committed == null)
            {
                return;
            }

            foreach (ConfigEntryBase entry in committed)
            {
                _pendingStyleTextEdits.Remove(entry);
            }
        }

        private void FlushStyleEditsNow()
        {
            CommitDueStyleTextEdits(true);
            if (!float.IsPositiveInfinity(_styleConfigSaveDueAt))
            {
                _styleConfigSaveDueAt = float.PositiveInfinity;
                Plugin.ConfigInstance?.Save();
            }
        }

        private void DrawStyleSection()
        {
            DrawSectionFoldout("Style", ref _styleSectionExpanded);
            if (!_styleSectionExpanded)
            {
                return;
            }

            bool hasScope = !string.IsNullOrEmpty(_currentLayoutKey);
            bool applyToScope = _presetApplyToScope && hasScope;

            BeginStyleIndent();
            GUILayout.BeginHorizontal();
            string globalName = string.IsNullOrEmpty(Plugin.SelectedStylePreset.Value)
                ? "(none)"
                : Plugin.SelectedStylePreset.Value
                    + (_globalStyleMatchesPreset ? string.Empty : " (modified)");
            GUILayout.Label($"Global style: {globalName}", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Copy", GUILayout.Width(52f)))
            {
                CopyCurrentStyleToClipboard();
                GUIUtility.ExitGUI();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            string scopeName = !hasScope
                ? "(no active scope)"
                : string.IsNullOrEmpty(_editorStylePreset) ? "(global style)" : _editorStylePreset;
            GUILayout.Label($"This scope: {scopeName}", GUILayout.ExpandWidth(true));
            if (hasScope && !string.IsNullOrEmpty(_editorStylePreset)
                && GUILayout.Button("Clear", GUILayout.Width(52f)))
            {
                _editorStylePreset = null;
                _editorStatus = "Scope set to global style — Save Scope writes it";
                _layoutEditorRect.height = 0f;
                GUIUtility.ExitGUI();
            }

            GUILayout.EndHorizontal();

            if (hasScope && !string.IsNullOrEmpty(_editorStylePreset))
            {
                GUILayout.Label(
                    $"This scope shows preset '{_editorStylePreset}'. The options below edit the global style, "
                    + "which is hidden on this scope — clear the assignment to tune what you see.",
                    WrappedLabelStyle);
            }

            EndStyleIndent();

            DrawPresetBrowser(hasScope, applyToScope);

            GUILayout.Label("Live preview:");
            DrawFontPreview(null);

            DrawSectionFoldout("Readout options", ref _readoutOptionsExpanded);
            if (_readoutOptionsExpanded)
            {
                BeginStyleIndent();
                DrawStyleEnumCycle("Distance Unit", Plugin.DistanceUnit);
                DrawStyleToggle("Show Unit Suffix", Plugin.ShowUnitSuffix);
                DrawStyleToggle("Decimal Format (045.0)", Plugin.UseDecimalFormat);
                DrawStyleToggle("Show Zeroing Line", Plugin.ShowZeroLine);
                DrawStyleEnumCycle("Ballistics Line", Plugin.BallisticsLine);
                DrawStyleEnumCycle("Hold Unit", Plugin.BallisticsHoldUnit);
                DrawStyleTextField("Range Prefix", Plugin.RangeLinePrefix);
                DrawStyleTextField("Zeroing Prefix", Plugin.ZeroLinePrefix);
                DrawStyleTextField("No-Target Text", Plugin.NoDistanceText);
                EndStyleIndent();
            }

            DrawSectionFoldout("Text options", ref _textOptionsExpanded);
            if (_textOptionsExpanded)
            {
                BeginStyleIndent();
                DrawStyleColor("Text Color", Plugin.ScopeWorldTextColor, ref _textColorExpanded);
                DrawStyleEnumCycle("Font", Plugin.ScopeFontSource);
                DrawStyleTextField("System Font", Plugin.ScopeFontName);
                DrawFontFileControl();
                DrawStyleSlider("Thickness", Plugin.ScopeTextThickness, -0.4f, 0.4f);
                DrawStyleSlider("Letter Spacing", Plugin.ScopeTextSpacing, -10f, 40f, "0.0");
                DrawStyleSlider("Glow", Plugin.ScopeTextGlow, 0f, 1f);
                DrawStyleSlider("Black Outline", Plugin.ScopeTextOutline, 0f, 0.4f);
                DrawStyleSlider("Chromatic Aberr.", Plugin.ScopeTextAberration, 0f, 1f);
                DrawStyleSlider("Vertical Offset", Plugin.ScopeWorldTextOffsetY, -0.1f, 0.1f, "0.000");
                EndStyleIndent();
            }

            DrawSectionFoldout("Background options", ref _backgroundOptionsExpanded);
            if (_backgroundOptionsExpanded)
            {
                BeginStyleIndent();
                DrawStyleToggle("Background Plate", Plugin.ScopeWorldBackground);
                DrawStyleSlider("Plate Width", Plugin.ScopeWorldBackgroundWidth, 0f, 0.8f);
                DrawStyleSlider("Plate Height", Plugin.ScopeWorldBackgroundHeight, 0f, 0.4f);
                DrawStyleColor("Plate Color", Plugin.ScopeWorldBackgroundColor, ref _backgroundColorExpanded);
                EndStyleIndent();
            }
        }
        private void CopyCurrentStyleToClipboard()
        {
            CommitDueStyleTextEdits(true);
            string presetName = Plugin.SelectedStylePreset.Value;
            _comparedPresetName = presetName;
            _globalStyleMatchesPreset = StylePresets.MatchesCurrent(presetName);
            _styleComparisonDirty = false;
            _nextStyleComparisonAt = Time.realtimeSinceStartup + StyleComparisonInterval;

            string name = string.IsNullOrEmpty(presetName)
                ? "Shared Style"
                : presetName + (_globalStyleMatchesPreset ? string.Empty : " (modified)");
            GUIUtility.systemCopyBuffer = StylePresets.ExportToJson(name, StylePresets.CaptureCurrentValues());
            _editorStatus = "Current style copied — paste it to share";
        }

        private void CopyPresetToClipboard(string name)
        {
            if (!StylePresets.TryCapturePresetValues(name, out System.Collections.Generic.Dictionary<string, string> values))
            {
                _editorStatus = $"Could not read '{name}' (see log)";
                return;
            }

            GUIUtility.systemCopyBuffer = StylePresets.ExportToJson(name, values);
            _editorStatus = $"'{name}' copied — paste it to share";
        }

        private void PastePresetFromClipboard()
        {
            if (StylePresets.TryImportFromJson(
                    GUIUtility.systemCopyBuffer, out string importedName, out string error))
            {
                _editorStatus = $"Imported '{importedName}' — click it to apply";
                RefreshStyleEditorPresets();
                InvalidateStyleComparison();
                _layoutEditorRect.height = 0f;
                return;
            }

            _editorStatus = $"Paste failed: {error}";
        }

        private void DrawPresetBrowser(bool hasScope, bool applyToScope)
        {
            DrawSectionFoldout("Browse & apply presets", ref _presetListExpanded);
            if (!_presetListExpanded)
            {
                return;
            }

            if (_styleEditorPresetNames == null)
            {
                RefreshStyleEditorPresets();
            }

            BeginStyleIndent();
            GUI.enabled = hasScope;
            int mode = GUILayout.Toolbar(applyToScope ? 1 : 0, new[] { "Apply to all scopes", "Only this scope" });
            GUI.enabled = true;
            if ((mode == 1) != applyToScope && hasScope)
            {
                _presetApplyToScope = mode == 1;
                _armedDeletePreset = null;
                _layoutEditorRect.height = 0f;
                GUIUtility.ExitGUI();
            }

            if (applyToScope)
            {
                DrawScopePresetRow(null, "(global style)", true);
            }

            string[] names = _styleEditorPresetNames;
            for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
            {
                if (applyToScope)
                {
                    DrawScopePresetRow(names[nameIndex], names[nameIndex], _styleEditorPresetIsBuiltin[nameIndex]);
                }
                else
                {
                    DrawGlobalPresetRow(names[nameIndex], _styleEditorPresetIsBuiltin[nameIndex]);
                }
            }

            GUILayout.BeginHorizontal();
            _presetSaveName = GUILayout.TextField(_presetSaveName ?? string.Empty, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Save Current As", GUILayout.ExpandWidth(false)))
            {
                SaveGlobalStyleAsPreset();
                GUIUtility.ExitGUI();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Paste Shared Preset", GUILayout.ExpandWidth(false)))
            {
                PastePresetFromClipboard();
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("Open Folder", GUILayout.ExpandWidth(false)))
            {
                string pluginDirectory = Path.GetDirectoryName(StylePresets.UserStylesPath);
                Application.OpenURL("file:///" + pluginDirectory.Replace('\\', '/'));
            }

            if (GUILayout.Button("Rescan", GUILayout.ExpandWidth(false)))
            {
                StylePresets.InvalidateShippedCache();
                RefreshStyleEditorPresets();
                _styleEditorFontFiles = null;
                _armedDeletePreset = null;
                InvalidateStyleComparison();
                _layoutEditorRect.height = 0f;
                GUIUtility.ExitGUI();
            }

            GUILayout.EndHorizontal();
            EndStyleIndent();
        }
        private void DrawGlobalPresetRow(string name, bool isBuiltin)
        {
            GUILayout.BeginHorizontal();
            bool isSelected = string.Equals(
                name, Plugin.SelectedStylePreset.Value, StringComparison.OrdinalIgnoreCase);
            Color previousColor = GUI.color;
            if (isSelected)
            {
                GUI.color = Color.green;
            }

            if (GUILayout.Button(name, GUILayout.ExpandWidth(true)))
            {
                CommitDueStyleTextEdits(true);
                if (StylePresets.Apply(name))
                {
                    Plugin.SelectedStylePreset.Value = name;
                    _editorStatus = $"Applied '{name}' to the global style";
                }
                else
                {
                    _editorStatus = $"Could not apply '{name}' (see log)";
                }

                GUI.color = previousColor;
                _armedDeletePreset = null;
                InvalidateStyleComparison();
                GUIUtility.ExitGUI();
            }

            GUI.color = previousColor;
            if (isBuiltin)
            {
                GUILayout.Space(PresetRowButtonWidth + PresetDeleteButtonWidth + 8f);
            }
            else
            {
                DrawPresetCopyButton(name);
                DrawPresetDeleteButton(name);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawPresetCopyButton(string name)
        {
            if (GUILayout.Button("Copy", GUILayout.Width(PresetRowButtonWidth)))
            {
                CopyPresetToClipboard(name);
                GUIUtility.ExitGUI();
            }
        }
        private void DrawScopePresetRow(string name, string label, bool isBuiltin)
        {
            bool isSelected = string.IsNullOrEmpty(name)
                ? string.IsNullOrEmpty(_editorStylePreset)
                : string.Equals(name, _editorStylePreset, StringComparison.OrdinalIgnoreCase);
            Color previousColor = GUI.color;
            if (isSelected)
            {
                GUI.color = Color.green;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(label, GUILayout.ExpandWidth(true)))
            {
                _editorStylePreset = name;
                _editorStatus = string.IsNullOrEmpty(name)
                    ? "Scope set to global style — Save Scope writes it"
                    : $"Previewing '{name}' on this scope — Save Scope writes it";
                GUI.color = previousColor;
                _armedDeletePreset = null;
                _layoutEditorRect.height = 0f;
                GUIUtility.ExitGUI();
            }

            GUI.color = previousColor;
            if (isBuiltin)
            {
                GUILayout.Space(PresetRowButtonWidth + 4f);
            }
            else
            {
                DrawPresetCopyButton(name);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawPresetDeleteButton(string name)
        {
            bool armed = string.Equals(name, _armedDeletePreset, StringComparison.OrdinalIgnoreCase);
            Color previousColor = GUI.color;
            if (armed)
            {
                GUI.color = Color.red;
            }

            if (GUILayout.Button("✕", GUILayout.Width(PresetDeleteButtonWidth)))
            {
                GUI.color = previousColor;
                if (!armed)
                {
                    _armedDeletePreset = name;
                    _editorStatus = $"Click ✕ again to delete '{name}'";
                }
                else if (StylePresets.Delete(name))
                {
                    if (string.Equals(name, Plugin.SelectedStylePreset.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        Plugin.SelectedStylePreset.Value = string.Empty;
                    }

                    if (string.Equals(name, _editorStylePreset, StringComparison.OrdinalIgnoreCase))
                    {
                        _editorStylePreset = null;
                    }

                    _armedDeletePreset = null;
                    _editorStatus = $"Deleted '{name}'";
                    RefreshStyleEditorPresets();
                    InvalidateStyleComparison();
                    _layoutEditorRect.height = 0f;
                }
                else
                {
                    _armedDeletePreset = null;
                    _editorStatus = $"Could not delete '{name}' (see log)";
                }

                GUIUtility.ExitGUI();
            }

            GUI.color = previousColor;
        }

        private void SaveGlobalStyleAsPreset()
        {
            string trimmed = (_presetSaveName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                _editorStatus = "Enter a preset name first";
                return;
            }

            if (StylePresets.IsBuiltin(trimmed))
            {
                _editorStatus = $"'{trimmed}' is a shipped preset; pick a different name";
                return;
            }
            CommitDueStyleTextEdits(true);
            if (StylePresets.SaveCurrent(trimmed))
            {
                Plugin.SelectedStylePreset.Value = trimmed;
                _presetSaveName = string.Empty;
                _editorStatus = $"Saved '{trimmed}'";
                RefreshStyleEditorPresets();
                InvalidateStyleComparison();
                _layoutEditorRect.height = 0f;
            }
            else
            {
                _editorStatus = $"Could not save '{trimmed}' (see log)";
            }
        }
        private void DrawSectionFoldout(string title, ref bool expanded)
        {
            if (GUILayout.Button((expanded ? "▼ " : "▶ ") + title, GUILayout.ExpandWidth(true)))
            {
                expanded = !expanded;
                _armedDeletePreset = null;
                _layoutEditorRect.height = 0f;
                GUIUtility.ExitGUI();
            }
        }

        private void DrawStyleToggle(string label, ConfigEntry<bool> entry)
        {
            bool value = entry.Value;
            bool next = GUILayout.Toggle(value, " " + label);
            if (next != value)
            {
                SetStyleValue(entry, next);
            }
        }

        private void DrawStyleSlider(
            string label,
            ConfigEntry<float> entry,
            float fallbackMin,
            float fallbackMax,
            string format = "0.00")
        {
            float min = fallbackMin;
            float max = fallbackMax;
            if (entry.Description?.AcceptableValues is AcceptableValueRange<float> range)
            {
                min = range.MinValue;
                max = range.MaxValue;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(StyleLabelWidth));
            float value = entry.Value;
            float next = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
            GUILayout.Label(value.ToString(format, CultureInfo.InvariantCulture), GUILayout.Width(StyleValueWidth));
            GUILayout.EndHorizontal();
            if (!Mathf.Approximately(next, value))
            {
                SetStyleValue(entry, next);
            }
        }

        private void DrawStyleEnumCycle<T>(string label, ConfigEntry<T> entry) where T : struct, Enum
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(StyleLabelWidth));
            T[] values = (T[])Enum.GetValues(typeof(T));
            int index = Array.IndexOf(values, entry.Value);
            if (GUILayout.Button("◀", GUILayout.Width(28f)) && values.Length > 0)
            {
                SetStyleValue(entry, values[(index - 1 + values.Length) % values.Length]);
            }

            GUILayout.Label(entry.Value.ToString(), GUILayout.ExpandWidth(true));
            if (GUILayout.Button("▶", GUILayout.Width(28f)) && values.Length > 0)
            {
                SetStyleValue(entry, values[(index + 1) % values.Length]);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawStyleTextField(string label, ConfigEntry<string> entry)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(StyleLabelWidth));
            string current = entry.Value ?? string.Empty;
            string displayed = _pendingStyleTextEdits.TryGetValue(entry, out PendingTextEdit pending)
                ? pending.Value
                : current;
            string edited = GUILayout.TextField(displayed, GUILayout.ExpandWidth(true));
            if (edited != displayed)
            {
                _pendingStyleTextEdits[entry] = new PendingTextEdit
                {
                    Value = edited,
                    Deadline = Time.realtimeSinceStartup + StyleEditCommitDelaySeconds
                };
            }

            GUILayout.EndHorizontal();
        }

        private void DrawStyleColor(string label, ConfigEntry<Color> entry, ref bool expanded)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(StyleLabelWidth));
            Rect swatch = GUILayoutUtility.GetRect(44f, 16f, GUILayout.Width(44f));
            Color previousColor = GUI.color;
            GUI.color = new Color(entry.Value.r, entry.Value.g, entry.Value.b, 1f);
            GUI.DrawTexture(swatch, Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(expanded ? "▼" : "▶", GUILayout.Width(28f)))
            {
                expanded = !expanded;
                _layoutEditorRect.height = 0f;
                GUILayout.EndHorizontal();
                GUIUtility.ExitGUI();
            }

            GUILayout.EndHorizontal();
            if (!expanded)
            {
                return;
            }

            Color value = entry.Value;
            Color next = value;
            next.r = DrawColorChannel("R", value.r);
            next.g = DrawColorChannel("G", value.g);
            next.b = DrawColorChannel("B", value.b);
            next.a = DrawColorChannel("A", value.a);
            if (next != value)
            {
                SetStyleValue(entry, next);
            }
        }

        private float DrawColorChannel(string channel, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(channel, GUILayout.Width(StyleLabelWidth));
            float next = GUILayout.HorizontalSlider(value, 0f, 1f, GUILayout.ExpandWidth(true));
            GUILayout.Label(
                Mathf.RoundToInt(value * 255f).ToString(CultureInfo.InvariantCulture),
                GUILayout.Width(StyleValueWidth));
            GUILayout.EndHorizontal();
            return next;
        }
        private void DrawFontFileControl()
        {
            _styleEditorFontFiles ??= ScanStyleEditorFontFiles();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Font File", GUILayout.Width(StyleLabelWidth));
            if (GUILayout.Button(_fontFileListExpanded ? "▼" : "▶", GUILayout.Width(28f)))
            {
                _fontFileListExpanded = !_fontFileListExpanded;
                _layoutEditorRect.height = 0f;
                GUILayout.EndHorizontal();
                GUIUtility.ExitGUI();
            }

            GUILayout.Label(Plugin.CustomFontFile.Value ?? string.Empty, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("◀", GUILayout.Width(28f)))
            {
                CycleFontFile(-1);
            }

            if (GUILayout.Button("▶", GUILayout.Width(28f)))
            {
                CycleFontFile(1);
            }

            GUILayout.EndHorizontal();
            if (!_fontFileListExpanded)
            {
                return;
            }

            BeginStyleIndent();
            string[] files = _styleEditorFontFiles;
            if (files.Length == 0)
            {
                GUILayout.Label(
                    "No font files found. Drop .ttf/.otf files or TMP font bundles into the fonts folder, "
                    + "then use Rescan in the preset section.",
                    WrappedLabelStyle);
            }
            string currentFile = Plugin.CustomFontFile.Value ?? string.Empty;
            int separatorIndex = currentFile.IndexOf(':');
            if (separatorIndex > 0)
            {
                currentFile = currentFile.Substring(0, separatorIndex);
            }

            currentFile = currentFile.Trim();
            foreach (string fileName in files)
            {
                Color previousColor = GUI.color;
                if (string.Equals(fileName, currentFile, StringComparison.OrdinalIgnoreCase))
                {
                    GUI.color = Color.green;
                }

                if (GUILayout.Button(fileName, GUILayout.ExpandWidth(true)))
                {
                    GUI.color = previousColor;
                    SelectFontFile(fileName);
                    GUIUtility.ExitGUI();
                }

                GUI.color = previousColor;
            }

            DrawBundleAssetField();
            EndStyleIndent();
        }
        private void DrawBundleAssetField()
        {
            ConfigEntry<string> entry = Plugin.CustomFontFile;
            string current = entry.Value ?? string.Empty;
            string baseline = current.IndexOf(':') > 0 ? current : string.Empty;
            string displayed = _pendingStyleTextEdits.TryGetValue(entry, out PendingTextEdit pending)
                ? pending.Value
                : baseline;

            GUILayout.BeginHorizontal();
            GUILayout.Label("bundle:asset", GUILayout.Width(StyleLabelWidth));
            string edited = GUILayout.TextField(displayed, GUILayout.ExpandWidth(true));
            if (edited != displayed)
            {
                _pendingStyleTextEdits[entry] = new PendingTextEdit
                {
                    Value = edited,
                    Deadline = Time.realtimeSinceStartup + StyleEditCommitDelaySeconds
                };
            }

            GUILayout.EndHorizontal();
        }
        private void SelectFontFile(string fileName)
        {
            _pendingStyleTextEdits.Remove(Plugin.CustomFontFile);
            SetStyleValue(Plugin.CustomFontFile, fileName);
            if (Plugin.ScopeFontSource.Value != ScopeFontSource.CustomFont)
            {
                SetStyleValue(Plugin.ScopeFontSource, ScopeFontSource.CustomFont);
            }
        }

        private void CycleFontFile(int direction)
        {
            string[] files = _styleEditorFontFiles;
            if (files == null || files.Length == 0)
            {
                _editorStatus = "No font files found — Rescan after adding some";
                return;
            }
            string current = Plugin.CustomFontFile.Value ?? string.Empty;
            int separatorIndex = current.IndexOf(':');
            if (separatorIndex > 0)
            {
                current = current.Substring(0, separatorIndex);
            }

            current = current.Trim();
            int index = Array.FindIndex(
                files, file => string.Equals(file, current, StringComparison.OrdinalIgnoreCase));
            index = index < 0 ? 0 : (index + direction + files.Length) % files.Length;
            SelectFontFile(files[index]);
        }

        private static string[] ScanStyleEditorFontFiles()
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
                return fontFiles.ToArray();
            }
            catch (Exception exception)
            {
                Plugin.LogSource?.LogWarning($"Could not scan the fonts folder: {exception.Message}");
                return Array.Empty<string>();
            }
        }
    }
}
