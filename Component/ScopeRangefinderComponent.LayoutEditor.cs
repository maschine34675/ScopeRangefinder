using System.Globalization;
using UnityEngine;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
        private const float LayoutEditorOffsetStep = 0.002f;
        private const float LayoutEditorScaleStep = 0.0025f;
        private const float LayoutEditorMinimumScaleAdjustment = -0.045f;
        private const float LayoutEditorMaximumScaleAdjustment = 0.20f;

        private bool _layoutEditorVisible;
        private Rect _layoutEditorRect = new Rect(24f, 120f, 360f, 290f);
        private string _layoutEditorKey;
        private float _editorOffsetX;
        private float _editorOffsetY;
        private float _editorScale;
        private string _editorStatus = string.Empty;
        private bool _savedCursorVisible;
        private CursorLockMode _savedCursorLockState;
        private bool _hasSavedCursorState;
        internal static bool BlocksGameMouseInput { get; private set; }

        private void HandleLayoutEditorHotkey()
        {
            if (Plugin.LayoutEditorToggle.Value.IsDown())
            {
                SetLayoutEditorVisible(!_layoutEditorVisible);
            }

            if (_layoutEditorVisible)
            {
                ShowLayoutEditorCursor();
            }
        }

        private ScopeLayoutEntry GetLayoutForDisplay(string layoutKey)
        {
            if (_layoutEditorVisible && !string.IsNullOrEmpty(layoutKey))
            {
                EnsureEditorDraft(layoutKey);
                return new ScopeLayoutEntry
                {
                    OffsetX = _editorOffsetX,
                    OffsetY = _editorOffsetY,
                    Scale = _editorScale
                };
            }

            return Plugin.ScopeLayouts?.GetForScope(layoutKey) ?? new ScopeLayoutEntry();
        }

        private void OnGUI()
        {
            if (!_layoutEditorVisible)
            {
                BlocksGameMouseInput = false;
                return;
            }

            BlocksGameMouseInput = IsMouseOverEditorWindow();
            _layoutEditorRect = GUILayout.Window(
                34675,
                _layoutEditorRect,
                DrawLayoutEditorWindow,
                "Scope Rangefinder Layout");
        }

        private void DrawLayoutEditorWindow(int windowId)
        {
            string currentKey = string.IsNullOrEmpty(_currentLayoutKey) ? "(no active scope)" : _currentLayoutKey;
            if (Event.current.type == EventType.Layout)
            {
                EnsureEditorDraft(_currentLayoutKey);
            }

            GUILayout.Label("Current Scope Key");
            GUILayout.BeginHorizontal();
            GUILayout.TextField(currentKey);
            if (GUILayout.Button("Copy", GUILayout.Width(64f)))
            {
                GUIUtility.systemCopyBuffer = _currentLayoutKey ?? string.Empty;
                _editorStatus = "Key copied";
                GUIUtility.ExitGUI();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            _editorOffsetX = DrawFloatControl("OffsetX", _editorOffsetX, LayoutEditorOffsetStep, "◀", "▶");
            _editorOffsetY = DrawFloatControl("OffsetY", _editorOffsetY, LayoutEditorOffsetStep, "▼", "▲");
            _editorScale = Mathf.Clamp(
                DrawFloatControl("Scale", _editorScale, LayoutEditorScaleStep),
                LayoutEditorMinimumScaleAdjustment,
                LayoutEditorMaximumScaleAdjustment);

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            GUI.enabled = !string.IsNullOrEmpty(_currentLayoutKey);
            if (GUILayout.Button("Save"))
            {
                SaveEditorDraft();
                GUI.enabled = true;
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("Reset"))
            {
                ResetEditorDraft();
                GUI.enabled = true;
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("Close"))
            {
                SetLayoutEditorVisible(false);
                GUI.enabled = true;
                GUIUtility.ExitGUI();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_editorStatus))
            {
                GUILayout.Label(_editorStatus);
            }

            GUI.DragWindow();
        }

        private void SetLayoutEditorVisible(bool visible)
        {
            if (_layoutEditorVisible == visible)
            {
                return;
            }

            _layoutEditorVisible = visible;
            if (visible)
            {
                ShowLayoutEditorCursor();
                LoadEditorDraftForCurrentScope();
                return;
            }

            BlocksGameMouseInput = false;
            RestoreLayoutEditorCursor();
        }

        private void ShowLayoutEditorCursor()
        {
            if (!_hasSavedCursorState)
            {
                _savedCursorVisible = Cursor.visible;
                _savedCursorLockState = Cursor.lockState;
                _hasSavedCursorState = true;
            }

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void RestoreLayoutEditorCursor()
        {
            if (!_hasSavedCursorState)
            {
                return;
            }

            Cursor.visible = _savedCursorVisible;
            Cursor.lockState = _savedCursorLockState;
            _hasSavedCursorState = false;
        }

        private bool IsMouseOverEditorWindow()
        {
            Vector2 mousePosition = Event.current?.mousePosition ?? new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            return _layoutEditorRect.Contains(mousePosition);
        }

        private float DrawFloatControl(
            string label,
            float value,
            float step,
            string decreaseGlyph = "-",
            string increaseGlyph = "+")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(58f));

            if (GUILayout.Button(decreaseGlyph + decreaseGlyph, GUILayout.Width(42f)))
            {
                value -= step * 10f;
            }

            if (GUILayout.Button(decreaseGlyph, GUILayout.Width(32f)))
            {
                value -= step;
            }

            string text = GUILayout.TextField(value.ToString("0.#####", CultureInfo.InvariantCulture), GUILayout.Width(86f));
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                && !float.IsNaN(parsed)
                && !float.IsInfinity(parsed))
            {
                value = parsed;
            }

            if (GUILayout.Button(increaseGlyph, GUILayout.Width(32f)))
            {
                value += step;
            }

            if (GUILayout.Button(increaseGlyph + increaseGlyph, GUILayout.Width(42f)))
            {
                value += step * 10f;
            }

            GUILayout.EndHorizontal();
            return value;
        }

        private void EnsureEditorDraft(string layoutKey)
        {
            if (!_layoutEditorVisible || string.IsNullOrEmpty(layoutKey) || _layoutEditorKey == layoutKey)
            {
                return;
            }

            LoadEditorDraft(layoutKey);
        }

        private void LoadEditorDraftForCurrentScope()
        {
            LoadEditorDraft(_currentLayoutKey);
        }

        private void LoadEditorDraft(string layoutKey)
        {
            _layoutEditorKey = layoutKey;
            _editorStatus = string.Empty;

            ScopeLayoutEntry layout = Plugin.ScopeLayouts?.GetForScope(layoutKey) ?? new ScopeLayoutEntry();
            _editorOffsetX = layout.OffsetX ?? 0f;
            _editorOffsetY = layout.OffsetY ?? 0f;
            _editorScale = layout.Scale ?? 0f;
        }

        private void SaveEditorDraft()
        {
            if (string.IsNullOrEmpty(_currentLayoutKey))
            {
                _editorStatus = "No active scope";
                return;
            }

            Plugin.ScopeLayouts ??= ScopeLayoutConfig.LoadOrCreate();
            Plugin.ScopeLayouts.SetForScope(
                _currentLayoutKey,
                new ScopeLayoutEntry
                {
                    OffsetX = _editorOffsetX,
                    OffsetY = _editorOffsetY,
                    Scale = _editorScale
                });

            _layoutEditorKey = _currentLayoutKey;
            _editorStatus = Plugin.ScopeLayouts.Save() ? "Saved to JSON" : "Save failed";
        }

        private void ResetEditorDraft()
        {
            if (string.IsNullOrEmpty(_currentLayoutKey))
            {
                _editorStatus = "No active scope";
                return;
            }

            Plugin.ScopeLayouts?.ResetScope(_currentLayoutKey);
            bool saved = Plugin.ScopeLayouts?.Save() ?? false;
            _layoutEditorKey = null;
            LoadEditorDraft(_currentLayoutKey);
            _editorStatus = saved ? "Reset to presets" : "Reset failed";
        }
    }
}
