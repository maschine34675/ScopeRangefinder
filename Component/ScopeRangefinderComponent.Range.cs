using EFT;
using EFT.Animations;
using UnityEngine;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
        private bool _measurementFrozen;
        private float _lastValidHitTime = float.NegativeInfinity;
        private float _lastValidDistance;
        private Vector3 _lastValidHitNormal = Vector3.up;
        private float _scanExtremeDistance;
        private Vector3 _scanExtremeNormal = Vector3.up;
        private float _scanExtremeTime = float.NegativeInfinity;
        private object _measurementSight;
        private int _measurementScopeIndex = -1;
        private bool _measurementUsedMainCamera;

        private void MeasureDistance(Camera scopeCamera, ProceduralWeaponAnimation weaponAnimation, Player player)
        {
            EFT.InventoryLogic.SightComponent sight = weaponAnimation?.CurrentAimingMod;
            int scopeIndex = sight != null ? sight.SelectedScopeIndex : -1;
            if (!ReferenceEquals(sight, _measurementSight)
                || scopeIndex != _measurementScopeIndex
                || _usingMainCameraScope != _measurementUsedMainCamera)
            {
                ResetMeasurementRobustness();
                _measurementSight = sight;
                _measurementScopeIndex = scopeIndex;
                _measurementUsedMainCamera = _usingMainCameraScope;
            }

            if (_measurementFrozen)
            {
                PublishMeasurementToApi();
                return;
            }

            float maxDistance = Plugin.MaxDistance.Value;
            Vector3 origin = scopeCamera.transform.position;
            Vector3 direction = scopeCamera.transform.forward;

            bool rawHit;
            float rawDistance;
            Vector3 rawNormal;
            if (_usingMainCameraScope)
            {
                rawHit = TryMeasureMainCameraDistance(
                    origin,
                    direction,
                    maxDistance,
                    weaponAnimation,
                    player,
                    out rawDistance,
                    out rawNormal);
            }
            else
            {
                rawHit = Physics.Raycast(
                    origin,
                    direction,
                    out RaycastHit hit,
                    maxDistance,
                    RaycastMask,
                    QueryTriggerInteraction.Ignore);
                rawDistance = rawHit ? hit.distance : 0f;
                rawNormal = rawHit ? hit.normal : Vector3.up;
            }

            RefineMeasurement(rawHit, rawDistance, rawNormal);
            PublishMeasurementToApi();
        }
        private void RefineMeasurement(bool rawHit, float rawDistance, Vector3 rawNormal)
        {
            float now = Time.time;
            float holdSeconds = Mathf.Max(0f, Plugin.MeasurementHoldTime.Value);
            ScanMode scanMode = Plugin.MeasurementScanMode.Value;
            float scanSeconds = Mathf.Max(0f, Plugin.MeasurementScanWindow.Value);
            bool validHit = rawHit && rawDistance > 0f;

            if (validHit)
            {
                _lastValidHitTime = now;
                _lastValidDistance = rawDistance;
                _lastValidHitNormal = rawNormal;
            }
            if (scanMode != ScanMode.Off && scanSeconds > 0f)
            {
                bool extremeExpired = now - _scanExtremeTime > scanSeconds;
                if (validHit)
                {
                    bool replaces = extremeExpired
                        || (scanMode == ScanMode.Near
                            ? rawDistance < _scanExtremeDistance
                            : rawDistance > _scanExtremeDistance);
                    if (replaces)
                    {
                        _scanExtremeDistance = rawDistance;
                        _scanExtremeNormal = rawNormal;
                        _scanExtremeTime = now;
                    }
                }

                if (validHit || !extremeExpired)
                {
                    _lastRaycastHit = true;
                    _lastMeasuredDistance = _scanExtremeDistance;
                    _lastHitNormal = _scanExtremeNormal;
                    return;
                }
            }
            else
            {
                _scanExtremeTime = float.NegativeInfinity;
            }
            if (rawHit)
            {
                _lastRaycastHit = true;
                _lastMeasuredDistance = rawDistance;
                _lastHitNormal = rawNormal;
                return;
            }
            if (holdSeconds > 0f && now - _lastValidHitTime <= holdSeconds)
            {
                _lastRaycastHit = true;
                _lastMeasuredDistance = _lastValidDistance;
                _lastHitNormal = _lastValidHitNormal;
                return;
            }

            _lastRaycastHit = false;
            _lastMeasuredDistance = 0f;
            _lastHitNormal = Vector3.up;
        }
        private void UpdateMeasurementFreeze()
        {
            if (BlocksGameKeyboardInput
                || !HotkeyInput.IsDownIgnoringOtherKeys(Plugin.MeasurementFreezeHotkey.Value))
            {
                return;
            }

            if (_measurementFrozen)
            {
                _measurementFrozen = false;
                _timeSinceLastCast = float.MaxValue;
                return;
            }
            if (_lastRaycastHit && _lastMeasuredDistance > 0f)
            {
                _measurementFrozen = true;
            }
        }

        private void ResetMeasurementRobustness()
        {
            _measurementSight = null;
            _measurementScopeIndex = -1;
            _measurementFrozen = false;
            _lastValidHitTime = float.NegativeInfinity;
            _lastValidDistance = 0f;
            _lastValidHitNormal = Vector3.up;
            _scanExtremeTime = float.NegativeInfinity;
            _scanExtremeDistance = 0f;
            _scanExtremeNormal = Vector3.up;
        }
        private void PublishMeasurementToApi()
        {
            if (_lastRaycastHit && _lastMeasuredDistance > 0f)
            {
                RangefinderApi.LastMeasuredDistanceMeters = _lastMeasuredDistance;
                RangefinderApi.LastMeasurementTime = Time.time;
                return;
            }

            RangefinderApi.LastMeasuredDistanceMeters = 0f;
        }

        private static bool TryMeasureMainCameraDistance(
            Vector3 cameraOrigin,
            Vector3 aimDirection,
            float maxDistance,
            ProceduralWeaponAnimation weaponAnimation,
            Player player,
            out float distance,
            out Vector3 hitNormal)
        {
            distance = 0f;
            hitNormal = Vector3.up;

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

            if (cameraHit.distance >= MainCameraUnreliableHitDistance)
            {
                distance = cameraHit.distance;
                hitNormal = cameraHit.normal;
                return true;
            }

            Transform fireport = weaponAnimation?.HandsContainer?.Fireport;
            if (fireport != null)
            {
                Vector3 toCameraHit = cameraHit.point - fireport.position;
                float toCameraHitDistance = toCameraHit.magnitude;
                if (toCameraHitDistance > 0.05f
                    && (!TryRaycastSkippingSelfHits(
                            fireport.position,
                            toCameraHit / toCameraHitDistance,
                            toCameraHitDistance - 0.05f,
                            weaponAnimation,
                            player,
                            out RaycastHit occluderHit)
                        || occluderHit.distance >= toCameraHitDistance - 0.1f))
                {
                    distance = cameraHit.distance;
                    hitNormal = cameraHit.normal;
                    return true;
                }
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
                        hitNormal = fireportHit.normal;
                        return true;
                    }

                    if (Mathf.Abs(fireportHit.distance - cameraHit.distance) <= MainCameraCloseHitAgreement)
                    {
                        distance = cameraHit.distance;
                        hitNormal = cameraHit.normal;
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
    }
}
