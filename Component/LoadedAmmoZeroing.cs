using System;
using System.Runtime.CompilerServices;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;

namespace ScopeRangefinder
{
    internal static class LoadedAmmoZeroing
    {
        private sealed class Record
        {
            internal AmmoTemplate Ammo;
        }
        private static readonly ConditionalWeakTable<Weapon, Record> Calculated =
            new ConditionalWeakTable<Weapon, Record>();

        private static bool _failureLogged;

        internal static bool Active => Plugin.Enabled.Value && Plugin.ZeroForLoadedAmmo.Value;
        internal static AmmoTemplate FindLoadedAmmo(Weapon weapon)
        {
            Slot[] chambers = weapon.Chambers;
            if (chambers != null && chambers.Length > 0 && chambers[0]?.ContainedItem is Ammo chambered)
            {
                return chambered.Template as AmmoTemplate;
            }

            Magazine magazine = weapon.GetCurrentMagazine();
            if (magazine?.Cartridges != null)
            {
                return magazine.FirstRealAmmo()?.Template as AmmoTemplate;
            }

            return null;
        }
        internal static bool IsStale(Weapon weapon)
        {
            AmmoTemplate loaded = FindLoadedAmmo(weapon);
            if (loaded == null)
            {
                return false;
            }

            return !Calculated.TryGetValue(weapon, out Record record) || record.Ammo != loaded;
        }
        internal static bool TryCalculateScope(Weapon weapon, SightComponent sight, int scopeIndex)
        {
            if (weapon == null || sight == null || !IsLocalPlayers(weapon))
            {
                return false;
            }

            Record record = Calculated.GetOrCreateValue(weapon);
            if (!Active)
            {
                record.Ammo = null;
                return false;
            }
            AmmoTemplate ammo = FindLoadedAmmo(weapon) ?? record.Ammo ?? weapon.Template?.DefAmmoTemplate;
            if (!OpticCalibration.TryCalculateScope(weapon, sight, scopeIndex, ammo))
            {
                return false;
            }

            record.Ammo = ammo;
            return true;
        }
        internal static AmmoTemplate CalculatedFor(Weapon weapon)
        {
            return weapon != null && Calculated.TryGetValue(weapon, out Record record) ? record.Ammo : null;
        }

        private static bool IsLocalPlayers(Weapon weapon)
        {
            if (!Singleton<GameWorld>.Instantiated)
            {
                return false;
            }
            Player main = Singleton<GameWorld>.Instance.MainPlayer;
            return main != null && ReferenceEquals(weapon.Owner, main.InventoryController);
        }

        internal static void LogFailureOnce(Exception error)
        {
            if (_failureLogged)
            {
                return;
            }

            _failureLogged = true;
            Plugin.LogSource?.LogWarning(
                "Zeroing for the loaded ammo failed and was left to the game ("
                + error.GetType().Name + ": " + error.Message + ").");
        }
    }
}
