using System.Linq;
using System.Text;
using EFT;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
        private static bool _fontInventoryLogged;

        private void LogFontInventoryOnce()
        {
            if (_fontInventoryLogged || !Plugin.LogLoadedFonts.Value)
            {
                return;
            }

            _fontInventoryLogged = true;

            var report = new StringBuilder();
            report.AppendLine("[FontInventory] Loaded font assets at first scope use:");

            TMP_FontAsset[] tmpFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            report.AppendLine($"[FontInventory] {tmpFonts.Length} TextMeshPro font assets:");
            foreach (TMP_FontAsset font in tmpFonts.OrderBy(f => f.name))
            {
                report.AppendLine(
                    $"[FontInventory]   TMP '{font.name}' | material '{font.material?.name}' | shader '{font.material?.shader?.name}'");
            }

            Font[] legacyFonts = Resources.FindObjectsOfTypeAll<Font>();
            report.AppendLine($"[FontInventory] {legacyFonts.Length} legacy fonts:");
            foreach (Font font in legacyFonts.OrderBy(f => f.name))
            {
                report.AppendLine($"[FontInventory]   Legacy '{font.name}' | dynamic={font.dynamic}");
            }

            TacticalRangeFinderController[] raptarDisplays =
                Resources.FindObjectsOfTypeAll<TacticalRangeFinderController>();
            foreach (TacticalRangeFinderController controller in raptarDisplays)
            {
                var displayText = AccessTools
                    .Field(typeof(TacticalRangeFinderController), "_textOnDisplay")
                    ?.GetValue(controller) as TMP_Text;
                report.AppendLine(
                    $"[FontInventory] RAPTAR display on '{controller.gameObject.name}': " +
                    $"font '{displayText?.font?.name}' | material '{displayText?.fontSharedMaterial?.name}' | " +
                    $"shader '{displayText?.fontSharedMaterial?.shader?.name}'");
            }

            if (raptarDisplays.Length == 0)
            {
                report.AppendLine(
                    "[FontInventory] No RAPTAR display loaded. Attach a Wilcox RAPTAR ES to a weapon " +
                    "in the raid to capture its display font.");
            }

            Plugin.LogSource.LogInfo(report.ToString());
        }
    }
}
