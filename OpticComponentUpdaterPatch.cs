using EFT.CameraControl;
using HarmonyLib;
using UnityEngine;

namespace ScopeRangefinder
{
    [HarmonyPatch(typeof(OpticComponentUpdater), "LateUpdate")]
    internal static class OpticComponentUpdaterPatch
    {
        [HarmonyPostfix]
        private static void PatchPostfix(OpticComponentUpdater __instance)
        {
            if (__instance == null || !ScopeRangefinderComponent.ShouldProcessExperimentalOpticCamera())
            {
                return;
            }

            Camera opticCamera = __instance.GetComponent<Camera>();
            ScopeRangefinderComponent.ApplyScopeAntialiasingOverride(opticCamera);
            ScopeRangefinderComponent.AfterOpticCameraUpdated(opticCamera);
        }
    }
}
