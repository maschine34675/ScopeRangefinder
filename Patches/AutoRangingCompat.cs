using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace ScopeRangefinder
{
    internal static class AutoRangingCompat
    {
        public const string AutoRangingGuid = "com.vultify.autoranging";

        public static void TryApply(Harmony harmony)
        {
            if (!Chainloader.PluginInfos.TryGetValue(AutoRangingGuid, out var pluginInfo)
                || pluginInfo.Instance == null)
            {
                return;
            }

            MethodInfo doRange = pluginInfo.Instance.GetType().GetMethod(
                "DoRange",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (doRange == null)
            {
                Plugin.LogSource.LogWarning(
                    "AutoRanging detected, but its DoRange method was not found. " +
                    "Disable either AutoRanging or AutoZeroEnabled manually; running both will fight over the zeroing.");
                return;
            }

            harmony.Patch(doRange, prefix: new HarmonyMethod(
                typeof(AutoRangingCompat),
                nameof(SuppressWhileAutoZeroActive)));
            Plugin.LogSource.LogInfo(
                "AutoRanging detected: its ranging is suppressed while AutoZeroEnabled is on, " +
                "since the two mods would otherwise fight over the sight zeroing.");
        }

        private static bool SuppressWhileAutoZeroActive()
        {
            return !(Plugin.Enabled.Value && Plugin.AutoZeroEnabled.Value);
        }
    }
}
