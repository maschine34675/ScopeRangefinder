using BepInEx.Configuration;
using UnityEngine;

namespace ScopeRangefinder
{
    internal static class HotkeyInput
    {
        public static bool IsDownIgnoringOtherKeys(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None || !Input.GetKeyDown(shortcut.MainKey))
            {
                return false;
            }

            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (!Input.GetKey(modifier))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
