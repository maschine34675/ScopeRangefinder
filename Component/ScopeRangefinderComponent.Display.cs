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
            if (string.IsNullOrEmpty(layoutKey) || layoutKey == _lastLoggedLayoutKey)
            {
                return;
            }

            _lastLoggedLayoutKey = layoutKey;
            Plugin.LogSource?.LogDebug($"Using scope layout key '{layoutKey}'.");
        }

        private string BuildDistanceText()
        {
            if (!_lastRaycastHit)
            {
                return Plugin.NoDistanceText.Value;
            }

            if (Plugin.UseDecimalFormat.Value)
            {
                float clamped = Mathf.Clamp(GetDisplayDistance(), 0f, 999f);
                return clamped.ToString("000.0");
            }

            int meters = Mathf.Clamp(Mathf.RoundToInt(GetDisplayDistance()), 0, 9999);
            return meters.ToString("D4");
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
