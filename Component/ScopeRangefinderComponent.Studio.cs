using System;
using BepInEx.Configuration;
using UnityEngine;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
        private StyleStudioController _styleStudio;

        private StyleStudioController StyleStudio => _styleStudio ??= new StyleStudioController(this);
        private void UpdateStyleStudio()
        {
            _styleStudio?.Pump();
        }

        private void DestroyStyleStudio()
        {
            _styleStudio?.Dispose();
            _styleStudio = null;
        }

        internal void ToggleStyleStudio()
        {
            StyleStudio.Toggle();
        }

        internal bool IsStyleStudioOpen => _styleStudio != null && _styleStudio.IsOpen;

        internal string StyleStudioUnavailableReason => StyleStudio.UnavailableReason;
        private void NotifyStyleStudioStateChanged()
        {
            _styleStudio?.MarkStateDirty();
        }

        internal void SetStyleValueFromStudio(ConfigEntryBase entry, object typedValue)
        {
            ConfigFile config = Plugin.ConfigInstance;
            bool previousSaveOnSet = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            try
            {
                entry.BoxedValue = typedValue;
            }
            finally
            {
                config.SaveOnConfigSet = previousSaveOnSet;
            }

            _styleConfigSaveDueAt = Time.realtimeSinceStartup + StyleSaveDebounceSeconds;
            InvalidateStyleComparison();
        }

        internal void FlushStudioTextEdits()
        {
            CommitDueStyleTextEdits(true);
        }

        internal void SelectFontFileFromStudio(string fileName)
        {
            SelectFontFile(fileName);
        }

        internal static string[] ListFontFilesForStudio()
        {
            return ScanStyleEditorFontFiles();
        }

        internal bool IsAimingForStudio()
        {
            return _isScoped;
        }
        internal void GetStudioScopeInfo(out string scopeKey, out string assignedPreset, out bool aiming)
        {
            aiming = _isScoped && !string.IsNullOrEmpty(_currentLayoutKey);
            scopeKey = aiming ? _currentLayoutKey : null;
            if (!aiming)
            {
                assignedPreset = null;
                return;
            }
            assignedPreset = _layoutEditorVisible
                ? _editorStylePreset
                : Plugin.ScopeLayouts?.GetForScope(_currentLayoutKey)?.StylePreset;
        }
        internal bool TryAssignScopePresetFromStudio(string presetName, out string status)
        {
            if (!_isScoped || string.IsNullOrEmpty(_currentLayoutKey))
            {
                status = "Aim through a scope to assign a preset to it";
                return false;
            }

            if (presetName != null && !StylePresets.TryGetPresetValues(presetName, out _))
            {
                status = $"Preset '{presetName}' does not exist";
                return false;
            }

            Plugin.ScopeLayouts ??= ScopeLayoutConfig.LoadOrCreate();
            ScopeLayoutEntry existing = Plugin.ScopeLayouts.GetRawForScope(_currentLayoutKey) ?? new ScopeLayoutEntry();
            existing.StylePreset = presetName;
            Plugin.ScopeLayouts.SetForScope(_currentLayoutKey, existing);
            bool saved = Plugin.ScopeLayouts.Save();
            _editorStylePreset = presetName;
            InvalidateStyleOverrideCache();

            status = saved
                ? (presetName == null ? "Scope set to the global style" : $"'{presetName}' assigned to this scope")
                : "Could not save the scope layout (see log)";
            return saved;
        }
        internal void ClearScopeAssignmentIfPreset(string presetName)
        {
            if (string.Equals(presetName, _editorStylePreset, StringComparison.OrdinalIgnoreCase))
            {
                _editorStylePreset = null;
            }
        }
    }
}
