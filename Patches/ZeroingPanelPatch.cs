using EFT;
using HarmonyLib;

namespace ScopeRangefinder
{
    [HarmonyPatch(typeof(Player), nameof(Player.ShowAmmoCountZeroingPanel))]
    internal static class ZeroingPanelPatch
    {
        [HarmonyPrefix]
        private static bool PatchPrefix(Player __instance, ref string message)
        {
            if (__instance == null || !__instance.IsYourPlayer)
            {
                return true;
            }
            if (message != null && message.Contains("|"))
            {
                return true;
            }
            if (ScopeRangefinderComponent.ShouldSuppressZeroingPanel())
            {
                return false;
            }

            if (ScopeRangefinderComponent.TryGetZeroingPanelText(out string panelText))
            {
                message = panelText;
            }

            return true;
        }
    }
}
