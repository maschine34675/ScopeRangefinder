using EFT.Animations;
using EFT.CameraControl;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
        private bool ApplyDisplayLayout()
        {
            if (_panelRect == null || _canvasRect == null)
            {
                return false;
            }

            SetPanelAnchors(new Vector2(0.5f, 0.30f));
            _panelRect.localScale = Vector3.one;
            _panelRect.anchoredPosition = new Vector2(
                ScopeDisplayStyle.DefaultOffsetX + Plugin.DisplayOffsetX.Value,
                ScopeDisplayStyle.DefaultOffsetY + Plugin.DisplayOffsetY.Value);
            return true;
        }

        private bool ApplyProjectedOverlayLayout(
            Camera scopeCamera,
            OpticSight currentOpticSight,
            ProceduralWeaponAnimation weaponAnimation)
        {
            if (_panelRect == null || _canvasRect == null)
            {
                return false;
            }

            Camera displayCamera = CameraClass.Instance?.Camera ?? Camera.main ?? scopeCamera;
            if (displayCamera == null
                || !TryGetProjectedOverlayPoint(displayCamera, currentOpticSight, weaponAnimation, out Vector3 worldPoint))
            {
                return false;
            }

            Vector3 screenPoint = displayCamera.WorldToScreenPoint(worldPoint);
            if (screenPoint.z <= 0f)
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    screenPoint,
                    null,
                    out Vector2 localPoint))
            {
                return false;
            }

            string layoutKey = ResolveScopeLayoutKey(currentOpticSight, weaponAnimation);
            _currentLayoutKey = layoutKey;
            ScopeLayoutEntry layout = GetLayoutForDisplay(layoutKey);
            LogLayoutKeyOnce(layoutKey, currentOpticSight);

            float offsetX = layout.OffsetX ?? Plugin.ScopeLocalOffsetX.Value;
            float offsetY = layout.OffsetY ?? Plugin.ScopeLocalOffsetY.Value;
            float globalScale = Mathf.Max(0.0001f, Plugin.ScopeWorldScale.Value);
            float layoutScale = Mathf.Max(0.0001f, layout.Scale ?? ProjectedOverlayReferenceScale);
            float offsetPixelScale = Mathf.Max(1f, Mathf.Min(_canvasRect.rect.width, _canvasRect.rect.height))
                * ProjectedOverlayOffsetMultiplier;
            float uiScale = Mathf.Clamp(
                (globalScale / ProjectedOverlayReferenceScale)
                * (layoutScale / ProjectedOverlayReferenceScale)
                * ProjectedOverlayScaleMultiplier,
                0.25f,
                4f);

            SetPanelAnchors(new Vector2(0.5f, 0.5f));
            _panelRect.localScale = Vector3.one * uiScale;
            _panelRect.anchoredPosition = localPoint + new Vector2(
                offsetX * offsetPixelScale,
                offsetY * offsetPixelScale);

            ApplyProjectedOverlayAppearance(uiScale);
            return true;
        }

        private static bool TryGetProjectedOverlayPoint(
            Camera displayCamera,
            OpticSight currentOpticSight,
            ProceduralWeaponAnimation weaponAnimation,
            out Vector3 worldPoint)
        {
            worldPoint = default;

            Transform scopeBone = weaponAnimation?.CurrentScope?.Bone;
            if (scopeBone != null)
            {
                Vector3 aimDirection = GetScopeAimDirection(scopeBone);
                if (aimDirection.sqrMagnitude > 0.0001f)
                {
                    worldPoint = displayCamera.transform.position
                        + aimDirection.normalized * ProjectedOverlayAnchorDistance;
                    return true;
                }
            }

            Transform scopeDataTransform = currentOpticSight?.ScopeData?.transform;
            if (scopeDataTransform != null)
            {
                worldPoint = displayCamera.transform.position
                    + scopeDataTransform.forward * ProjectedOverlayAnchorDistance;
                return true;
            }

            Transform scopePrefabTransform = weaponAnimation?.CurrentScope?.ScopePrefabCache?.transform;
            if (scopePrefabTransform != null)
            {
                worldPoint = displayCamera.transform.position
                    + scopePrefabTransform.forward * ProjectedOverlayAnchorDistance;
                return true;
            }

            Transform opticTransform = currentOpticSight?.transform;
            if (opticTransform != null)
            {
                worldPoint = displayCamera.transform.position
                    + opticTransform.forward * ProjectedOverlayAnchorDistance;
                return true;
            }

            return false;
        }

        private static Vector3 GetScopeAimDirection(Transform scopeBone)
        {
            if (scopeBone == null)
            {
                return Vector3.zero;
            }

            return scopeBone.name == "aim_camera"
                ? scopeBone.up * -1f
                : scopeBone.forward * -1f;
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

        private bool ApplyWorldDisplayLayout(
            Camera scopeCamera,
            OpticSight currentOpticSight,
            ProceduralWeaponAnimation weaponAnimation)
        {
            Camera displayCamera = scopeCamera ?? CameraClass.Instance?.Camera;
            if (displayCamera == null)
            {
                return false;
            }

            EnsureWorldDisplay();
            string layoutKey = ResolveScopeLayoutKey(currentOpticSight, weaponAnimation);
            _currentLayoutKey = layoutKey;
            ScopeLayoutEntry layout = GetLayoutForDisplay(layoutKey);
            LogLayoutKeyOnce(layoutKey, currentOpticSight);

            Transform parent = displayCamera.transform;
            if (_worldParent != parent)
            {
                _worldParent = parent;
                _worldRoot.transform.SetParent(parent, false);
                ApplyLayerRecursively(_worldRoot, GetFirstCameraLayer(displayCamera));
            }

            float depth = Mathf.Max(displayCamera.nearClipPlane + 0.05f, ScopeDisplayDepth);

            float zoomCompensation = CalculateZoomCompensation(displayCamera, depth);
            float offsetX = layout.OffsetX ?? Plugin.ScopeLocalOffsetX.Value;
            float offsetY = layout.OffsetY ?? Plugin.ScopeLocalOffsetY.Value;
            float scale = Mathf.Max(0.0001f, layout.Scale ?? Plugin.ScopeWorldScale.Value);
            Vector3 localOffset = new Vector3(
                offsetX * zoomCompensation,
                offsetY * zoomCompensation,
                depth);
            Vector3 localScale = Vector3.one * scale * zoomCompensation;
            _worldRoot.transform.SetLocalPositionAndRotation(localOffset, Quaternion.identity);
            _worldRoot.transform.localScale = localScale;
            ApplyWorldTextOffset();
            ApplyWorldBackgroundSize();
            ApplyWorldColors();
            ApplyScopeAntialiasingOverride(displayCamera);
            SetWorldBackgroundVisible(Plugin.ScopeWorldBackground.Value);
            return true;
        }

        private static float CalculateZoomCompensation(Camera displayCamera, float depth)
        {
            if (!Plugin.ScopeCompensateZoomScale.Value || displayCamera == null)
            {
                return 1f;
            }

            float currentFov = Mathf.Clamp(displayCamera.fieldOfView, 1f, 170f);
            float referenceTan = Mathf.Tan(ScopeScaleReferenceFov * 0.5f * Mathf.Deg2Rad);
            float currentTan = Mathf.Tan(currentFov * 0.5f * Mathf.Deg2Rad);
            float depthFactor = Mathf.Max(0.01f, depth) / 0.25f;
            return (currentTan / referenceTan) * depthFactor;
        }

        private static int GetFirstCameraLayer(Camera camera)
        {
            int mask = camera.cullingMask;
            for (int layer = 0; layer < 32; layer++)
            {
                if ((mask & (1 << layer)) != 0)
                {
                    return layer;
                }
            }

            return camera.gameObject.layer;
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
            Plugin.LogSource?.LogInfo($"Using scope layout key '{layoutKey}'.");
        }

        private void EnsureWorldDisplay()
        {
            if (_worldRoot != null)
            {
                return;
            }

            _worldRoot = new GameObject("ScopeRangefinderWorldDisplay");
            _worldRoot.SetActive(false);

            GameObject textObject = new GameObject("DistanceText");
            textObject.transform.SetParent(_worldRoot.transform, false);

            _worldDistanceText = textObject.AddComponent<TextMesh>();
            _worldDistanceText.anchor = TextAnchor.MiddleCenter;
            _worldDistanceText.alignment = TextAlignment.Center;
            _worldDistanceText.fontSize = 96;
            _worldDistanceText.characterSize = 0.013333f;
            _worldDistanceText.fontStyle = FontStyle.Normal;
            _worldDistanceText.color = Plugin.ScopeWorldTextColor.Value;
            _worldDistanceText.text = Plugin.NoDistanceText.Value;

            Font font = Font.CreateDynamicFontFromOSFont(ScopeDisplayStyle.GetPreferredOsFontNames(), 96);
            if (font != null)
            {
                _worldDistanceText.font = font;
                Renderer renderer = textObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    _worldTextMaterial = new Material(font.material);
                    ConfigureOverlayMaterial(_worldTextMaterial, 4000);
                    renderer.sharedMaterial = _worldTextMaterial;
                }
            }

            _worldBackground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _worldBackground.name = "Background";
            _worldBackground.transform.SetParent(_worldRoot.transform, false);
            _worldBackground.transform.localPosition = new Vector3(0f, 0f, 0.001f);
            _worldBackground.transform.localScale = new Vector3(0.8f, 0.28f, 1f);
            Collider backgroundCollider = _worldBackground.GetComponent<Collider>();
            if (backgroundCollider != null)
            {
                Destroy(backgroundCollider);
            }
            Renderer backgroundRenderer = _worldBackground.GetComponent<Renderer>();
            if (backgroundRenderer != null)
            {
                _worldBackgroundMaterial = new Material(Shader.Find("GUI/Text Shader") ?? Shader.Find("Unlit/Color"));
                _worldBackgroundMaterial.color = Plugin.ScopeWorldBackgroundColor.Value;
                ConfigureOverlayMaterial(_worldBackgroundMaterial, 3999);
                backgroundRenderer.sharedMaterial = _worldBackgroundMaterial;
            }
            SetWorldBackgroundVisible(false);
        }

        private void SetWorldDisplayVisible(bool visible)
        {
            if (_worldRoot != null && _worldRoot.activeSelf != visible)
            {
                _worldRoot.SetActive(visible);
            }
        }

        private void SetWorldBackgroundVisible(bool visible)
        {
            if (_worldBackground != null && _worldBackground.activeSelf != visible)
            {
                _worldBackground.SetActive(visible);
            }
        }

        private void ApplyWorldBackgroundSize()
        {
            if (_worldBackground == null)
            {
                return;
            }

            float width = Mathf.Max(0.05f, Plugin.ScopeWorldBackgroundWidth.Value);
            float height = Mathf.Max(0.03f, Plugin.ScopeWorldBackgroundHeight.Value);
            _worldBackground.transform.localScale = new Vector3(width, height, 1f);
        }

        private void ApplyWorldTextOffset()
        {
            if (_worldDistanceText == null)
            {
                return;
            }

            _worldDistanceText.transform.localPosition = new Vector3(0f, Plugin.ScopeWorldTextOffsetY.Value, 0f);
        }

        private void ApplyWorldColors()
        {
            if (_worldDistanceText != null)
            {
                _worldDistanceText.color = Plugin.ScopeWorldTextColor.Value;
            }

            if (_worldBackgroundMaterial != null)
            {
                _worldBackgroundMaterial.color = Plugin.ScopeWorldBackgroundColor.Value;
            }
        }

        private void ApplyProjectedOverlayAppearance(float uiScale)
        {
            if (_distanceText != null)
            {
                _distanceText.font = ScopeDisplayStyle.LoadRangefinderFont();
                _distanceText.color = Plugin.ScopeWorldTextColor.Value;
                _distanceText.fontStyle = FontStyle.Normal;
            }

            if (_panelRect == null)
            {
                return;
            }

            float width = Mathf.Max(0.05f, Plugin.ScopeWorldBackgroundWidth.Value);
            float height = Mathf.Max(0.03f, Plugin.ScopeWorldBackgroundHeight.Value);
            float widthPixels = ProjectedOverlayReferencePanelWidth
                * (width / ProjectedOverlayReferenceBackgroundWidth);
            float heightPixels = ProjectedOverlayReferencePanelHeight
                * (height / ProjectedOverlayReferenceBackgroundHeight);
            _panelRect.sizeDelta = new Vector2(widthPixels, heightPixels);

            if (_distanceText != null)
            {
                RectTransform textRect = _distanceText.rectTransform;
                float referenceHeight = ProjectedOverlayReferencePanelHeight;
                float horizontalPadding = Mathf.Max(2f, referenceHeight * 0.13f);
                float verticalPadding = Mathf.Max(1f, referenceHeight * 0.065f);
                float textOffsetY = referenceHeight
                    * (Plugin.ScopeWorldTextOffsetY.Value / ProjectedOverlayReferenceBackgroundHeight);

                textRect.anchorMin = new Vector2(0.5f, 0.5f);
                textRect.anchorMax = new Vector2(0.5f, 0.5f);
                textRect.pivot = new Vector2(0.5f, 0.5f);
                textRect.sizeDelta = new Vector2(
                    Mathf.Max(12f, widthPixels - horizontalPadding * 2f),
                    Mathf.Max(8f, ProjectedOverlayReferencePanelHeight - verticalPadding * 2f));
                textRect.anchoredPosition = new Vector2(0f, textOffsetY);
                _distanceText.fontSize = Mathf.RoundToInt(Mathf.Max(12f, ProjectedOverlayReferencePanelHeight * 0.70f));
            }

            if (_panelBackgroundImages == null && _panelRect != null)
            {
                _panelBackgroundImages = _panelRect.GetComponentsInChildren<Image>(true);
            }

            if (_panelBackgroundImages == null)
            {
                return;
            }

            for (int i = 0; i < _panelBackgroundImages.Length; i++)
            {
                _panelBackgroundImages[i].enabled = Plugin.ScopeWorldBackground.Value && i == 0;
                _panelBackgroundImages[i].color = Plugin.ScopeWorldBackgroundColor.Value;
            }
        }

        private static void ConfigureOverlayMaterial(Material material, int renderQueue)
        {
            if (material == null)
            {
                return;
            }

            material.renderQueue = renderQueue;
            material.SetInt("_Cull", 0);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        }

        internal static void ApplyScopeAntialiasingOverride(Camera displayCamera)
        {
            ScopeAntialiasingOverrideMode mode = Plugin.ScopeAntialiasingOverride.Value;
            if (mode == ScopeAntialiasingOverrideMode.Off || displayCamera == null)
            {
                return;
            }

            PostProcessLayer postProcessLayer = displayCamera.GetComponent<PostProcessLayer>();
            if (postProcessLayer == null)
            {
                return;
            }

            postProcessLayer.enabled = true;
            postProcessLayer.DisableTAAResolvePass(false);

            if (mode == ScopeAntialiasingOverrideMode.FXAA)
            {
                postProcessLayer.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;
                displayCamera.depthTextureMode &= ~DepthTextureMode.MotionVectors;
                return;
            }

            if (mode == ScopeAntialiasingOverrideMode.None)
            {
                postProcessLayer.antialiasingMode = PostProcessLayer.Antialiasing.None;
                displayCamera.depthTextureMode &= ~DepthTextureMode.MotionVectors;
            }
        }

        private static void ApplyLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                ApplyLayerRecursively(gameObject.transform.GetChild(i).gameObject, layer);
            }
        }

        private void UpdateDistanceText(bool updateOverlay, bool updateWorld)
        {
            string text;
            if (!_lastRaycastHit)
            {
                text = Plugin.NoDistanceText.Value;
            }
            else if (Plugin.UseDecimalFormat.Value)
            {
                float clamped = Mathf.Clamp(GetDisplayDistance(), 0f, 999f);
                text = clamped.ToString("000.0");
            }
            else
            {
                int meters = Mathf.Clamp(Mathf.RoundToInt(GetDisplayDistance()), 0, 9999);
                text = meters.ToString("D4");
            }

            if (updateOverlay && _distanceText != null)
            {
                _distanceText.text = text;
            }

            if (updateWorld && _worldDistanceText != null)
            {
                _worldDistanceText.text = text;
            }
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

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform panelRect = ScopeDisplayStyle.CreateDisplayPanel(canvasObject.transform);

            _panelRect = panelRect;
            _distanceText = ScopeDisplayStyle.CreateReadoutText(panelRect);
            _panelBackgroundImages = _panelRect.GetComponentsInChildren<Image>(true);
        }
    }
}
