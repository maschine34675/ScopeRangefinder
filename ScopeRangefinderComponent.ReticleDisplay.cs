using EFT.Animations;
using EFT.CameraControl;
using UnityEngine;
using UnityEngine.Rendering;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
        private const float ReadoutZoomReferenceDepth = 0.25f;
        private const float ReadoutScaleReferenceFov = 35f;
        private const float ReadoutBaseCharacterSize = 0.013333f;
        private const float ReadoutBaseScale = 0.05f;

        private GameObject _reticleReadoutRoot;
        private TextMesh _reticleDistanceText;
        private MeshRenderer _reticleTextRenderer;
        private Material _reticleTextMaterial;
        private OpticReadoutCommandBuffer _reticleCommandBuffer;
        private bool _reticleDisplayConfigured;
        private Font _reticleAppliedFont;
        private Color _appliedTextColor;
        private float _appliedTextOffsetY = float.NaN;
        private bool _appliedBackgroundVisible;
        private float _appliedBackgroundWidth = float.NaN;
        private float _appliedBackgroundHeight = float.NaN;
        private Color _appliedBackgroundColor;

        private bool ShouldUseReticleCommandBufferDisplay()
        {
            return !Plugin.PiPDisablerLoaded && !_usingMainCameraScope;
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

            Font font = ScopeDisplayStyle.LoadRangefinderFont();
            if (font == null)
            {
                return;
            }

            if (_reticleAppliedFont != font)
            {
                _reticleAppliedFont = font;
                _reticleDistanceText.font = font;

                if (_reticleTextMaterial != null)
                {
                    Destroy(_reticleTextMaterial);
                }

                _reticleTextMaterial = new Material(font.material);
                ConfigureReticleDrawMaterial(_reticleTextMaterial, 5000);
                _reticleTextMaterial.color = Plugin.ScopeWorldTextColor.Value;
                return;
            }

            Texture atlas = font.material != null ? font.material.mainTexture : null;
            if (_reticleTextMaterial != null && atlas != null && _reticleTextMaterial.mainTexture != atlas)
            {
                _reticleTextMaterial.mainTexture = atlas;
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

            if (Plugin.ScopeWorldBackground.Value && _reticleBackgroundRenderer != null && _reticleBackgroundMaterial != null)
            {
                buffer.DrawRenderer(_reticleBackgroundRenderer, _reticleBackgroundMaterial, 0, 0);
            }

            buffer.DrawRenderer(_reticleTextRenderer, _reticleTextMaterial, 0, 0);
            buffer.SetViewProjectionMatrices(scopeCamera.worldToCameraMatrix, scopeCamera.projectionMatrix);
        }

        private void UpdateReticleReadoutViewPose(Camera scopeCamera)
        {
            float depth = ResolveReticleReadoutDepth(_activeOpticSight, scopeCamera);
            float zoomCompensation = CalculateReadoutZoomCompensation(scopeCamera, depth);
            float halfFovRadians = scopeCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfHeight = depth * Mathf.Tan(halfFovRadians);
            float halfWidth = halfHeight * scopeCamera.aspect;
            float offsetX = float.IsNaN(_appliedLayoutOffsetX) ? 0f : _appliedLayoutOffsetX;
            float offsetY = float.IsNaN(_appliedLayoutOffsetY) ? 0f : _appliedLayoutOffsetY;
            float uiScale = float.IsNaN(_appliedLayoutUiScale) ? ScopeCanvasDefaultUiScale : _appliedLayoutUiScale;
            float meshScale = ReadoutBaseScale * uiScale * zoomCompensation;

            _reticleReadoutRoot.transform.SetPositionAndRotation(
                new Vector3(offsetX * 2f * halfWidth, offsetY * 2f * halfHeight, -depth),
                Quaternion.identity);
            _reticleReadoutRoot.transform.localScale = Vector3.one * meshScale;
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
            LogLayoutKeyOnce(layoutKey, currentOpticSight);

            float offsetX = layout.OffsetX ?? 0f;
            float offsetY = layout.OffsetY ?? 0f;
            float uiScale = ResolveLayoutUiScale(layout.Scale ?? 0f);

            bool layoutChanged = layoutKey != _appliedLayoutKey
                || !Mathf.Approximately(offsetX, _appliedLayoutOffsetX)
                || !Mathf.Approximately(offsetY, _appliedLayoutOffsetY)
                || !Mathf.Approximately(uiScale, _appliedLayoutUiScale);
            bool cameraChanged = _configuredScopeCamera != scopeCamera;
            bool appearanceChanged = _appliedTextColor != Plugin.ScopeWorldTextColor.Value
                || _appliedTextOffsetY != Plugin.ScopeWorldTextOffsetY.Value
                || _appliedBackgroundVisible != Plugin.ScopeWorldBackground.Value
                || _appliedBackgroundWidth != Plugin.ScopeWorldBackgroundWidth.Value
                || _appliedBackgroundHeight != Plugin.ScopeWorldBackgroundHeight.Value
                || _appliedBackgroundColor != Plugin.ScopeWorldBackgroundColor.Value;
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
            _reticleReadoutRoot.SetActive(false);
            DontDestroyOnLoad(_reticleReadoutRoot);

            GameObject textObject = new GameObject("DistanceText");
            textObject.transform.SetParent(_reticleReadoutRoot.transform, false);
            _reticleDistanceText = textObject.AddComponent<TextMesh>();
            _reticleDistanceText.anchor = TextAnchor.MiddleCenter;
            _reticleDistanceText.alignment = TextAlignment.Center;
            _reticleDistanceText.fontSize = 96;
            _reticleDistanceText.characterSize = ReadoutBaseCharacterSize * ScopeCanvasDefaultUiScale;
            _reticleDistanceText.color = Plugin.ScopeWorldTextColor.Value;
            _reticleDistanceText.text = Plugin.NoDistanceText.Value;

            Font font = ScopeDisplayStyle.LoadRangefinderFont();
            if (font != null)
            {
                _reticleDistanceText.font = font;
            }

            _reticleAppliedFont = font;
            _reticleTextRenderer = textObject.GetComponent<MeshRenderer>();
            if (font != null)
            {
                _reticleTextMaterial = new Material(font.material);
            }
            else
            {
                Shader shader = Shader.Find("GUI/Text Shader") ?? Shader.Find("Unlit/Color");
                _reticleTextMaterial = new Material(shader);
            }

            ConfigureReticleDrawMaterial(_reticleTextMaterial, 5000);
            _reticleTextMaterial.color = Plugin.ScopeWorldTextColor.Value;

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
            _reticleBackgroundMaterial.color = Plugin.ScopeWorldBackgroundColor.Value;
            SetReticleBackgroundVisible(Plugin.ScopeWorldBackground.Value);

            DisableRegularMeshRenderer(_reticleTextRenderer);
            DisableRegularMeshRenderer(_reticleBackgroundRenderer);
        }

        private static float ResolveReticleReadoutDepth(OpticSight currentOpticSight, Camera scopeCamera)
        {
            return Mathf.Max(scopeCamera.nearClipPlane + 0.05f, LegacyScreenSpacePlaneDistance);
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
                _reticleDistanceText.color = Plugin.ScopeWorldTextColor.Value;
                _reticleDistanceText.characterSize = ReadoutBaseCharacterSize * ScopeCanvasDefaultUiScale;
                _reticleDistanceText.transform.localPosition = new Vector3(
                    0f,
                    Plugin.ScopeWorldTextOffsetY.Value * ScopeCanvasDefaultUiScale,
                    0f);
            }

            if (_reticleTextMaterial != null)
            {
                _reticleTextMaterial.color = Plugin.ScopeWorldTextColor.Value;
            }

            if (_reticleBackground != null)
            {
                float width = Mathf.Max(0.05f, Plugin.ScopeWorldBackgroundWidth.Value);
                float height = Mathf.Max(0.03f, Plugin.ScopeWorldBackgroundHeight.Value);
                _reticleBackground.transform.localScale = new Vector3(width, height, 1f);
            }

            if (_reticleBackgroundMaterial != null)
            {
                _reticleBackgroundMaterial.color = Plugin.ScopeWorldBackgroundColor.Value;
            }

            SetReticleBackgroundVisible(Plugin.ScopeWorldBackground.Value);

            _appliedTextColor = Plugin.ScopeWorldTextColor.Value;
            _appliedTextOffsetY = Plugin.ScopeWorldTextOffsetY.Value;
            _appliedBackgroundVisible = Plugin.ScopeWorldBackground.Value;
            _appliedBackgroundWidth = Plugin.ScopeWorldBackgroundWidth.Value;
            _appliedBackgroundHeight = Plugin.ScopeWorldBackgroundHeight.Value;
            _appliedBackgroundColor = Plugin.ScopeWorldBackgroundColor.Value;
        }

        private static void ConfigureReticleDrawMaterial(Material material, int renderQueue)
        {
            if (material == null)
            {
                return;
            }

            material.renderQueue = renderQueue;
            material.SetInt("_Cull", 0);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_ZTest", (int)CompareFunction.Always);
            if (material.HasProperty("unity_GUIZTestMode"))
            {
                material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            }
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

            _reticleDistanceText.text = text;
            _lastRenderedDistanceText = text;
            _distanceTextDirty = false;
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
