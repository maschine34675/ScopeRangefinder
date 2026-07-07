using EFT;
using EFT.Animations;
using UnityEngine;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
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
                    out _lastMeasuredDistance,
                    out _lastHitNormal);
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
            _lastHitNormal = _lastRaycastHit ? hit.normal : Vector3.up;
        }

        private static bool TryMeasurePiPDistance(
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

            if (cameraHit.distance >= PiPUnreliableHitDistance)
            {
                distance = cameraHit.distance;
                hitNormal = cameraHit.normal;
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
                        hitNormal = fireportHit.normal;
                        return true;
                    }

                    if (Mathf.Abs(fireportHit.distance - cameraHit.distance) <= PiPCloseHitAgreement)
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
