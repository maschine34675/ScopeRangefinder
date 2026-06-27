using EFT;
using EFT.Animations;
using EFT.CameraControl;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent : MonoBehaviour
    {
        private const int OverlaySortingOrder = 30000;
        private const float RayStartOffset = 0.1f;
        private const float PiPUnreliableHitDistance = 5f;
        private const float PiPCloseHitAgreement = 0.75f;
        private const float ScopeScaleReferenceFov = 35f;
        private const float ScopeDisplayDepth = 0.25f;
        private const float ProjectedOverlayReferenceScale = 0.05f;
        private const float ProjectedOverlayAnchorDistance = 100f;
        private const float ProjectedOverlayOffsetMultiplier = 3.6f;
        private const float ProjectedOverlayScaleMultiplier = 0.7f;
        private const float ProjectedOverlayReferenceBackgroundWidth = 0.45f;
        private const float ProjectedOverlayReferenceBackgroundHeight = 0.16f;
        private const float ProjectedOverlayReferencePanelWidth = 142f;
        private const float ProjectedOverlayReferencePanelHeight = 46f;
        private const string WilcoxRaptarTemplateId = "61605d88ffa6e502ac5e7eeb";

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private RectTransform _panelRect;
        private Text _distanceText;
        private Image[] _panelBackgroundImages;
        private GameObject _worldRoot;
        private GameObject _worldBackground;
        private Material _worldBackgroundMaterial;
        private Material _worldTextMaterial;
        private TextMesh _worldDistanceText;
        private Transform _worldParent;
        private Camera _activeScopeCamera;
        private OpticSight _activeOpticSight;
        private ProceduralWeaponAnimation _activeWeaponAnimation;
        private bool _worldDisplayNeedsLateLayout;
        private string _currentLayoutKey;
        private string _lastLoggedLayoutKey;
        private float _timeSinceLastCast;
        private float _lastMeasuredDistance;
        private bool _lastRaycastHit;
        private bool _isScoped;
        private float _scopedElapsedTime;
        private bool _usingMainCameraScope;
        private bool _raptarActivationOverride;
        private static ScopeRangefinderComponent _activeInstance;
        private static readonly int RaycastMask =
            LayerMaskClass.HighPolyWithTerrainMask
            | LayerMaskClass.TransparentLayerMask
            | LayerMaskClass.HitColliderMask;

        private void Awake()
        {
            _activeInstance = this;
            Camera.onPreCull += HandleCameraPreCull;
            CreateOverlay();
            ResetAndHide();
        }

        private void OnDestroy()
        {
            Camera.onPreCull -= HandleCameraPreCull;
            if (_activeInstance == this)
            {
                _activeInstance = null;
            }
        }

        private void Update()
        {
            HandleLayoutEditorHotkey();

            if (!Plugin.Enabled.Value)
            {
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

            if (!_isScoped)
            {
                _isScoped = true;
                _scopedElapsedTime = 0f;
                MeasureDistance(scopeCamera, weaponAnimation, player);
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
            }

            if (_scopedElapsedTime < Plugin.DisplayShowDelay.Value || !ShouldShowReadout(weaponAnimation))
            {
                _canvas.enabled = false;
                SetWorldDisplayVisible(false);
                return;
            }

            ScopeRenderMode renderMode = GetEffectiveRenderMode();
            if (!_usingMainCameraScope && renderMode == ScopeRenderMode.ExperimentalInScopeCamera)
            {
                _canvas.enabled = false;
                if (!ApplyWorldDisplayLayout(scopeCamera, currentOpticSight, weaponAnimation))
                {
                    SetWorldDisplayVisible(false);
                    return;
                }

                UpdateDistanceText(false, true);
                SetWorldDisplayVisible(true);
                _worldDisplayNeedsLateLayout = true;
                return;
            }

            if (renderMode == ScopeRenderMode.ProjectedOverlay)
            {
                SetWorldDisplayVisible(false);
                _worldDisplayNeedsLateLayout = false;
                if (!ApplyProjectedOverlayLayout(scopeCamera, currentOpticSight, weaponAnimation))
                {
                    _canvas.enabled = false;
                    return;
                }

                _canvas.enabled = true;
                UpdateDistanceText(true, false);
                return;
            }

            SetWorldDisplayVisible(false);
            _worldDisplayNeedsLateLayout = false;
            if (!ApplyDisplayLayout())
            {
                _canvas.enabled = false;
                return;
            }

            _canvas.enabled = true;
            UpdateDistanceText(true, false);
        }

        internal static ScopeRenderMode GetEffectiveRenderMode()
        {
            ScopeRenderMode renderMode = Plugin.ScopeRenderMode.Value;
            if (Plugin.PiPDisablerLoaded && renderMode == ScopeRenderMode.ProjectedOverlay)
            {
                return ScopeRenderMode.LegacyOverlay;
            }

            return renderMode;
        }

        internal static bool ShouldProcessExperimentalOpticCamera()
        {
            if (!Plugin.Enabled.Value
                || GetEffectiveRenderMode() != ScopeRenderMode.ExperimentalInScopeCamera)
            {
                return false;
            }

            ScopeRangefinderComponent instance = _activeInstance;
            return instance == null || !instance._usingMainCameraScope;
        }

        private void LateUpdate()
        {
            if (!_worldDisplayNeedsLateLayout || _worldRoot == null || !_worldRoot.activeSelf)
            {
                return;
            }

            if (!ApplyWorldDisplayLayout(_activeScopeCamera, _activeOpticSight, _activeWeaponAnimation))
            {
                SetWorldDisplayVisible(false);
                _worldDisplayNeedsLateLayout = false;
                return;
            }

            UpdateDistanceText(false, true);
        }

        internal static void AfterOpticCameraUpdated(Camera opticCamera)
        {
            ScopeRangefinderComponent instance = _activeInstance;
            if (instance == null
                || opticCamera == null
                || instance._usingMainCameraScope
                || !instance._worldDisplayNeedsLateLayout
                || instance._worldRoot == null
                || !instance._worldRoot.activeSelf
                || instance._activeScopeCamera != opticCamera)
            {
                return;
            }

            if (!instance.ApplyWorldDisplayLayout(
                    instance._activeScopeCamera,
                    instance._activeOpticSight,
                    instance._activeWeaponAnimation))
            {
                instance.SetWorldDisplayVisible(false);
                instance._worldDisplayNeedsLateLayout = false;
                return;
            }

            instance.UpdateDistanceText(false, true);
        }

        private static void HandleCameraPreCull(Camera camera)
        {
            ScopeRangefinderComponent instance = _activeInstance;
            if (instance == null
                || camera == null
                || instance._usingMainCameraScope
                || !instance._worldDisplayNeedsLateLayout
                || instance._worldRoot == null
                || !instance._worldRoot.activeSelf
                || instance._activeScopeCamera != camera)
            {
                return;
            }

            if (!instance.ApplyWorldDisplayLayout(
                    instance._activeScopeCamera,
                    instance._activeOpticSight,
                    instance._activeWeaponAnimation))
            {
                instance.SetWorldDisplayVisible(false);
                instance._worldDisplayNeedsLateLayout = false;
                return;
            }

            instance.UpdateDistanceText(false, true);
        }

        private void ResetAndHide()
        {
            _isScoped = false;
            _scopedElapsedTime = 0f;
            _usingMainCameraScope = false;
            _activeScopeCamera = null;
            _activeOpticSight = null;
            _activeWeaponAnimation = null;
            _currentLayoutKey = null;
            _worldDisplayNeedsLateLayout = false;

            if (_canvas != null)
            {
                _canvas.enabled = false;
            }

            SetWorldDisplayVisible(false);

            _timeSinceLastCast = 0f;
            _lastRaycastHit = false;
            _lastMeasuredDistance = 0f;
        }
    }
}
