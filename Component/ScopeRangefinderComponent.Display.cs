using EFT.Animations;
using EFT.CameraControl;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
        private bool _distanceTextDirty = true;
        private string _overlayLastRenderedText;
        private bool _overlayDisplayVisible;
        private Image[] _panelImages;
        private Outline _overlayOutline;
        private RectTransform _distanceTextRect;

        private const float OverlayBasePanelWidth = 142f;
        private const float OverlayBasePanelHeight = 46f;
        private string _lastRenderedDistanceText;

        private bool ApplyDisplayLayout()
        {
            if (_panelRect == null || _canvasRect == null)
            {
                return false;
            }

            ApplyScreenOverlayCanvasMode();
            SetPanelAnchors(new Vector2(0.5f, 0.30f));
            ScopeLayoutEntry layout = GetLayoutForDisplay(_currentLayoutKey);
            float layoutOffsetX = (layout.OffsetX ?? 0f) * 1920f;
            float layoutOffsetY = (layout.OffsetY ?? 0f) * 1080f;
            float scaleFactor = ResolveLayoutUiScale(layout.Scale ?? 0f) / ScopeCanvasDefaultUiScale;

            _panelRect.localScale = Vector3.one * scaleFactor;
            _panelRect.anchoredPosition = new Vector2(
                ScopeDisplayStyle.DefaultOffsetX + layoutOffsetX,
                ScopeDisplayStyle.DefaultOffsetY + layoutOffsetY);
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
        private void LogLayoutKeyOnce(string layoutKey)
        {
            if (!Plugin.LogScopeKeys.Value || string.IsNullOrEmpty(layoutKey) || layoutKey == _lastLoggedLayoutKey)
            {
                return;
            }

            _lastLoggedLayoutKey = layoutKey;
            string path = _usingMainCameraScope ? "screen overlay" : "in-scope";
            Plugin.LogSource?.LogInfo($"Using scope layout key '{layoutKey}' ({path}).");
        }

        private const float MetersPerYard = 0.9144f;
        internal static int ConfiguredReadoutRows()
        {
            int rows = 1;
            if (ActiveStyle.ShowZeroLine)
            {
                rows++;
            }

            if (ActiveStyle.BallisticsLine != BallisticsLineMode.Off)
            {
                rows++;
            }

            return rows;
        }
        internal static float ReadoutPlateWidthFactor(int rows)
        {
            if (rows <= 1)
            {
                return 1f;
            }

            return ActiveStyle.BallisticsLine == BallisticsLineMode.Dial ? 2.6f : 1.6f;
        }

        internal static float ReadoutPlateHeightFactor(int rows)
        {
            return rows <= 1 ? 1f : rows == 2 ? 1.85f : 2.65f;
        }

        private string BuildDistanceText()
        {
            string rangeValue = _lastRaycastHit
                ? FormatDistanceValue(GetDisplayDistance())
                : ActiveStyle.NoDistanceText;

            bool zeroLine = ActiveStyle.ShowZeroLine;
            bool ballisticsLine = ActiveStyle.BallisticsLine != BallisticsLineMode.Off;
            if (!zeroLine && !ballisticsLine)
            {
                return rangeValue;
            }

            string text = ComposeReadoutLine(ActiveStyle.RangeLinePrefix, rangeValue);
            if (zeroLine)
            {
                text += "\n" + ComposeReadoutLine(ActiveStyle.ZeroLinePrefix, BuildZeroValueText());
            }

            if (ballisticsLine)
            {
                text += "\n" + BuildBallisticsLineText();
            }

            return text;
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

            return ActiveStyle.NoDistanceText;
        }
        private static int ActivePrefixWidth()
        {
            if (ConfiguredReadoutRows() <= 1)
            {
                return 0;
            }

            int width = (ActiveStyle.RangeLinePrefix ?? string.Empty).Trim().Length;
            if (ActiveStyle.ShowZeroLine)
            {
                width = Mathf.Max(width, (ActiveStyle.ZeroLinePrefix ?? string.Empty).Trim().Length);
            }

            switch (ActiveStyle.BallisticsLine)
            {
                case BallisticsLineMode.Hold:
                    width = Mathf.Max(width, HoldLinePrefix.Length);
                    break;
                case BallisticsLineMode.Dial:
                    width = Mathf.Max(width, DialLinePrefix.Length);
                    break;
            }

            return width;
        }

        private static string ComposeReadoutLine(string prefix, string value)
        {
            prefix = prefix?.Trim();
            int width = ActivePrefixWidth();
            if (width <= 0)
            {
                return string.IsNullOrEmpty(prefix) ? value : prefix + " " + value;
            }
            return (prefix ?? string.Empty).PadRight(width) + " " + value;
        }
        internal static string BuildWidestReadoutText()
        {
            string widestDistance = FormatDistanceValue(8888f);
            string noTarget = ActiveStyle.NoDistanceText ?? string.Empty;
            if (noTarget.Length > widestDistance.Length)
            {
                widestDistance = noTarget;
            }

            if (ConfiguredReadoutRows() <= 1)
            {
                return widestDistance;
            }

            string text = ComposeReadoutLine(ActiveStyle.RangeLinePrefix, widestDistance);
            if (ActiveStyle.ShowZeroLine)
            {
                text += "\n" + ComposeReadoutLine(ActiveStyle.ZeroLinePrefix, widestDistance);
            }
            string widestHold = FormatHoldValue(-12.3f, 1500);
            switch (ActiveStyle.BallisticsLine)
            {
                case BallisticsLineMode.Hold:
                    text += "\n" + ComposeReadoutLine(HoldLinePrefix, widestHold);
                    break;
                case BallisticsLineMode.Dial:
                    text += "\n" + ComposeReadoutLine(DialLinePrefix, widestDistance) + " " + widestHold;
                    break;
            }

            return text;
        }

        private static string FormatDistanceValue(float meters)
        {
            float displayDistance = ConvertToDisplayUnit(meters);
            string suffix = ActiveStyle.ShowUnitSuffix ? GetUnitSuffix() : string.Empty;

            if (ActiveStyle.UseDecimalFormat)
            {
                return Mathf.Clamp(displayDistance, 0f, 999f).ToString("000.0") + suffix;
            }

            return Mathf.Clamp(Mathf.RoundToInt(displayDistance), 0, 9999).ToString("D4") + suffix;
        }
        private static float ConvertToDisplayUnit(float meters)
        {
            return ActiveStyle.DistanceUnit == DistanceUnit.Yards ? meters / MetersPerYard : meters;
        }

        private static string GetUnitSuffix()
        {
            return ActiveStyle.DistanceUnit == DistanceUnit.Yards ? "yd" : "m";
        }
        internal static string FormatPanelDistance(int meters)
        {
            return Mathf.RoundToInt(ConvertToDisplayUnit(meters)) + GetUnitSuffix();
        }
        internal static string BuildSampleDistanceText()
        {
            string rangeValue = FormatDistanceValue(123.4f);
            bool zeroLine = ActiveStyle.ShowZeroLine;
            BallisticsLineMode ballisticsMode = ActiveStyle.BallisticsLine;
            if (!zeroLine && ballisticsMode == BallisticsLineMode.Off)
            {
                return rangeValue;
            }

            string text = ComposeReadoutLine(ActiveStyle.RangeLinePrefix, rangeValue);
            if (zeroLine)
            {
                text += "\n" + ComposeReadoutLine(ActiveStyle.ZeroLinePrefix, FormatDistanceValue(400f));
            }

            if (ballisticsMode == BallisticsLineMode.Dial)
            {
                text += "\n" + ComposeReadoutLine(DialLinePrefix, FormatDistanceValue(350f))
                    + " " + FormatHoldValue(0.4f, 123);
            }
            else if (ballisticsMode == BallisticsLineMode.Hold)
            {
                text += "\n" + ComposeReadoutLine(HoldLinePrefix, FormatHoldValue(1.2f, 123));
            }

            return text;
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

            string text = BuildDistanceText();
            if (text == _overlayLastRenderedText)
            {
                _distanceTextDirty = false;
                return;
            }

            _distanceText.text = text;
            _overlayLastRenderedText = text;
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
            _distanceTextRect = _distanceText.rectTransform;
            _panelImages = panelRect.GetComponentsInChildren<Image>(true);
            _overlayOutline = _distanceText.gameObject.AddComponent<Outline>();
            _overlayOutline.effectColor = Color.black;
            _overlayOutline.enabled = false;
        }
        private void ApplyOverlayAppearance()
        {
            if (_distanceText == null)
            {
                return;
            }

            Font font = ScopeDisplayStyle.LoadRangefinderFont();
            if (font != null && _distanceText.font != font)
            {
                _distanceText.font = font;
            }

            Color textColor = ActiveStyle.TextColor;
            if (_distanceText.color != textColor)
            {
                _distanceText.color = textColor;
            }

            int rows = ConfiguredReadoutRows();
            TextAnchor alignment = rows > 1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            if (_distanceText.alignment != alignment)
            {
                _distanceText.alignment = alignment;
            }

            if (_panelRect != null)
            {
                float width = OverlayBasePanelWidth
                    * (Mathf.Max(0.05f, ActiveStyle.BackgroundWidth) / 0.26f)
                    * ReadoutPlateWidthFactor(rows);
                float height = OverlayBasePanelHeight
                    * (Mathf.Max(0.03f, ActiveStyle.BackgroundHeight) / 0.11f)
                    * ReadoutPlateHeightFactor(rows);
                var panelSize = new Vector2(width, height);
                if (_panelRect.sizeDelta != panelSize)
                {
                    _panelRect.sizeDelta = panelSize;
                }
            }

            if (_panelImages != null)
            {
                bool showBackground = ActiveStyle.BackgroundVisible;
                Color backgroundColor = ActiveStyle.BackgroundColor;
                var accentColor = new Color(
                    Mathf.Clamp01(backgroundColor.r * 2.2f),
                    Mathf.Clamp01(backgroundColor.g * 2.2f),
                    Mathf.Clamp01(backgroundColor.b * 2.2f),
                    backgroundColor.a * 0.65f);
                for (int i = 0; i < _panelImages.Length; i++)
                {
                    Image image = _panelImages[i];
                    if (image == null)
                    {
                        continue;
                    }

                    if (image.enabled != showBackground)
                    {
                        image.enabled = showBackground;
                    }

                    Color layerColor = i == 0 ? backgroundColor : accentColor;
                    if (image.color != layerColor)
                    {
                        image.color = layerColor;
                    }
                }
            }

            if (_overlayOutline != null)
            {
                float outline = Mathf.Clamp01(ActiveStyle.TextOutline);
                bool outlineActive = outline > 0.001f;
                if (_overlayOutline.enabled != outlineActive)
                {
                    _overlayOutline.enabled = outlineActive;
                }

                if (outlineActive)
                {
                    Vector2 effectDistance = Vector2.one * (outline * 5f);
                    if (_overlayOutline.effectDistance != effectDistance)
                    {
                        _overlayOutline.effectDistance = effectDistance;
                    }
                }
            }

            if (_distanceTextRect != null)
            {
                float offsetY = ActiveStyle.TextOffsetY / 0.11f * OverlayBasePanelHeight;
                var anchored = new Vector2(0f, offsetY);
                if (_distanceTextRect.anchoredPosition != anchored)
                {
                    _distanceTextRect.anchoredPosition = anchored;
                }
            }
        }
    }
}
