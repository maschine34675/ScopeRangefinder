using EFT.InputSystem;
using HarmonyLib;
using System.Collections.Generic;

namespace ScopeRangefinder
{
    [HarmonyPatch(typeof(InputNode), nameof(InputNode.TranslateInput))]
    internal static class LayoutEditorInputPatch
    {
        [HarmonyPrefix]
        private static void PatchPrefix(List<ECommand> commands)
        {
            if (commands == null)
            {
                return;
            }
            if (ScopeRangefinderComponent.BlocksGameKeyboardInput)
            {
                commands.Clear();
                return;
            }

            if (!ScopeRangefinderComponent.BlocksGameMouseInput)
            {
                return;
            }

            commands.RemoveAll(command =>
                command == ECommand.ToggleShooting
                || command == ECommand.ToggleAlternativeShooting);
        }
    }
}
