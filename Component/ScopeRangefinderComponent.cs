using EFT;
using EFT.Animations;
using EFT.CameraControl;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent : MonoBehaviour
    {
        private const int OverlaySortingOrder = 30000;
        private const float RayStartOffset = 0.1f;
        private const float PiPUnreliableHitDistance = 5f;
        private const float PiPCloseHitAgreement = 0.75f;
        private const float ScopeCanvasDefaultUiScale = 0.7f;
        private const float ScopeCanvasScaleSensitivity = 14f;
        private const float ScopeCanvasMinUiScale = 0.25f;
        private const float ScopeCanvasMaxUiScale = 4f;
        private const string WilcoxRaptarTemplateId = "61605d88ffa6e502ac5e7eeb";
        private const string OverlayLayoutKeyPrefix = "overlay:";

        private Canvas _canvas;
        private CanvasScaler _canvasScaler;
        private RectTransform _canvasRect;
        private RectTransform _panelRect;
        private Text _distanceText;
        private Camera _activeScopeCamera;
        private OpticSight _activeOpticSight;
        private ProceduralWeaponAnimation _activeWeaponAnimation;
        private string _currentLayoutKey;
        private string _lastLoggedLayoutKey;
        private float _timeSinceLastCast;
        private float _lastMeasuredDistance;
        private Vector3 _lastHitNormal = Vector3.up;
        private bool _lastRaycastHit;
        private bool _isScoped;
        private float _scopedElapsedTime;
        private bool _usingMainCameraScope;
        private bool _raptarActivationOverride;
        private bool _opticDisplayVisible;
        private static ScopeRangefinderComponent _activeInstance;
        private string _appliedLayoutKey;
        private float _appliedLayoutOffsetX = float.NaN;
        private float _appliedLayoutOffsetY = float.NaN;
        private float _appliedLayoutUiScale = float.NaN;
        private Camera _configuredScopeCamera;
        private static readonly int RaycastMask =
            LayersMaskController.HighPolyWithTerrainMask
            | LayersMaskController.TransparentLayerMask
            | LayersMaskController.HitColliderMask;

        private void Awake()
        {
            _activeInstance = this;
            CreateOverlay();
            ResetAndHide();
        }

        private void OnDestroy()
        {
            RestoreAutoZero(_activeWeaponAnimation);
            DestroyTrajectoryVisualization();
            DestroyReticleReadoutDisplay();
            DestroyFontPreview();
            RestoreLayoutEditorCursor();
            if (_activeInstance == this)
            {
                _activeInstance = null;
            }
        }

        private void Update()
        {
            HandleLayoutEditorHotkey();
            UpdateFontPreview();

            if (!Plugin.Enabled.Value)
            {
                RestoreAutoZero(_activeWeaponAnimation);
                ResetAndHide();
                return;
            }

            if (!TryGetScopedState(
                    out Camera scopeCamera,
                    out OpticSight currentOpticSight,
                    out ProceduralWeaponAnimation weaponAnimation,
                    out Player player))
            {
                ResetAndHide();
                return;
            }

            _activeScopeCamera = scopeCamera;
            _activeOpticSight = currentOpticSight;
            _activeWeaponAnimation = weaponAnimation;
            _currentLayoutKey = ResolveScopeLayoutKey(currentOpticSight, weaponAnimation);
            if (_usingMainCameraScope && !string.IsNullOrEmpty(_currentLayoutKey))
            {
                _currentLayoutKey = OverlayLayoutKeyPrefix + _currentLayoutKey;
            }

            if (!_isScoped)
            {
                _isScoped = true;
                _scopedElapsedTime = 0f;
                MeasureDistance(scopeCamera, weaponAnimation, player);
                MarkDistanceTextDirty();
                LogFontInventoryOnce();
            }
            else
            {
                _scopedElapsedTime += Time.deltaTime;
            }

            _timeSinceLastCast += Time.deltaTime;
            if (_timeSinceLastCast >= Plugin.UpdateInterval.Value)
            {
                _timeSinceLastCast = 0f;
                MeasureDistance(scopeCamera, weaponAnimation, player);
                MarkDistanceTextDirty();
            }

            UpdateAutoZero(player, weaponAnimation);

            if (_scopedElapsedTime < Plugin.DisplayShowDelay.Value || !ShouldShowReadout(weaponAnimation))
            {
                HideOpticReadout();
                return;
            }

            if (!ShouldUseInScopeDisplay())
            {
                HideReticleReadoutDisplay();
                _opticDisplayVisible = false;
                if (!ApplyDisplayLayout())
                {
                    _canvas.enabled = false;
                    _overlayDisplayVisible = false;
                    return;
                }

                _canvas.enabled = true;
                _overlayDisplayVisible = true;
                ApplyOverlayAppearance();
                UpdateDistanceText();
                return;
            }

            _opticDisplayVisible = true;
            _canvas.enabled = false;
            _overlayDisplayVisible = false;
        }

        private void LateUpdate()
        {
            if (!Plugin.Enabled.Value || !_opticDisplayVisible || !ShouldUseInScopeDisplay())
            {
                return;
            }

            Camera scopeCamera = _activeScopeCamera;
            OpticSight currentOpticSight = _activeOpticSight;
            ProceduralWeaponAnimation weaponAnimation = _activeWeaponAnimation;
            if (scopeCamera == null || currentOpticSight == null || weaponAnimation == null)
            {
                return;
            }

            SyncReticleCommandBufferDisplay(scopeCamera, currentOpticSight, weaponAnimation);
        }

        private void HideOpticReadout()
        {
            _canvas.enabled = false;
            _opticDisplayVisible = false;
            _overlayDisplayVisible = false;
            HideReticleReadoutDisplay();
        }

        private static bool ShouldUseInScopeDisplay()
        {
            return _activeInstance != null && !_activeInstance._usingMainCameraScope;
        }

        internal static void PopulateReticleReadoutCommandBuffer(CommandBuffer buffer, Camera scopeCamera)
        {
            _activeInstance?.DrawReticleReadoutToBuffer(buffer, scopeCamera);
        }

        private void ResetAppliedLayoutState()
        {
            _appliedLayoutKey = null;
            _appliedLayoutOffsetX = float.NaN;
            _appliedLayoutOffsetY = float.NaN;
            _appliedLayoutUiScale = float.NaN;
            _lastRenderedDistanceText = null;
            _overlayLastRenderedText = null;
            _distanceTextDirty = true;
        }

        private void ResetAndHide()
        {
            if (Plugin.AutoZeroMode == null || Plugin.AutoZeroMode.Value == AutoZeroMode.Continuous)
            {
                RestoreAutoZero(_activeWeaponAnimation);
            }

            SetTrajectoryPreviewVisible(false);
            _isScoped = false;
            _scopedElapsedTime = 0f;
            _usingMainCameraScope = false;
            _activeScopeCamera = null;
            _activeOpticSight = null;
            _activeWeaponAnimation = null;
            _currentLayoutKey = null;
            _opticDisplayVisible = false;
            _overlayDisplayVisible = false;
            ResetAppliedLayoutState();
            _configuredScopeCamera = null;
            HideReticleReadoutDisplay();

            if (_canvas != null)
            {
                _canvas.enabled = false;
            }

            _timeSinceLastCast = 0f;
            _lastRaycastHit = false;
            _lastMeasuredDistance = 0f;
        }
    }
}
