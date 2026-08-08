using Comfort.Common;
using EFT;
using EFT.Animations;
using EFT.CameraControl;
using EFT.InventoryLogic;
using UnityEngine;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
        private bool TryGetScopedState(
            out Camera scopeCamera,
            out OpticSight currentOpticSight,
            out ProceduralWeaponAnimation weaponAnimation,
            out Player player)
        {
            scopeCamera = null;
            currentOpticSight = null;
            weaponAnimation = null;
            player = null;
            _usingMainCameraScope = false;
            _raptarActivationOverride = false;

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
                || weaponAnimation.CurrentScope == null
                || !weaponAnimation.CurrentScope.IsOptic)
            {
                return false;
            }

            if (Plugin.RequireWilcoxRaptar.Value
                && !HasRequiredWilcoxRaptar(firearmController.Item))
            {
                return false;
            }
            _raptarActivationOverride = Plugin.RequireWilcoxRaptar.Value && Plugin.RequireWilcoxRaptarActive.Value;

            CameraManager cameraManager = CameraManager.Instance;
            if (cameraManager == null)
            {
                return false;
            }

            OpticCameraManager opticManager = cameraManager.OpticCameraManager;
            OpticSight activeOpticSight = opticManager?.CurrentOpticSight;
            Camera opticCamera = opticManager?.Camera;
            if (activeOpticSight != null
                && activeOpticSight.isActiveAndEnabled
                && opticCamera != null
                && opticCamera.isActiveAndEnabled)
            {
                scopeCamera = opticCamera;
                currentOpticSight = activeOpticSight;
                return true;
            }
            if (Plugin.PiPDisablerLoaded)
            {
                Camera mainCamera = cameraManager.Camera;
                if (mainCamera != null && mainCamera.gameObject.activeInHierarchy)
                {
                    scopeCamera = mainCamera;
                    currentOpticSight = GetCurrentOpticSight(weaponAnimation);
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

        private static bool HasRequiredWilcoxRaptar(Item itemInHands)
        {
            if (itemInHands == null)
            {
                return false;
            }

            foreach (Item item in itemInHands.GetAllItems())
            {
                if (item.StringTemplateId != WilcoxRaptarTemplateId)
                {
                    continue;
                }

                if (!Plugin.RequireWilcoxRaptarActive.Value)
                {
                    return true;
                }

                LightComponent lightComponent = item.GetItemComponent<LightComponent>();
                if (lightComponent != null && lightComponent.IsActive)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ShouldShowReadout(ProceduralWeaponAnimation weaponAnimation)
        {
            if (_raptarActivationOverride)
            {
                return true;
            }

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
            ScopePrefabCache scopeCache = weaponAnimation.CurrentScope?.ScopePrefabCache;
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

        private static OpticSight GetCurrentOpticSight(ProceduralWeaponAnimation weaponAnimation)
        {
            return weaponAnimation?.CurrentScope?.ScopePrefabCache?.CurrentModOpticSight;
        }
    }
}
