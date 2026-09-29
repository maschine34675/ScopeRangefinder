using System;
using EFT.InventoryLogic;
using HarmonyLib;

namespace ScopeRangefinder
{
    [HarmonyPatch(typeof(Weapon), nameof(Weapon.RecalculateScopeCalibrationPoints))]
    internal static class LoadedAmmoZeroingPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Weapon __instance, SightComponent sight, int scopeIndex)
        {
            try
            {
                return !LoadedAmmoZeroing.TryCalculateScope(__instance, sight, scopeIndex);
            }
            catch (Exception error)
            {
                LoadedAmmoZeroing.LogFailureOnce(error);
                return true;
            }
        }
    }
}
