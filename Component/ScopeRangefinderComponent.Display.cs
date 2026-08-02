using EFT.Animations;
using EFT.CameraControl;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
        private bool _distanceTextDirty = true;
        private string _lastRenderedDistanceText;

        private bool ApplyDisplayLayout()
        {
            if (_panelRect == null || _canvasRect == null)
            {
                return false;
            }

            ApplyScreenOverlayCanvasMode();
            SetPanelAnchors(new Vector2(0.5f, 0.30f));
            _panelRect.localScale = Vector3.one;
            _panelRect.anchoredPosition = new Vector2(
                ScopeDisplayStyle.DefaultOffsetX + Plugin.DisplayOffsetX.Value,
                ScopeDisplayStyle.DefaultOffsetY + Plugin.DisplayOffsetY.Value);
            return true;
        }

        private void ApplyScreenOverlayCanvasMode()
        {
            if (_canvas == null)
            {
                return;
            }

            ApplyScaledScreenCanvasMode();
            if (_canvas.transform.parent != transform)
            {
                _canvas.transform.SetParent(transform, false);
            }

            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.worldCamera = null;
            _canvas.pixelPerfect = true;
            _canvas.sortingOrder = OverlaySortingOrder;
            _canvas.overrideSorting = true;
        }

        private void ApplyScaledScreenCanvasMode()
        {
            if (_canvasScaler == null)
            {
                return;
            }

            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _canvasScaler.referenceResolution = new Vector2(1920, 1080);
            _canvasScaler.matchWidthOrHeight = 0.5f;
            _canvasScaler.referencePixelsPerUnit = 100f;
        }

        private static float ResolveLayoutUiScale(float scaleAdjustment)
        {
            return Mathf.Clamp(
                ScopeCanvasDefaultUiScale + scaleAdjustment * ScopeCanvasScaleSensitivity,
                ScopeCanvasMinUiScale,
                ScopeCanvasMaxUiScale);
        }

        private void SetPanelAnchors(Vector2 anchor)
        {
            if (_panelRect.anchorMin == anchor && _panelRect.anchorMax == anchor)
            {
                return;
            }

            _panelRect.anchorMin = anchor;
            _panelRect.anchorMax = anchor;
        }

        private static string ResolveScopeLayoutKey(
            OpticSight currentOpticSight,
            ProceduralWeaponAnimation weaponAnimation)
        {
            string templateId = weaponAnimation?.CurrentScope?.Mod?.Item?.StringTemplateId;
            if (!string.IsNullOrEmpty(templateId))
            {
                return templateId;
            }

            string opticName = currentOpticSight?.name;
            if (!string.IsNullOrEmpty(opticName))
            {
                return opticName;
            }

            return weaponAnimation?.CurrentScope?.ScopePrefabCache?.name;
        }

        private void LogLayoutKeyOnce(string layoutKey, OpticSight currentOpticSight)
        {
            if (!Plugin.LogScopeKeys.Value || string.IsNullOrEmpty(layoutKey) || layoutKey == _lastLoggedLayoutKey)
            {
                return;
            }

            _lastLoggedLayoutKey = layoutKey;
            Plugin.LogSource?.LogInfo($"Using scope layout key '{layoutKey}'.");
        }

        private const float MetersPerYard = 0.9144f;
        private string BuildDistanceText(bool includeZeroLine = true)
        {
            string rangeValue = _lastRaycastHit
                ? FormatDistanceValue(GetDisplayDistance())
                : Plugin.NoDistanceText.Value;

            if (!includeZeroLine || !Plugin.ShowZeroLine.Value)
            {
                return rangeValue;
            }

            return ComposeReadoutLine(Plugin.RangeLinePrefix.Value, rangeValue)
                + "\n"
                + ComposeReadoutLine(Plugin.ZeroLinePrefix.Value, BuildZeroValueText());
        }
        private string BuildZeroValueText()
        {
            EFT.InventoryLogic.SightComponent currentSight = _activeWeaponAnimation?.CurrentAimingMod;

            if (Plugin.AutoZeroEnabled.Value && IsAutoZeroEffective(currentSight))
            {
                if (Plugin.AutoZeroMode.Value == AutoZeroMode.Continuous)
                {
                    return "auto";
                }

                if (_autoZeroLastDistance > 0)
                {
                    return FormatDistanceValue(_autoZeroLastDistance);
                }
            }

            if (currentSight != null
                && currentSight.HasOpticCalibrationPoints(currentSight.SelectedScopeIndex))
            {
                return FormatDistanceValue(currentSight.GetCurrentOpticCalibrationDistance());
            }

            return Plugin.NoDistanceText.Value;
        }

        private static string ComposeReadoutLine(string prefix, string value)
        {
            prefix = prefix?.Trim();
            return string.IsNullOrEmpty(prefix) ? value : prefix + " " + value;
        }

        private static string FormatDistanceValue(float meters)
        {
            float displayDistance = ConvertToDisplayUnit(meters);
            string suffix = Plugin.ShowUnitSuffix.Value ? GetUnitSuffix() : string.Empty;

            if (Plugin.UseDecimalFormat.Value)
            {
                return Mathf.Clamp(displayDistance, 0f, 999f).ToString("000.0") + suffix;
            }

            return Mathf.Clamp(Mathf.RoundToInt(displayDistance), 0, 9999).ToString("D4") + suffix;
        }
        private static float ConvertToDisplayUnit(float meters)
        {
            return Plugin.DistanceUnit.Value == DistanceUnit.Yards ? meters / MetersPerYard : meters;
        }

        private static string GetUnitSuffix()
        {
            return Plugin.DistanceUnit.Value == DistanceUnit.Yards ? "yd" : "m";
        }
        internal static string FormatPanelDistance(int meters)
        {
            return Mathf.RoundToInt(ConvertToDisplayUnit(meters)) + GetUnitSuffix();
        }
        internal static string BuildSampleDistanceText()
        {
            string rangeValue = FormatDistanceValue(123.4f);
            if (!Plugin.ShowZeroLine.Value)
            {
                return rangeValue;
            }

            return ComposeReadoutLine(Plugin.RangeLinePrefix.Value, rangeValue)
                + "\n"
                + ComposeReadoutLine(Plugin.ZeroLinePrefix.Value, FormatDistanceValue(400f));
        }

        internal void MarkDistanceTextDirty()
        {
            _distanceTextDirty = true;
        }

        private void UpdateDistanceTextIfDirty()
        {
            if (!_distanceTextDirty || _distanceText == null)
            {
                return;
            }

            string text = BuildDistanceText(includeZeroLine: false);
            if (text == _lastRenderedDistanceText)
            {
                _distanceTextDirty = false;
                return;
            }

            _distanceText.text = text;
            _lastRenderedDistanceText = text;
            _distanceTextDirty = false;
        }

        private void UpdateDistanceText()
        {
            _distanceTextDirty = true;
            UpdateDistanceTextIfDirty();
        }

        private float GetDisplayDistance()
        {
            return _lastMeasuredDistance;
        }

        private void CreateOverlay()
        {
            var canvasObject = new GameObject("ScopeRangefinderCanvas");
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = OverlaySortingOrder;
            _canvas.overrideSorting = true;
            _canvasRect = canvasObject.GetComponent<RectTransform>();

            _canvasScaler = canvasObject.AddComponent<CanvasScaler>();
            ApplyScaledScreenCanvasMode();

            RectTransform panelRect = ScopeDisplayStyle.CreateDisplayPanel(canvasObject.transform);

            _panelRect = panelRect;
            _distanceText = ScopeDisplayStyle.CreateReadoutText(panelRect);
        }
    }
}
