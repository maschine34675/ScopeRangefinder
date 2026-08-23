using EFT;
using EFT.Animations;
using EFT.CameraControl;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
        private const float ReadoutPlaneDistance = 2f;
        private const float ReadoutZoomReferenceDepth = 0.25f;
        private const float ReadoutScaleReferenceFov = 35f;
        private const float ReadoutTmpFontSize = 0.9f;
        private const float ReadoutBaseScale = 0.05f;

        private GameObject _reticleReadoutRoot;
        private TextMeshPro _reticleDistanceText;
        private MeshRenderer _reticleTextRenderer;
        private Material _reticleTextMaterial;
        private readonly Material[] _reticleGlowMaterials = new Material[GlowStyling.LayerCount];
        private readonly Material[] _reticleFringeMaterials = new Material[2];
        private OpticReadoutCommandBuffer _reticleCommandBuffer;
        private bool _reticleDisplayConfigured;
        private TMP_FontAsset _reticleAppliedFont;
        private Color _appliedTextColor;
        private float _appliedTextOffsetY = float.NaN;
        private float _appliedTextThickness = float.NaN;
        private float _appliedTextOutline = float.NaN;
        private float _appliedTextAberration = float.NaN;
        private float _appliedTextSpacing = float.NaN;
        private float _appliedTextGlow = float.NaN;
        private int _appliedReadoutRows = -1;
        private BallisticsLineMode _appliedBallisticsMode = (BallisticsLineMode)(-1);
        private bool _thicknessUnsupportedLogged;
        private string _centeringMeasurementKey;
        private float _centeringOffsetX;
        private Vector2 _widestTextSize;
        private bool _appliedBackgroundVisible;
        private float _appliedBackgroundWidth = float.NaN;
        private float _appliedBackgroundHeight = float.NaN;
        private Color _appliedBackgroundColor;

        private bool ShouldUseReticleCommandBufferDisplay()
        {
            return !_usingMainCameraScope;
        }

        internal void SyncReticleCommandBufferDisplay(
            Camera scopeCamera,
            OpticSight currentOpticSight,
            ProceduralWeaponAnimation weaponAnimation)
        {
            if (!_opticDisplayVisible
                || scopeCamera == null
                || currentOpticSight == null
                || weaponAnimation == null
                || !ShouldUseReticleCommandBufferDisplay())
            {
                return;
            }

            if (!ConfigureReticleReadoutIfNeeded(scopeCamera, currentOpticSight, weaponAnimation))
            {
                SetReticleReadoutVisible(false);
                _opticDisplayVisible = false;
                return;
            }

            EnsureReticleReadoutFont();
            SetReticleReadoutVisible(true);
            UpdateReticleDistanceTextIfDirty();
            EnsureReticleCommandBuffer(scopeCamera);
        }
        private void EnsureReticleReadoutFont()
        {
            if (_reticleDistanceText == null)
            {
                return;
            }

            TMP_FontAsset font = ScopeDisplayStyle.LoadRangefinderTmpFont();
            if (font == null)
            {
                return;
            }

            if (_reticleAppliedFont != font)
            {
                _reticleAppliedFont = font;
                _reticleDistanceText.font = font;
                _thicknessUnsupportedLogged = false;

                if (_reticleTextMaterial != null)
                {
                    Destroy(_reticleTextMaterial);
                }

                for (int i = 0; i < _reticleGlowMaterials.Length; i++)
                {
                    if (_reticleGlowMaterials[i] != null)
                    {
                        Destroy(_reticleGlowMaterials[i]);
                        _reticleGlowMaterials[i] = null;
                    }
                }

                for (int i = 0; i < _reticleFringeMaterials.Length; i++)
                {
                    if (_reticleFringeMaterials[i] != null)
                    {
                        Destroy(_reticleFringeMaterials[i]);
                        _reticleFringeMaterials[i] = null;
                    }
                }

                _reticleTextMaterial = new Material(font.material);
                ConfigureReticleDrawMaterial(_reticleTextMaterial, 5000);
                ApplyTextFaceColor();
                ApplyTextThickness();
                ApplyTextOutline();
                ApplyTextAberration();
                _reticleDistanceText.fontSharedMaterial = _reticleTextMaterial;
                ApplyTextGlow();
                _lastRenderedDistanceText = null;
                _distanceTextDirty = true;
                return;
            }

            Texture atlas = font.material != null ? font.material.mainTexture : null;
            if (_reticleTextMaterial != null && atlas != null && _reticleTextMaterial.mainTexture != atlas)
            {
                _reticleTextMaterial.mainTexture = atlas;
            }

            foreach (Material glowMaterial in _reticleGlowMaterials)
            {
                if (glowMaterial != null && atlas != null && glowMaterial.mainTexture != atlas)
                {
                    glowMaterial.mainTexture = atlas;
                }
            }

            foreach (Material fringeMaterial in _reticleFringeMaterials)
            {
                if (fringeMaterial != null && atlas != null && fringeMaterial.mainTexture != atlas)
                {
                    fringeMaterial.mainTexture = atlas;
                }
            }
        }

        internal void DrawReticleReadoutToBuffer(CommandBuffer buffer, Camera scopeCamera)
        {
            if (!_opticDisplayVisible
                || buffer == null
                || scopeCamera == null
                || !ShouldUseReticleCommandBufferDisplay()
                || _reticleReadoutRoot == null
                || _reticleTextRenderer == null
                || _reticleTextMaterial == null
                || !_reticleReadoutRoot.activeSelf)
            {
                return;
            }

            UpdateReticleReadoutViewPose(scopeCamera);
            buffer.SetViewProjectionMatrices(
                Matrix4x4.identity,
                scopeCamera.nonJitteredProjectionMatrix);

            if (ActiveStyle.BackgroundVisible && _reticleBackgroundRenderer != null && _reticleBackgroundMaterial != null)
            {
                buffer.DrawRenderer(_reticleBackgroundRenderer, _reticleBackgroundMaterial, 0, 0);
            }

            if (ActiveStyle.TextGlow > 0.001f)
            {
                for (int i = _reticleGlowMaterials.Length - 1; i >= 0; i--)
                {
                    if (_reticleGlowMaterials[i] != null)
                    {
                        buffer.DrawRenderer(_reticleTextRenderer, _reticleGlowMaterials[i], 0, 0);
                    }
                }
            }

            DrawAberrationFringes(buffer);
            buffer.DrawRenderer(_reticleTextRenderer, _reticleTextMaterial, 0, 0);
            buffer.SetViewProjectionMatrices(scopeCamera.worldToCameraMatrix, scopeCamera.projectionMatrix);
        }

        private void UpdateReticleReadoutViewPose(Camera scopeCamera)
        {
            float depth = ResolveReticleReadoutDepth(scopeCamera);
            float zoomCompensation = CalculateReadoutZoomCompensation(scopeCamera, depth);
            float halfFovRadians = scopeCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfHeight = depth * Mathf.Tan(halfFovRadians);
            float halfWidth = halfHeight * scopeCamera.aspect;
            float offsetX = float.IsNaN(_appliedLayoutOffsetX) ? 0f : _appliedLayoutOffsetX;
            float offsetY = float.IsNaN(_appliedLayoutOffsetY) ? 0f : _appliedLayoutOffsetY;
            float uiScale = float.IsNaN(_appliedLayoutUiScale) ? ScopeCanvasDefaultUiScale : _appliedLayoutUiScale;
            float meshScale = ReadoutBaseScale * uiScale * zoomCompensation;
            float shiftX = 0f;
            float shiftY = 0f;
            if (_appliedLayoutAnchor != ReadoutAnchor.Center)
            {
                Vector2 pivot = _appliedLayoutAnchor.ToPivot();
                GetReadoutBlockEnvelope(out Vector2 blockSize, out Vector2 blockCenter);
                shiftX = (-blockCenter.x + (0.5f - pivot.x) * blockSize.x) * meshScale;
                shiftY = (-blockCenter.y + (0.5f - pivot.y) * blockSize.y) * meshScale;
            }
            _reticleReadoutRoot.transform.SetPositionAndRotation(
                new Vector3(offsetX * 2f * halfWidth + shiftX, offsetY * 2f * halfHeight + shiftY, -depth),
                Quaternion.identity);
            _reticleReadoutRoot.transform.localScale = Vector3.one * meshScale;
        }
        private bool TryGetAnchorSwitchOffsetDelta(
            ReadoutAnchor from,
            ReadoutAnchor to,
            out Vector2 delta)
        {
            delta = Vector2.zero;
            if (_usingMainCameraScope)
            {
                if (_panelRect == null)
                {
                    return false;
                }

                Vector2 pivotDelta = to.ToPivot() - from.ToPivot();
                Vector2 size = Vector2.Scale(_panelRect.sizeDelta, (Vector2)_panelRect.localScale);
                delta = new Vector2(pivotDelta.x * size.x / 1920f, pivotDelta.y * size.y / 1080f);
                return true;
            }

            float uiScale = float.IsNaN(_appliedLayoutUiScale) ? ScopeCanvasDefaultUiScale : _appliedLayoutUiScale;
            return TryGetInScopeAnchorSwitchDelta(from, to, uiScale, out delta);
        }
        private bool TryGetAnchorSwitchOffsetDeltaForScale(
            ReadoutAnchor from,
            ReadoutAnchor to,
            float scaleAdjustment,
            out Vector2 delta)
        {
            delta = Vector2.zero;
            if (_usingMainCameraScope)
            {
                return false;
            }

            return TryGetInScopeAnchorSwitchDelta(from, to, ResolveLayoutUiScale(scaleAdjustment), out delta);
        }

        private bool TryGetInScopeAnchorSwitchDelta(
            ReadoutAnchor from,
            ReadoutAnchor to,
            float uiScale,
            out Vector2 delta)
        {
            delta = Vector2.zero;
            Camera scopeCamera = _activeScopeCamera;
            if (scopeCamera == null || _reticleReadoutRoot == null)
            {
                return false;
            }
            float depth = ResolveReticleReadoutDepth(scopeCamera);
            float halfHeight = depth * Mathf.Tan(scopeCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * scopeCamera.aspect;
            float meshScale = ReadoutBaseScale * uiScale * CalculateReadoutZoomCompensation(scopeCamera, depth);
            Vector2 shiftFrom = AnchorShiftLocal(from);
            Vector2 shiftTo = AnchorShiftLocal(to);
            delta = new Vector2(
                (shiftFrom.x - shiftTo.x) * meshScale / (2f * halfWidth),
                (shiftFrom.y - shiftTo.y) * meshScale / (2f * halfHeight));
            return true;
        }
        private Vector2 AnchorShiftLocal(ReadoutAnchor anchor)
        {
            if (anchor == ReadoutAnchor.Center)
            {
                return Vector2.zero;
            }

            Vector2 pivot = anchor.ToPivot();
            GetReadoutBlockEnvelope(out Vector2 size, out Vector2 center);
            return new Vector2(
                -center.x + (0.5f - pivot.x) * size.x,
                -center.y + (0.5f - pivot.y) * size.y);
        }
        private void GetReadoutBlockEnvelope(out Vector2 size, out Vector2 center)
        {
            if (ActiveStyle.BackgroundVisible && _reticleBackground != null)
            {
                Vector3 plate = _reticleBackground.transform.localScale;
                size = new Vector2(plate.x, plate.y);
                center = Vector2.zero;
                return;
            }

            if (_reticleDistanceText != null)
            {
                if (ConfiguredReadoutRows() > 1)
                {
                    GetStableCenteringOffsetX();
                    size = _widestTextSize;
                }
                else
                {
                    Bounds bounds = _reticleDistanceText.textBounds;
                    size = new Vector2(bounds.size.x, bounds.size.y);
                }

                center = new Vector2(0f, ActiveStyle.TextOffsetY * ScopeCanvasDefaultUiScale);
                return;
            }

            size = Vector2.zero;
            center = Vector2.zero;
        }

        private GameObject _reticleBackground;
        private MeshRenderer _reticleBackgroundRenderer;
        private Material _reticleBackgroundMaterial;

        private bool ConfigureReticleReadoutIfNeeded(
            Camera scopeCamera,
            OpticSight currentOpticSight,
            ProceduralWeaponAnimation weaponAnimation)
        {
            EnsureReticleReadoutObjects();

            string layoutKey = ResolveScopeLayoutKey(currentOpticSight, weaponAnimation);
            _currentLayoutKey = layoutKey;
            ScopeLayoutEntry layout = GetLayoutForDisplay(layoutKey);

            float offsetX = layout.OffsetX ?? 0f;
            float offsetY = layout.OffsetY ?? 0f;
            float uiScale = ResolveLayoutUiScale(layout.Scale ?? 0f);
            ReadoutAnchor anchor = layout.Anchor ?? ReadoutAnchor.Center;

            bool layoutChanged = layoutKey != _appliedLayoutKey
                || !Mathf.Approximately(offsetX, _appliedLayoutOffsetX)
                || !Mathf.Approximately(offsetY, _appliedLayoutOffsetY)
                || !Mathf.Approximately(uiScale, _appliedLayoutUiScale)
                || anchor != _appliedLayoutAnchor;
            bool cameraChanged = _configuredScopeCamera != scopeCamera;
            bool appearanceChanged = _appliedTextColor != ActiveStyle.TextColor
                || _appliedTextOffsetY != ActiveStyle.TextOffsetY
                || _appliedTextThickness != ActiveStyle.TextThickness
                || _appliedTextOutline != ActiveStyle.TextOutline
                || _appliedTextAberration != ActiveStyle.TextAberration
                || _appliedTextSpacing != ActiveStyle.TextSpacing
                || _appliedTextGlow != ActiveStyle.TextGlow
                || _appliedReadoutRows != ConfiguredReadoutRows()
                || _appliedBallisticsMode != ActiveStyle.BallisticsLine
                || _appliedBackgroundVisible != ActiveStyle.BackgroundVisible
                || _appliedBackgroundWidth != ActiveStyle.BackgroundWidth
                || _appliedBackgroundHeight != ActiveStyle.BackgroundHeight
                || _appliedBackgroundColor != ActiveStyle.BackgroundColor;
            bool needsConfigure = !_reticleDisplayConfigured || cameraChanged || layoutChanged || appearanceChanged;

            if (!needsConfigure)
            {
                return true;
            }

            ApplyReticleReadoutAppearance(uiScale);
            DisableRegularMeshRenderer(_reticleTextRenderer);
            DisableRegularMeshRenderer(_reticleBackgroundRenderer);

            _configuredScopeCamera = scopeCamera;
            _reticleDisplayConfigured = true;
            _appliedLayoutKey = layoutKey;
            _appliedLayoutOffsetX = offsetX;
            _appliedLayoutOffsetY = offsetY;
            _appliedLayoutUiScale = uiScale;
            _appliedLayoutAnchor = anchor;
            _distanceTextDirty = true;
            return true;
        }

        private void EnsureReticleReadoutObjects()
        {
            if (_reticleReadoutRoot != null)
            {
                return;
            }
            _reticleReadoutRoot = new GameObject("ScopeRangefinderReticleReadout");
            DontDestroyOnLoad(_reticleReadoutRoot);

            GameObject textObject = new GameObject("DistanceText");
            textObject.transform.SetParent(_reticleReadoutRoot.transform, false);
            _reticleDistanceText = textObject.AddComponent<TextMeshPro>();
            _reticleDistanceText.alignment = TextAlignmentOptions.Center;
            _reticleDistanceText.enableWordWrapping = false;
            _reticleDistanceText.overflowMode = TextOverflowModes.Overflow;
            _reticleDistanceText.fontSize = ReadoutTmpFontSize;
            _reticleDistanceText.color = ActiveStyle.TextColor;
            _reticleDistanceText.rectTransform.sizeDelta = new Vector2(4f, 1f);
            _reticleDistanceText.text = ActiveStyle.NoDistanceText;

            _reticleTextRenderer = textObject.GetComponent<MeshRenderer>();

            TMP_FontAsset font = ScopeDisplayStyle.LoadRangefinderTmpFont();
            _reticleAppliedFont = font;
            if (font != null)
            {
                _reticleDistanceText.font = font;
                _reticleTextMaterial = new Material(font.material);
            }
            else
            {
                _reticleTextMaterial = new Material(_reticleDistanceText.fontSharedMaterial);
            }

            ConfigureReticleDrawMaterial(_reticleTextMaterial, 5000);
            _reticleDistanceText.fontSharedMaterial = _reticleTextMaterial;

            _reticleBackground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _reticleBackground.name = "Background";
            _reticleBackground.transform.SetParent(_reticleReadoutRoot.transform, false);
            _reticleBackground.transform.localPosition = new Vector3(0f, 0f, -0.001f);
            _reticleBackground.transform.localScale = new Vector3(0.8f, 0.28f, 1f);
            Collider backgroundCollider = _reticleBackground.GetComponent<Collider>();
            if (backgroundCollider != null)
            {
                Destroy(backgroundCollider);
            }

            _reticleBackgroundRenderer = _reticleBackground.GetComponent<MeshRenderer>();
            Shader backgroundShader = Shader.Find("GUI/Text Shader") ?? Shader.Find("Unlit/Color");
            _reticleBackgroundMaterial = new Material(backgroundShader);
            ConfigureReticleDrawMaterial(_reticleBackgroundMaterial, 4999);
            _reticleBackgroundMaterial.color = ActiveStyle.BackgroundColor;
            SetReticleBackgroundVisible(ActiveStyle.BackgroundVisible);
            DisableRegularMeshRenderer(_reticleTextRenderer);
            DisableRegularMeshRenderer(_reticleBackgroundRenderer);

            _reticleReadoutRoot.SetActive(false);
        }

        private static float ResolveReticleReadoutDepth(Camera scopeCamera)
        {
            return Mathf.Max(scopeCamera.nearClipPlane + 0.05f, ReadoutPlaneDistance);
        }

        private static float CalculateReadoutZoomCompensation(Camera displayCamera, float depth)
        {
            if (displayCamera == null)
            {
                return 1f;
            }

            float currentFov = Mathf.Clamp(displayCamera.fieldOfView, 1f, 170f);
            float referenceTan = Mathf.Tan(ReadoutScaleReferenceFov * 0.5f * Mathf.Deg2Rad);
            float currentTan = Mathf.Tan(currentFov * 0.5f * Mathf.Deg2Rad);
            float depthFactor = Mathf.Max(0.01f, depth) / ReadoutZoomReferenceDepth;
            return (currentTan / referenceTan) * depthFactor;
        }

        private void ApplyReticleReadoutAppearance(float uiScale)
        {
            if (_reticleDistanceText != null)
            {
                ApplyTextFaceColor();
                _reticleDistanceText.fontSize = ReadoutTmpFontSize;
                TextAlignmentOptions alignment = ConfiguredReadoutRows() > 1
                    ? TextAlignmentOptions.Left
                    : TextAlignmentOptions.Center;
                if (_reticleDistanceText.alignment != alignment)
                {
                    _reticleDistanceText.alignment = alignment;
                    _lastRenderedDistanceText = null;
                    _distanceTextDirty = true;
                }

                float spacing = ActiveStyle.TextSpacing;
                if (!Mathf.Approximately(_reticleDistanceText.characterSpacing, spacing))
                {
                    _reticleDistanceText.characterSpacing = spacing;
                    _lastRenderedDistanceText = null;
                    _distanceTextDirty = true;
                }

                RecenterReadoutText();
            }

            ApplyTextThickness();
            ApplyTextOutline();
            ApplyTextAberration();
            ApplyTextGlow();

            if (_reticleBackground != null)
            {
                int rows = ConfiguredReadoutRows();
                float width = Mathf.Max(0.05f, ActiveStyle.BackgroundWidth)
                    * ReadoutPlateWidthFactor(rows);
                float height = Mathf.Max(0.03f, ActiveStyle.BackgroundHeight)
                    * ReadoutPlateHeightFactor(rows);
                _reticleBackground.transform.localScale = new Vector3(width, height, 1f);
            }

            if (_reticleBackgroundMaterial != null)
            {
                _reticleBackgroundMaterial.color = ActiveStyle.BackgroundColor;
            }

            SetReticleBackgroundVisible(ActiveStyle.BackgroundVisible);

            _appliedTextColor = ActiveStyle.TextColor;
            _appliedTextOffsetY = ActiveStyle.TextOffsetY;
            _appliedTextThickness = ActiveStyle.TextThickness;
            _appliedTextOutline = ActiveStyle.TextOutline;
            _appliedTextAberration = ActiveStyle.TextAberration;
            _appliedTextSpacing = ActiveStyle.TextSpacing;
            _appliedTextGlow = ActiveStyle.TextGlow;
            _appliedReadoutRows = ConfiguredReadoutRows();
            _appliedBallisticsMode = ActiveStyle.BallisticsLine;
            _appliedBackgroundVisible = ActiveStyle.BackgroundVisible;
            _appliedBackgroundWidth = ActiveStyle.BackgroundWidth;
            _appliedBackgroundHeight = ActiveStyle.BackgroundHeight;
            _appliedBackgroundColor = ActiveStyle.BackgroundColor;
        }
        private void ApplyTextThickness()
        {
            if (_reticleTextMaterial == null)
            {
                return;
            }

            if (_reticleTextMaterial.HasProperty("_FaceDilate"))
            {
                float thickness = ActiveStyle.TextThickness;
                _reticleTextMaterial.SetFloat("_FaceDilate", thickness);
                if (!Mathf.Approximately(_appliedTextThickness, thickness) && _reticleDistanceText != null)
                {
                    _reticleDistanceText.UpdateMeshPadding();
                    _lastRenderedDistanceText = null;
                    _distanceTextDirty = true;
                }
            }
            else if (!_thicknessUnsupportedLogged && Mathf.Abs(ActiveStyle.TextThickness) > 0.001f)
            {
                _thicknessUnsupportedLogged = true;
                Plugin.LogSource?.LogInfo(
                    "Text Thickness has no effect: the current font is not SDF-rendered " +
                    "(bitmap font asset). Rebuild the font asset with render mode SDFAA, " +
                    "or drop the raw .ttf/.otf into the fonts folder instead.");
            }
        }
        private void ApplyTextFaceColor()
        {
            if (_reticleDistanceText == null || _reticleTextMaterial == null)
            {
                return;
            }

            Color textColor = ActiveStyle.TextColor;
            if (_reticleTextMaterial.HasProperty("_FaceColor"))
            {
                _reticleDistanceText.color = new Color(1f, 1f, 1f, textColor.a);
                _reticleTextMaterial.SetColor(
                    "_FaceColor", new Color(textColor.r, textColor.g, textColor.b, 1f));
            }
            else
            {
                _reticleDistanceText.color = textColor;
            }
        }
        private const float AberrationMaxShift = 0.018f;
        private void ApplyTextAberration()
        {
            float strength = Mathf.Clamp01(ActiveStyle.TextAberration);
            bool active = strength > 0.001f
                && _reticleTextMaterial != null
                && _reticleTextMaterial.HasProperty("_FaceDilate");
            if (!active)
            {
                return;
            }

            GlowStyling.GetAberrationFringeColors(
                ActiveStyle.TextColor, out Color outwardColor, out Color inwardColor);
            for (int i = 0; i < _reticleFringeMaterials.Length; i++)
            {
                if (_reticleFringeMaterials[i] == null)
                {
                    _reticleFringeMaterials[i] = new Material(_reticleTextMaterial);
                }

                Material fringe = _reticleFringeMaterials[i];
                fringe.SetFloat("_FaceDilate", ActiveStyle.TextThickness);
                fringe.SetFloat("_OutlineWidth", 0f);
                fringe.DisableKeyword("OUTLINE_ON");
                Color fringeColor = i == 0 ? outwardColor : inwardColor;
                fringeColor.a = GlowStyling.GetAberrationFringeAlpha(strength);
                fringe.SetColor("_FaceColor", fringeColor);
            }
        }
        private void DrawAberrationFringes(CommandBuffer buffer)
        {
            float strength = Mathf.Clamp01(ActiveStyle.TextAberration);
            if (strength <= 0.001f || _reticleDistanceText == null)
            {
                return;
            }

            Mesh textMesh = _reticleDistanceText.mesh;
            if (textMesh == null)
            {
                return;
            }

            Vector3 rootPosition = _reticleReadoutRoot.transform.position;
            Vector2 radial = new Vector2(rootPosition.x, rootPosition.y);
            Vector2 direction = radial.sqrMagnitude > 1e-8f ? radial.normalized : Vector2.right;
            float shift = strength * AberrationMaxShift * _reticleReadoutRoot.transform.localScale.x;
            Vector3 offset = new Vector3(direction.x, direction.y, 0f) * shift;

            Matrix4x4 textMatrix = _reticleTextRenderer.localToWorldMatrix;
            if (_reticleFringeMaterials[0] != null)
            {
                buffer.DrawMesh(textMesh, Matrix4x4.Translate(offset) * textMatrix, _reticleFringeMaterials[0], 0, 0);
            }

            if (_reticleFringeMaterials[1] != null)
            {
                buffer.DrawMesh(textMesh, Matrix4x4.Translate(-offset) * textMatrix, _reticleFringeMaterials[1], 0, 0);
            }
        }
        private void ApplyTextOutline()
        {
            if (_reticleTextMaterial == null || !_reticleTextMaterial.HasProperty("_OutlineWidth"))
            {
                return;
            }

            float width = Mathf.Clamp01(ActiveStyle.TextOutline);
            _reticleTextMaterial.SetFloat("_OutlineWidth", width);
            _reticleTextMaterial.SetColor("_OutlineColor", Color.black);
            if (width > 0f)
            {
                _reticleTextMaterial.EnableKeyword("OUTLINE_ON");
            }
            else
            {
                _reticleTextMaterial.DisableKeyword("OUTLINE_ON");
            }

            if (!Mathf.Approximately(_appliedTextOutline, width) && _reticleDistanceText != null)
            {
                _reticleDistanceText.UpdateMeshPadding();
                _lastRenderedDistanceText = null;
                _distanceTextDirty = true;
            }
        }
        private void ApplyTextGlow()
        {
            float strength = Mathf.Clamp01(ActiveStyle.TextGlow);
            bool glowActive = strength > 0.001f
                && _reticleTextMaterial != null
                && _reticleTextMaterial.HasProperty("_FaceDilate");

            if (glowActive)
            {
                for (int i = 0; i < _reticleGlowMaterials.Length; i++)
                {
                    if (_reticleGlowMaterials[i] == null)
                    {
                        _reticleGlowMaterials[i] = new Material(_reticleTextMaterial);
                    }

                    GlowStyling.ConfigureLayer(
                        _reticleGlowMaterials[i],
                        i,
                        strength,
                        ActiveStyle.TextThickness,
                        ActiveStyle.TextColor);
                }
            }
            Material desiredSharedMaterial = glowActive
                ? _reticleGlowMaterials[GlowStyling.LayerCount - 1]
                : _reticleTextMaterial;
            if (_reticleDistanceText != null
                && desiredSharedMaterial != null
                && _reticleDistanceText.fontSharedMaterial != desiredSharedMaterial)
            {
                _reticleDistanceText.fontSharedMaterial = desiredSharedMaterial;
            }

            if (glowActive)
            {
                _reticleDistanceText?.UpdateMeshPadding();
                _lastRenderedDistanceText = null;
                _distanceTextDirty = true;
            }
        }

        private static void ConfigureReticleDrawMaterial(Material material, int renderQueue)
        {
            if (material == null)
            {
                return;
            }

            material.renderQueue = renderQueue;
            material.SetInt("_Cull", 0);
            material.SetInt("_CullMode", 0);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_ZTest", (int)CompareFunction.Always);
            material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        }

        private static void DisableRegularMeshRenderer(MeshRenderer renderer)
        {
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        private void EnsureReticleCommandBuffer(Camera scopeCamera)
        {
            if (scopeCamera == null)
            {
                return;
            }

            if (_reticleCommandBuffer != null && _configuredScopeCamera != null && _configuredScopeCamera != scopeCamera)
            {
                DestroyReticleCommandBuffer();
            }

            if (_reticleCommandBuffer == null)
            {
                _reticleCommandBuffer = scopeCamera.GetComponent<OpticReadoutCommandBuffer>();
                if (_reticleCommandBuffer == null)
                {
                    _reticleCommandBuffer = scopeCamera.gameObject.AddComponent<OpticReadoutCommandBuffer>();
                }
            }
        }

        private void UpdateReticleDistanceTextIfDirty()
        {
            if (!_distanceTextDirty || _reticleDistanceText == null)
            {
                return;
            }

            string text = BuildDistanceText();
            if (text == _lastRenderedDistanceText)
            {
                _distanceTextDirty = false;
                return;
            }
            _reticleDistanceText.SetMonospaceText(text, false);
            _reticleDistanceText.ForceMeshUpdate();
            _lastRenderedDistanceText = text;
            RecenterReadoutText();
            _distanceTextDirty = false;
        }
        private void RecenterReadoutText()
        {
            if (_reticleDistanceText == null)
            {
                return;
            }

            float offsetX = ConfiguredReadoutRows() > 1 ? GetStableCenteringOffsetX() : 0f;

            _reticleDistanceText.transform.localPosition = new Vector3(
                offsetX,
                ActiveStyle.TextOffsetY * ScopeCanvasDefaultUiScale,
                0f);
        }
        private float GetStableCenteringOffsetX()
        {
            string widestText = BuildWidestReadoutText();
            string measurementKey = string.Concat(
                widestText,
                "|",
                _reticleAppliedFont != null ? _reticleAppliedFont.GetInstanceID().ToString() : "0",
                "|",
                _reticleDistanceText.characterSpacing.ToString("F3"),
                "|",
                _reticleDistanceText.fontSize.ToString("F3"));
            if (measurementKey == _centeringMeasurementKey)
            {
                return _centeringOffsetX;
            }

            string liveText = _lastRenderedDistanceText;
            _reticleDistanceText.SetMonospaceText(widestText, false);
            _reticleDistanceText.ForceMeshUpdate();
            Bounds widestBounds = _reticleDistanceText.textBounds;
            _centeringOffsetX = -widestBounds.center.x;
            _widestTextSize = new Vector2(widestBounds.size.x, widestBounds.size.y);
            _centeringMeasurementKey = measurementKey;
            if (!string.IsNullOrEmpty(liveText))
            {
                _reticleDistanceText.SetMonospaceText(liveText, false);
                _reticleDistanceText.ForceMeshUpdate();
            }
            else
            {
                _lastRenderedDistanceText = null;
                _distanceTextDirty = true;
            }

            return _centeringOffsetX;
        }

        private void SetReticleReadoutVisible(bool visible)
        {
            if (_reticleReadoutRoot != null && _reticleReadoutRoot.activeSelf != visible)
            {
                _reticleReadoutRoot.SetActive(visible);
            }
        }

        private void SetReticleBackgroundVisible(bool visible)
        {
            if (_reticleBackground != null && _reticleBackground.activeSelf != visible)
            {
                _reticleBackground.SetActive(visible);
            }
        }

        private void HideReticleReadoutDisplay()
        {
            SetReticleReadoutVisible(false);
            _reticleDisplayConfigured = false;
        }

        private void DestroyReticleCommandBuffer()
        {
            if (_reticleCommandBuffer != null)
            {
                Destroy(_reticleCommandBuffer);
                _reticleCommandBuffer = null;
            }
        }

        private void DestroyReticleReadoutDisplay()
        {
            DestroyReticleCommandBuffer();

            if (_reticleReadoutRoot != null)
            {
                Destroy(_reticleReadoutRoot);
                _reticleReadoutRoot = null;
            }

            if (_reticleTextMaterial != null)
            {
                Destroy(_reticleTextMaterial);
                _reticleTextMaterial = null;
            }

            for (int i = 0; i < _reticleGlowMaterials.Length; i++)
            {
                if (_reticleGlowMaterials[i] != null)
                {
                    Destroy(_reticleGlowMaterials[i]);
                    _reticleGlowMaterials[i] = null;
                }
            }

            for (int i = 0; i < _reticleFringeMaterials.Length; i++)
            {
                if (_reticleFringeMaterials[i] != null)
                {
                    Destroy(_reticleFringeMaterials[i]);
                    _reticleFringeMaterials[i] = null;
                }
            }

            if (_reticleBackgroundMaterial != null)
            {
                Destroy(_reticleBackgroundMaterial);
                _reticleBackgroundMaterial = null;
            }

            _reticleDistanceText = null;
            _reticleTextRenderer = null;
            _reticleBackground = null;
            _reticleBackgroundRenderer = null;
            _reticleAppliedFont = null;
            _reticleDisplayConfigured = false;
        }
    }
}
