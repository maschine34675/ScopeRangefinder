using Comfort.Common;
using EFT;
using EFT.Animations;
using EFT.InventoryLogic;
using System.Collections.Generic;
using UnityEngine;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
        private const float AutoZeroTrajectoryStep = 0.025f;
        private const float AutoZeroLineWidth = 0.015f;

        private SightComponent _autoZeroSight;
        private int _autoZeroScopeIndex = -1;
        private Vector3[] _autoZeroOriginalPoints;
        private int _autoZeroLastDistance;
        private AmmoTemplate _autoZeroLastAmmo;
        private LineRenderer _autoZeroTrajectoryLine;

        private void UpdateAutoZero(Player player, ProceduralWeaponAnimation weaponAnimation)
        {
            if (!Plugin.AutoZeroEnabled.Value || !_lastRaycastHit)
            {
                RestoreAutoZero(weaponAnimation);
                SetTrajectoryDebugVisible(false);
                return;
            }

            if (player?.HandsController is not Player.FirearmController firearmController
                || firearmController.Item is not Weapon weapon
                || weaponAnimation?.CurrentAimingMod == null)
            {
                RestoreAutoZero(weaponAnimation);
                SetTrajectoryDebugVisible(false);
                return;
            }

            SightComponent sight = weaponAnimation.CurrentAimingMod;
            int scopeIndex = sight.SelectedScopeIndex;
            if (!TryGetMutableCalibrationPoints(sight, scopeIndex, out Vector3[] points, out int pointIndex))
            {
                RestoreAutoZero(weaponAnimation);
                SetTrajectoryDebugVisible(false);
                return;
            }

            AmmoTemplate ammo = weapon.CurrentAmmoTemplate ?? weapon.Template?.DefAmmoTemplate;
            int distance = Mathf.Clamp(Mathf.RoundToInt(_lastMeasuredDistance), 1, Mathf.RoundToInt(Plugin.MaxDistance.Value));
            if (ammo == null || !TryCalculateCalibrationPoint(weapon, ammo, distance, out Vector3 calibrationPoint))
            {
                RestoreAutoZero(weaponAnimation);
                SetTrajectoryDebugVisible(false);
                return;
            }

            EnsureAutoZeroBackup(sight, scopeIndex, points);

            bool changed = _autoZeroSight != sight
                || _autoZeroScopeIndex != scopeIndex
                || _autoZeroLastDistance != distance
                || _autoZeroLastAmmo != ammo;

            points[pointIndex] = calibrationPoint;
            sight.OpticCalibrationPoints[scopeIndex] = points;

            if (changed)
            {
                _autoZeroLastDistance = distance;
                _autoZeroLastAmmo = ammo;
                weaponAnimation.method_2();
                player.ShowAmmoCountZeroingPanel($"{distance}m auto");
            }

            UpdateTrajectoryDebug(weaponAnimation, ammo, weapon.SpeedFactor);
        }

        private bool TryGetMutableCalibrationPoints(
            SightComponent sight,
            int scopeIndex,
            out Vector3[] points,
            out int pointIndex)
        {
            points = null;
            pointIndex = -1;

            if (sight == null
                || scopeIndex < 0
                || !sight.HasOpticCalibrationPoints(scopeIndex))
            {
                return false;
            }

            points = sight.OpticCalibrationPoints[scopeIndex];
            if (points == null || points.Length == 0)
            {
                return false;
            }

            pointIndex = sight.method_0(scopeIndex);
            return pointIndex >= 0 && pointIndex < points.Length;
        }

        private void EnsureAutoZeroBackup(SightComponent sight, int scopeIndex, Vector3[] points)
        {
            if (_autoZeroSight == sight && _autoZeroScopeIndex == scopeIndex)
            {
                return;
            }

            RestoreAutoZero();
            _autoZeroSight = sight;
            _autoZeroScopeIndex = scopeIndex;
            _autoZeroOriginalPoints = (Vector3[])points.Clone();
            _autoZeroLastDistance = -1;
            _autoZeroLastAmmo = null;
        }

        private static bool TryCalculateCalibrationPoint(
            Weapon weapon,
            AmmoTemplate ammo,
            int distance,
            out Vector3 calibrationPoint)
        {
            calibrationPoint = Vector3.zero;

            if (weapon == null || ammo == null || distance <= 0)
            {
                return false;
            }

            Vector3[] data = weapon.CreateOpticCalibrationData(
                new[] { distance },
                ammo,
                weapon.SpeedFactor,
                0.001f);

            if (data == null || data.Length == 0)
            {
                return false;
            }

            calibrationPoint = data[0];
            return true;
        }

        private void RestoreAutoZero(ProceduralWeaponAnimation weaponAnimation = null)
        {
            bool restored = false;
            if (_autoZeroSight != null
                && _autoZeroScopeIndex >= 0
                && _autoZeroSight.OpticCalibrationPoints != null
                && _autoZeroScopeIndex < _autoZeroSight.OpticCalibrationPoints.Length
                && _autoZeroOriginalPoints != null)
            {
                _autoZeroSight.OpticCalibrationPoints[_autoZeroScopeIndex] = _autoZeroOriginalPoints;
                restored = true;
            }

            _autoZeroSight = null;
            _autoZeroScopeIndex = -1;
            _autoZeroOriginalPoints = null;
            _autoZeroLastDistance = -1;
            _autoZeroLastAmmo = null;

            if (restored)
            {
                weaponAnimation?.method_2();
            }
        }

        private void UpdateTrajectoryDebug(
            ProceduralWeaponAnimation weaponAnimation,
            AmmoTemplate ammo,
            float speedFactor)
        {
            if (!Plugin.AutoZeroDebugTrajectory.Value)
            {
                SetTrajectoryDebugVisible(false);
                return;
            }

            Transform fireport = weaponAnimation?.HandsContainer?.Fireport;
            if (fireport == null || ammo == null)
            {
                SetTrajectoryDebugVisible(false);
                return;
            }

            List<Vector3> points = BuildTrajectoryPoints(fireport, ammo, speedFactor);
            if (points.Count < 2)
            {
                SetTrajectoryDebugVisible(false);
                return;
            }

            EnsureTrajectoryLine();
            _autoZeroTrajectoryLine.positionCount = points.Count;
            _autoZeroTrajectoryLine.SetPositions(points.ToArray());
            _autoZeroTrajectoryLine.enabled = true;
        }

        private static List<Vector3> BuildTrajectoryPoints(
            Transform fireport,
            AmmoTemplate ammo,
            float speedFactor)
        {
            var points = new List<Vector3>();
            GClass3735 trajectoryInfo = null;

            try
            {
                float initialSpeed = ammo.InitialSpeed * speedFactor;
                Vector3 localOrigin = Vector3.zero;
                Vector3 localVelocity = Vector3.forward * initialSpeed;
                Quaternion localToFireport = Quaternion.Euler(90f, 0f, 0f);

                EftBulletClass.FormTrajectory(
                    localOrigin,
                    localVelocity,
                    ammo.BulletMassGram,
                    ammo.BulletDiameterMilimeters,
                    ammo.BallisticCoeficient,
                    out trajectoryInfo);

                float duration = Plugin.AutoZeroTrajectoryDuration.Value;
                for (float time = 0f; time <= duration; time += AutoZeroTrajectoryStep)
                {
                    EftBulletClass.PredictedTrajectoryCalculation(
                        out Vector3 localPosition,
                        out _,
                        trajectoryInfo,
                        time);
                    points.Add(fireport.position + fireport.TransformDirection(localToFireport * localPosition));
                }
            }
            finally
            {
                if (trajectoryInfo != null && Singleton<GameWorld>.Instantiated)
                {
                    Singleton<GameWorld>.Instance.TrajectoryCalculatorPool.Return(trajectoryInfo);
                }
            }

            return points;
        }

        private void EnsureTrajectoryLine()
        {
            if (_autoZeroTrajectoryLine != null)
            {
                return;
            }

            var lineObject = new GameObject("ScopeRangefinderAutoZeroTrajectory");
            lineObject.transform.SetParent(transform, false);
            _autoZeroTrajectoryLine = lineObject.AddComponent<LineRenderer>();
            _autoZeroTrajectoryLine.useWorldSpace = true;
            _autoZeroTrajectoryLine.positionCount = 0;
            _autoZeroTrajectoryLine.startWidth = AutoZeroLineWidth;
            _autoZeroTrajectoryLine.endWidth = AutoZeroLineWidth;
            _autoZeroTrajectoryLine.material = new Material(Shader.Find("GUI/Text Shader") ?? Shader.Find("Sprites/Default"));
            _autoZeroTrajectoryLine.startColor = new Color(0f, 1f, 0.25f, 0.85f);
            _autoZeroTrajectoryLine.endColor = new Color(0f, 1f, 0.25f, 0.25f);
            _autoZeroTrajectoryLine.enabled = false;
        }

        private void SetTrajectoryDebugVisible(bool visible)
        {
            if (_autoZeroTrajectoryLine != null)
            {
                _autoZeroTrajectoryLine.enabled = visible;
            }
        }
    }
}
