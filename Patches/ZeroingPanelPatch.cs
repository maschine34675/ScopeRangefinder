using EFT;
using HarmonyLib;

namespace ScopeRangefinder
{
    [HarmonyPatch(typeof(Player), nameof(Player.ShowAmmoCountZeroingPanel))]
    internal static class ZeroingPanelPatch
    {
        [HarmonyPrefix]
        private static void PatchPrefix(Player __instance, ref string message)
        {
            if (__instance == null || !__instance.IsYourPlayer)
            {
                return;
            }

            if (ScopeRangefinderComponent.TryGetZeroingPanelText(out string panelText))
            {
                message = panelText;
            }
        }
    }
}
