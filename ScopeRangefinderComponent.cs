using Comfort.Common;
using EFT;
using EFT.Animations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeRangefinder
{
    internal class ScopeRangefinderComponent : MonoBehaviour
    {
        private const int OverlaySortingOrder = 30000;

        private Canvas _canvas;
        private RectTransform _panelRect;
        private TextMeshProUGUI _distanceText;
        private float _timeSinceLastCast;
        private float _lastMeasuredDistance;
        private bool _lastRaycastHit;
        private bool _isScoped;
        private float _scopedElapsedTime;
        private static readonly int RaycastMask =
            LayerMaskClass.HighPolyWithTerrainMask
            | LayerMaskClass.TransparentLayerMask
            | LayerMaskClass.HitColliderMask;

        private void Awake()
        {
            CreateOverlay();
            ResetAndHide();
        }

        private void Update()
        {
            if (!Plugin.Enabled.Value)
            {
                ResetAndHide();
                return;
            }

            ApplyDisplayLayout();

            if (!TryGetScopedState(out Camera opticCamera, out ProceduralWeaponAnimation weaponAnimation))
            {
                ResetAndHide();
                return;
            }

            if (!_isScoped)
            {
                _isScoped = true;
                _scopedElapsedTime = 0f;
                MeasureDistance(opticCamera);
            }
            else
            {
                _scopedElapsedTime += Time.deltaTime;
            }

            _timeSinceLastCast += Time.deltaTime;
            if (_timeSinceLastCast >= Plugin.UpdateInterval.Value)
            {
                _timeSinceLastCast = 0f;
                MeasureDistance(opticCamera);
            }

            if (_scopedElapsedTime < Plugin.DisplayShowDelay.Value || !ShouldShowReadout(weaponAnimation))
            {
                _canvas.enabled = false;
                return;
            }

            _canvas.enabled = true;
            UpdateDistanceText();
        }

        private bool TryGetScopedState(out Camera opticCamera, out ProceduralWeaponAnimation weaponAnimation)
        {
            opticCamera = null;
            weaponAnimation = null;

            if (!Singleton<GameWorld>.Instantiated)
            {
                return false;
            }

            GameWorld gameWorld = Singleton<GameWorld>.Instance;
            if (gameWorld == null)
            {
                return false;
            }

            Player player = gameWorld.MainPlayer;
            if (!IsPlayerUsable(player, gameWorld))
            {
                return false;
            }

            if (player.HandsController is not Player.FirearmController firearmController || !firearmController.IsAiming)
            {
                return false;
            }

            weaponAnimation = player.ProceduralWeaponAnimation;
            if (weaponAnimation == null
                || weaponAnimation.ScopeAimTransforms.Count < 1
                || !weaponAnimation.CurrentScope.IsOptic)
            {
                return false;
            }

            CameraClass cameraClass = CameraClass.Instance;
            if (cameraClass == null)
            {
                return false;
            }

            GClass3687 opticManager = cameraClass.OpticCameraManager;
            if (opticManager?.CurrentOpticSight == null || !opticManager.CurrentOpticSight.isActiveAndEnabled)
            {
                return false;
            }

            opticCamera = opticManager.Camera;
            return opticCamera != null && opticCamera.gameObject.activeInHierarchy;
        }

        private static bool IsPlayerUsable(Player player, GameWorld gameWorld)
        {
            if (player == null || !player.IsYourPlayer)
            {
                return false;
            }

            // MainPlayer can outlive the body during raid teardown.
            if (player.PlayerBody == null || player.HandsController == null)
            {
                return false;
            }

            if (!gameWorld.AllAlivePlayersList.Contains(player))
            {
                return false;
            }

            return player.PointOfView == EPointOfView.FirstPerson;
        }

        private void ApplyDisplayLayout()
        {
            if (_panelRect == null)
            {
                return;
            }

            _panelRect.anchoredPosition = new Vector2(
                ScopeDisplayStyle.DefaultOffsetX + Plugin.DisplayOffsetX.Value,
                ScopeDisplayStyle.DefaultOffsetY + Plugin.DisplayOffsetY.Value);
        }

        private bool ShouldShowReadout(ProceduralWeaponAnimation weaponAnimation)
        {
            float minDistance = Plugin.MinDisplayDistance.Value;
            if (minDistance > 0f)
            {
                return _lastRaycastHit && _lastMeasuredDistance >= minDistance;
            }

            float minZoom = Plugin.MinZoomBlendFactor.Value;
            if (minZoom > 0f && !IsZoomedEnough(weaponAnimation, minZoom))
            {
                return false;
            }

            return true;
        }

        private static bool IsZoomedEnough(ProceduralWeaponAnimation weaponAnimation, float minBlendFactor)
        {
            ScopePrefabCache scopeCache = weaponAnimation.CurrentScope.ScopePrefabCache;
            if (scopeCache == null)
            {
                return true;
            }

            ScopeZoomHandler zoomHandler = scopeCache.GetComponentInChildren<ScopeZoomHandler>(true);
            if (zoomHandler == null)
            {
                return true;
            }

            return zoomHandler.BlendFactor >= minBlendFactor;
        }

        private void MeasureDistance(Camera opticCamera)
        {
            Vector3 origin = opticCamera.transform.position;
            Vector3 direction = opticCamera.transform.forward;
            float maxDistance = Plugin.MaxDistance.Value;

            _lastRaycastHit = Physics.Raycast(
                origin,
                direction,
                out RaycastHit hit,
                maxDistance,
                RaycastMask,
                QueryTriggerInteraction.Ignore);

            _lastMeasuredDistance = _lastRaycastHit ? hit.distance : 0f;
        }

        private void UpdateDistanceText()
        {
            if (!_lastRaycastHit)
            {
                _distanceText.SetMonospaceText(Plugin.NoDistanceText.Value, true);
                return;
            }

            if (Plugin.UseDecimalFormat.Value)
            {
                float clamped = Mathf.Clamp(_lastMeasuredDistance, 0f, 999f);
                _distanceText.SetMonospaceText(clamped.ToString("000.0"), true);
            }
            else
            {
                int meters = Mathf.RoundToInt(_lastMeasuredDistance);
                _distanceText.SetMonospaceText(meters.ToString("D4"), true);
            }
        }

        private void ResetAndHide()
        {
            _isScoped = false;
            _scopedElapsedTime = 0f;

            if (_canvas != null)
            {
                _canvas.enabled = false;
            }

            _timeSinceLastCast = 0f;
            _lastRaycastHit = false;
            _lastMeasuredDistance = 0f;
        }

        private void CreateOverlay()
        {
            var canvasObject = new GameObject("ScopeRangefinderCanvas");
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = OverlaySortingOrder;
            _canvas.overrideSorting = true;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform panelRect = ScopeDisplayStyle.CreateDisplayPanel(canvasObject.transform);

            _panelRect = panelRect;
            _distanceText = ScopeDisplayStyle.CreateReadoutText(panelRect);
        }
    }
}
