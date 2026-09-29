using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
        private const float ZeroingRangeSyncInterval = 1f;
        private int _appliedZeroingLimit;
        private bool _appliedLoadedAmmoZeroing;
        private Weapon _zeroingSyncWeapon;
        private float _zeroingSyncTimer;
        private Weapon _ammoRecalculatedWeapon;
        private AmmoTemplate _ammoRecalculatedFor;

        private void UpdateManualZeroing()
        {
            int limit = Plugin.Enabled.Value ? Plugin.MaxZeroingDistance.Value : 0;
            bool loadedAmmo = LoadedAmmoZeroing.Active;
            ZeroingRangeExtension.Apply(limit);
            if (limit != _appliedZeroingLimit || loadedAmmo != _appliedLoadedAmmoZeroing)
            {
                _appliedZeroingLimit = limit;
                _appliedLoadedAmmoZeroing = loadedAmmo;
                RecalculateZeroingInWorld();
                _zeroingSyncWeapon = null;
                _ammoRecalculatedWeapon = null;
            }

            if (limit <= 0 && !loadedAmmo)
            {
                return;
            }

            Player player = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance.MainPlayer : null;
            Weapon weapon = (player?.HandsController as Player.FirearmController)?.Item as Weapon;
            if (weapon == null)
            {
                _zeroingSyncWeapon = null;
                return;
            }

            bool recalculate = false;
            string recalculateReason = "steps of another length";
            if (loadedAmmo && LoadedAmmoZeroing.IsStale(weapon))
            {
                AmmoTemplate loaded = LoadedAmmoZeroing.FindLoadedAmmo(weapon);
                if (weapon != _ammoRecalculatedWeapon || loaded != _ammoRecalculatedFor)
                {
                    _ammoRecalculatedWeapon = weapon;
                    _ammoRecalculatedFor = loaded;
                    recalculate = true;
                    recalculateReason = "loaded round changed";
                }
            }

            if (limit > 0)
            {
                _zeroingSyncTimer -= Time.deltaTime;
                if (weapon != _zeroingSyncWeapon || _zeroingSyncTimer <= 0f)
                {
                    _zeroingSyncWeapon = weapon;
                    _zeroingSyncTimer = ZeroingRangeSyncInterval;
                    recalculate |= ZeroingRangeExtension.HasStaleCalibration(SightsOf(weapon));
                }
            }

            if (recalculate)
            {
                RecalculateZeroing(weapon, player, recalculateReason);
            }
        }
        private static void RecalculateZeroing(Weapon weapon, Player holder, string reason)
        {
            weapon.RecalculateOpticCalibrationPoints();
            foreach (SightComponent sight in SightsOf(weapon))
            {
                int[] chosen = sight.ScopesCurrentCalibPointIndexes;
                if (chosen == null)
                {
                    continue;
                }

                for (int scope = 0; scope < chosen.Length && scope < sight.ScopesCount; scope++)
                {
                    sight.SetSelectedOpticCalibrationPoint(scope, chosen[scope]);
                }
            }
            if (holder != null && holder.IsYourPlayer)
            {
                holder.ProceduralWeaponAnimation?.method_2();
                if (Plugin.LogScopeKeys.Value)
                {
                    LogZeroingRecalculated(weapon, holder, reason);
                }
            }
        }
        private static void RecalculateZeroingInWorld()
        {
            if (!Singleton<GameWorld>.Instantiated)
            {
                return;
            }

            GameWorld world = Singleton<GameWorld>.Instance;
            var done = new HashSet<Weapon>();
            Player main = world.MainPlayer;
            if (main?.Inventory != null)
            {
                foreach (Item item in main.Inventory.AllRealPlayerItems)
                {
                    if (item is Weapon carried && HasBuiltCalibration(carried) && done.Add(carried))
                    {
                        RecalculateZeroing(carried, main, "settings changed");
                    }
                }
            }

            foreach (Player player in world.AllAlivePlayersList)
            {
                if ((player?.HandsController as Player.FirearmController)?.Item is Weapon held
                    && HasBuiltCalibration(held)
                    && done.Add(held))
                {
                    RecalculateZeroing(held, player, "settings changed");
                }
            }
        }
        private static void LogZeroingRecalculated(Weapon weapon, Player holder, string reason)
        {
            SightComponent sight = holder.ProceduralWeaponAnimation?.CurrentAimingMod;
            if (sight == null || sight.OpticCalibrationPoints == null)
            {
                sight = null;
                foreach (SightComponent candidate in SightsOf(weapon))
                {
                    if (candidate.OpticCalibrationPoints != null)
                    {
                        sight = candidate;
                        break;
                    }
                }
            }

            string weaponName = weapon.Template?._name;
            if (sight == null)
            {
                Plugin.LogSource?.LogInfo($"Zeroing recalculated ({reason}) for {weaponName}: no sight calibration built yet.");
                return;
            }

            int scope = sight.SelectedScopeIndex;
            int[] steps = sight.GetScopeCalibrationDistances(scope);
            Vector3[] points = scope < sight.OpticCalibrationPoints.Length ? sight.OpticCalibrationPoints[scope] : null;
            int[] chosen = sight.ScopesCurrentCalibPointIndexes;
            int index = chosen != null && scope < chosen.Length ? chosen[scope] : -1;
            if (steps == null || steps.Length == 0 || points == null || index < 0 || index >= steps.Length || index >= points.Length)
            {
                Plugin.LogSource?.LogInfo($"Zeroing recalculated ({reason}) for {weaponName}: sight {sight.Item?.Template?._name} has no dial.");
                return;
            }

            AmmoTemplate calculatedFor = LoadedAmmoZeroing.CalculatedFor(weapon);
            AmmoTemplate loaded = LoadedAmmoZeroing.FindLoadedAmmo(weapon);
            AmmoTemplate defaultAmmo = weapon.Template?.DefAmmoTemplate;
            string comparison = string.Empty;
            if (defaultAmmo != null)
            {
                Vector3[] defaultPoint = OpticCalibration.CalculatePoints(new[] { steps[index] }, defaultAmmo, weapon.SpeedFactor);
                if (defaultPoint != null)
                {
                    float angle = ComputeHoldMilliradians(defaultPoint[0], points[index]);
                    comparison = $", {Mathf.Abs(angle):F2} mrad from the default round {defaultAmmo._name}";
                }
            }

            Plugin.LogSource?.LogInfo(
                $"Zeroing recalculated ({reason}) for {weaponName}: sight {sight.Item?.Template?._name} scope {scope}, "
                + $"steps {steps[0]}..{steps[steps.Length - 1]} m ({steps.Length}), dial at {steps[index]} m, "
                + $"calculated for {(calculatedFor != null ? calculatedFor._name : "the game's default")}, "
                + $"loaded {(loaded != null ? loaded._name : "nothing")}{comparison}.");
        }

        private static bool HasBuiltCalibration(Weapon weapon)
        {
            foreach (SightComponent sight in SightsOf(weapon))
            {
                if (sight.OpticCalibrationPoints != null)
                {
                    return true;
                }
            }

            return false;
        }
        private static IEnumerable<SightComponent> SightsOf(Weapon weapon)
        {
            return weapon.GetAllItemsFromCollection().GetComponents<SightComponent>();
        }
    }
}
