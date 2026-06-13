using System;
using Comfort.Common;
using EFT;
using EFT.Animations;
using EFT.CameraControl;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ScopeRangefinder
{
    internal class ScopeRangefinderComponent : MonoBehaviour
    {
        private const int OverlaySortingOrder = 30000;
        private const float RayStartOffset = 0.1f;
        private const float PiPUnreliableHitDistance = 5f;
        private const float PiPCloseHitAgreement = 0.75f;

        private Canvas _canvas;
        private RectTransform _panelRect;
        private TextMeshProUGUI _distanceText;
        private float _timeSinceLastCast;
        private float _lastMeasuredDistance;
        private bool _lastRaycastHit;
        private bool _isScoped;
        private float _scopedElapsedTime;
        private bool _usingMainCameraScope;
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

            if (!TryGetScopedState(out Camera scopeCamera, out ProceduralWeaponAnimation weaponAnimation, out Player player))
            {
                ResetAndHide();
                return;
            }

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
                return;
            }

            _canvas.enabled = true;
            UpdateDistanceText();
        }

        private bool TryGetScopedState(
            out Camera scopeCamera,
            out ProceduralWeaponAnimation weaponAnimation,
            out Player player)
        {
            scopeCamera = null;
            weaponAnimation = null;
            player = null;
            _usingMainCameraScope = false;

            if (!Singleton<GameWorld>.Instantiated)
            {
                return false;
            }

            GameWorld gameWorld = Singleton<GameWorld>.Instance;
            if (gameWorld == null)
            {
                return false;
            }

            player = gameWorld.MainPlayer;
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
            OpticSight currentOpticSight = opticManager?.CurrentOpticSight;
            Camera opticCamera = opticManager?.Camera;
            if (currentOpticSight != null
                && currentOpticSight.isActiveAndEnabled
                && opticCamera != null
                && opticCamera.gameObject.activeInHierarchy)
            {
                scopeCamera = opticCamera;
                return true;
            }

            if (Plugin.PiPDisablerLoaded)
            {
                Camera mainCamera = cameraClass.Camera;
                if (mainCamera != null && mainCamera.gameObject.activeInHierarchy)
                {
                    scopeCamera = mainCamera;
                    _usingMainCameraScope = true;
                    return true;
                }
            }

            return false;
        }

        private static bool IsPlayerUsable(Player player, GameWorld gameWorld)
        {
            if (player == null || !player.IsYourPlayer)
            {
                return false;
            }

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
            if (minZoom > 0f && !_usingMainCameraScope && !IsZoomedEnough(weaponAnimation, minZoom))
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

        private void MeasureDistance(Camera scopeCamera, ProceduralWeaponAnimation weaponAnimation, Player player)
        {
            float maxDistance = Plugin.MaxDistance.Value;
            Vector3 origin = scopeCamera.transform.position;
            Vector3 direction = scopeCamera.transform.forward;

            if (_usingMainCameraScope)
            {
                _lastRaycastHit = TryMeasurePiPDistance(
                    origin,
                    direction,
                    maxDistance,
                    weaponAnimation,
                    player,
                    out _lastMeasuredDistance);
                return;
            }

            _lastRaycastHit = Physics.Raycast(
                origin,
                direction,
                out RaycastHit hit,
                maxDistance,
                RaycastMask,
                QueryTriggerInteraction.Ignore);
            _lastMeasuredDistance = _lastRaycastHit ? hit.distance : 0f;
        }

        private static bool TryMeasurePiPDistance(
            Vector3 cameraOrigin,
            Vector3 aimDirection,
            float maxDistance,
            ProceduralWeaponAnimation weaponAnimation,
            Player player,
            out float distance)
        {
            distance = 0f;

            if (!TryRaycastSkippingSelfHits(
                    cameraOrigin,
                    aimDirection,
                    maxDistance,
                    weaponAnimation,
                    player,
                    out RaycastHit cameraHit))
            {
                return false;
            }

            if (cameraHit.distance >= PiPUnreliableHitDistance)
            {
                distance = cameraHit.distance;
                return true;
            }

            Transform fireport = weaponAnimation?.HandsContainer?.Fireport;
            if (fireport != null)
            {
                Vector3 fireportOrigin = fireport.position + aimDirection * RayStartOffset;
                if (TryRaycastSkippingSelfHits(
                        fireportOrigin,
                        aimDirection,
                        maxDistance,
                        weaponAnimation,
                        player,
                        out RaycastHit fireportHit))
                {
                    if (fireportHit.distance > cameraHit.distance + 0.25f)
                    {
                        distance = fireportHit.distance;
                        return true;
                    }

                    if (Mathf.Abs(fireportHit.distance - cameraHit.distance) <= PiPCloseHitAgreement)
                    {
                        distance = cameraHit.distance;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryRaycastSkippingSelfHits(
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            ProceduralWeaponAnimation weaponAnimation,
            Player player,
            out RaycastHit hit)
        {
            hit = default;
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                maxDistance,
                RaycastMask,
                QueryTriggerInteraction.Ignore);

            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            Transform weaponRoot = weaponAnimation?.HandsContainer?.WeaponRoot;
            Transform weaponTransform = weaponAnimation?.HandsContainer?.Weapon?.transform;
            Transform scopeRoot = weaponAnimation?.CurrentScope?.ScopePrefabCache?.transform;
            Transform scopeBone = weaponAnimation?.CurrentScope?.Bone;
            Transform cameraContainer = weaponAnimation?.CameraContainer?.transform;
            Transform trackingTransform = weaponAnimation?.HandsContainer?.TrackingTransform;

            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit candidate = hits[i];
                if (candidate.distance < RayStartOffset)
                {
                    continue;
                }

                if (ShouldIgnoreSelfHit(
                        candidate,
                        weaponRoot,
                        weaponTransform,
                        scopeRoot,
                        scopeBone,
                        cameraContainer,
                        trackingTransform,
                        player))
                {
                    continue;
                }

                if (candidate.distance < bestDistance)
                {
                    bestDistance = candidate.distance;
                    hit = candidate;
                    found = true;
                }
            }

            return found;
        }

        private static bool ShouldIgnoreSelfHit(
            RaycastHit hit,
            Transform weaponRoot,
            Transform weaponTransform,
            Transform scopeRoot,
            Transform scopeBone,
            Transform cameraContainer,
            Transform trackingTransform,
            Player player)
        {
            Transform hitTransform = hit.collider.transform;
            if (hitTransform == null)
            {
                return false;
            }

            if (IsTransformInHierarchy(hitTransform, weaponRoot)
                || IsTransformInHierarchy(hitTransform, weaponTransform)
                || IsTransformInHierarchy(hitTransform, scopeRoot)
                || IsTransformInHierarchy(hitTransform, scopeBone)
                || IsTransformInHierarchy(hitTransform, cameraContainer)
                || IsTransformInHierarchy(hitTransform, trackingTransform))
            {
                return true;
            }

            if (player?.PlayerBody != null)
            {
                if (IsTransformInHierarchy(hitTransform, player.PlayerBody.transform)
                    || IsTransformInHierarchy(hitTransform, player.PlayerBody.MeshTransform))
                {
                    return true;
                }
            }

            Transform playerTransform = player?.Transform?.Original;
            if (IsTransformInHierarchy(hitTransform, playerTransform))
            {
                return true;
            }

            return false;
        }

        private static bool IsTransformInHierarchy(Transform hitTransform, Transform root)
        {
            return root != null && (hitTransform == root || hitTransform.IsChildOf(root));
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
            _usingMainCameraScope = false;

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
