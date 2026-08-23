using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using UnityEngine;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
        private const float BallisticsSampleStep = 0.005f;
        private const float BallisticsMaxFlightTime = 13f;
        private const float MilliradiansPerMinuteOfAngle = 0.2908882f;
        internal const string HoldLinePrefix = "HLD";
        internal const string DialLinePrefix = "DIA";

        private Weapon _activeWeapon;

        private struct BallisticsSolution
        {
            public bool Valid;
            public int MeasuredDistance;
            public float HoldMilliradians;
            public int BestDialDistance;
            public float BestDialResidualMilliradians;
        }
        private BallisticsSolution _ballisticsSolution;
        private AmmoTemplate _ballisticsAmmo;
        private float _ballisticsSpeedFactor;
        private int _ballisticsDistance = -1;
        private SightComponent _ballisticsSight;
        private int _ballisticsScopeIndex = -1;
        private int _ballisticsPointIndex = -1;
        private Vector3[] _ballisticsPointsArray;
        private Vector3 _ballisticsSightLine;
        private AmmoTemplate _impactAmmo;
        private float _impactSpeedFactor;
        private int _impactDistance = -1;
        private bool _impactValid;
        private Vector3 _impactPoint;

        private bool TryGetBallisticsSolution(out BallisticsSolution solution)
        {
            solution = default;
            if (!_lastRaycastHit)
            {
                return false;
            }

            SightComponent sight = _activeWeaponAnimation?.CurrentAimingMod;
            Weapon weapon = _activeWeapon;
            if (sight == null || weapon == null)
            {
                return false;
            }

            int scopeIndex = sight.SelectedScopeIndex;
            if (!TryGetMutableCalibrationPoints(sight, scopeIndex, out Vector3[] points, out int pointIndex))
            {
                return false;
            }

            AmmoTemplate ammo = weapon.CurrentAmmoTemplate ?? weapon.Template?.DefAmmoTemplate;
            if (ammo == null)
            {
                return false;
            }

            int distance = Mathf.Clamp(
                Mathf.RoundToInt(_lastMeasuredDistance), 1, Mathf.RoundToInt(Plugin.MaxDistance.Value));
            float speedFactor = weapon.SpeedFactor;
            Vector3 sightLine = points[pointIndex];

            if (_ballisticsSolution.Valid
                && _ballisticsAmmo == ammo
                && _ballisticsSpeedFactor == speedFactor
                && _ballisticsDistance == distance
                && _ballisticsSight == sight
                && _ballisticsScopeIndex == scopeIndex
                && _ballisticsPointIndex == pointIndex
                && ReferenceEquals(_ballisticsPointsArray, points)
                && _ballisticsSightLine == sightLine)
            {
                solution = _ballisticsSolution;
                return true;
            }

            if (_impactAmmo != ammo || _impactSpeedFactor != speedFactor || _impactDistance != distance)
            {
                _impactValid = TryComputeImpactPoint(ammo, speedFactor, distance, out _impactPoint);
                _impactAmmo = ammo;
                _impactSpeedFactor = speedFactor;
                _impactDistance = distance;
            }

            if (!_impactValid)
            {
                return false;
            }

            var result = new BallisticsSolution
            {
                Valid = true,
                MeasuredDistance = distance,
                HoldMilliradians = ComputeHoldMilliradians(sightLine, _impactPoint),
                BestDialDistance = -1
            };

            int[] steps = sight.GetScopeCalibrationDistances(scopeIndex);
            if (steps != null && steps.Length > 0 && steps.Length == points.Length)
            {
                float bestAbs = float.MaxValue;
                for (int k = 0; k < steps.Length; k++)
                {
                    float residual = ComputeHoldMilliradians(points[k], _impactPoint);
                    if (Mathf.Abs(residual) < bestAbs)
                    {
                        bestAbs = Mathf.Abs(residual);
                        result.BestDialDistance = steps[k];
                        result.BestDialResidualMilliradians = residual;
                    }
                }
            }

            _ballisticsSolution = result;
            _ballisticsAmmo = ammo;
            _ballisticsSpeedFactor = speedFactor;
            _ballisticsDistance = distance;
            _ballisticsSight = sight;
            _ballisticsScopeIndex = scopeIndex;
            _ballisticsPointIndex = pointIndex;
            _ballisticsPointsArray = points;
            _ballisticsSightLine = sightLine;

            solution = result;
            return true;
        }
        private static float ComputeHoldMilliradians(Vector3 sightLinePoint, Vector3 impactPoint)
        {
            return 1000f * Mathf.Atan2(
                sightLinePoint.y * impactPoint.z - impactPoint.y * sightLinePoint.z,
                sightLinePoint.z * impactPoint.z + sightLinePoint.y * impactPoint.y);
        }
        private bool TryComputeImpactPoint(
            AmmoTemplate ammo, float speedFactor, int targetDistance, out Vector3 impactPoint)
        {
            impactPoint = Vector3.zero;
            if (ammo == null || targetDistance <= 0 || !Singleton<GameWorld>.Instantiated)
            {
                return false;
            }

            TrajectoryCalculator trajectoryInfo = null;
            try
            {
                float initialSpeed = ammo.InitialSpeed * speedFactor;
                Shot.FormTrajectory(
                    Vector3.zero,
                    Vector3.forward * initialSpeed,
                    ammo.BulletMassGram,
                    ammo.BulletDiameterMilimeters,
                    ammo.BallisticCoeficient,
                    out trajectoryInfo);

                float targetSqr = (float)targetDistance * targetDistance;
                Vector3 previous = Vector3.zero;
                int maxSteps = Mathf.FloorToInt(BallisticsMaxFlightTime / BallisticsSampleStep) + 1;
                for (int i = 1; i <= maxSteps; i++)
                {
                    Shot.PredictedTrajectoryCalculation(
                        out Vector3 position,
                        out _,
                        trajectoryInfo,
                        i * BallisticsSampleStep);
                    if (position.sqrMagnitude >= targetSqr)
                    {
                        float previousMagnitude = previous.magnitude;
                        float magnitude = position.magnitude;
                        float t = magnitude > previousMagnitude
                            ? Mathf.Clamp01((targetDistance - previousMagnitude) / (magnitude - previousMagnitude))
                            : 1f;
                        impactPoint = Vector3.Lerp(previous, position, t);
                        return true;
                    }

                    previous = position;
                }
                return false;
            }
            finally
            {
                if (trajectoryInfo != null && Singleton<GameWorld>.Instantiated)
                {
                    Singleton<GameWorld>.Instance.TrajectoryCalculatorPool.Return(trajectoryInfo);
                }
            }
        }
        private string BuildBallisticsLineText()
        {
            BallisticsLineMode mode = ActiveStyle.BallisticsLine;
            if (!TryGetBallisticsSolution(out BallisticsSolution solution))
            {
                return ComposeReadoutLine(
                    mode == BallisticsLineMode.Dial ? DialLinePrefix : HoldLinePrefix,
                    ActiveStyle.NoDistanceText);
            }
            bool dialUnavailable = solution.BestDialDistance < 0
                || (Plugin.AutoZeroEnabled.Value && IsAutoZeroEffective(_activeWeaponAnimation?.CurrentAimingMod));
            if (mode == BallisticsLineMode.Dial && dialUnavailable)
            {
                return ComposeReadoutLine(
                    DialLinePrefix, FormatHoldValue(solution.HoldMilliradians, solution.MeasuredDistance));
            }

            if (mode == BallisticsLineMode.Dial)
            {
                string text = ComposeReadoutLine(
                    DialLinePrefix, FormatDistanceValue(solution.BestDialDistance, withSuffix: false));
                if (Mathf.Abs(solution.BestDialResidualMilliradians) >= DialResidualThresholdMilliradians)
                {
                    text += FormatDialResidual(solution.BestDialResidualMilliradians, solution.MeasuredDistance);
                }

                return text;
            }

            return ComposeReadoutLine(
                HoldLinePrefix, FormatHoldValue(solution.HoldMilliradians, solution.MeasuredDistance));
        }
        private const float DialResidualThresholdMilliradians = 0.15f;
        private static string FormatDialResidual(float milliradians, int distanceMeters)
        {
            switch (ActiveStyle.BallisticsHoldUnit)
            {
                case HoldUnit.MinutesOfAngle:
                    return (milliradians / MilliradiansPerMinuteOfAngle).ToString("+0.0;-0.0");
                case HoldUnit.Centimeters:
                    float centimeters = Mathf.Tan(milliradians / 1000f) * distanceMeters * 100f;
                    return Mathf.RoundToInt(centimeters).ToString("+0;-0");
                default:
                    return milliradians.ToString("+0.0;-0.0");
            }
        }

        private static string FormatHoldValue(float milliradians, int distanceMeters)
        {
            switch (ActiveStyle.BallisticsHoldUnit)
            {
                case HoldUnit.MinutesOfAngle:
                    return (milliradians / MilliradiansPerMinuteOfAngle).ToString("+0.0;-0.0") + "moa";
                case HoldUnit.Centimeters:
                    float centimeters = Mathf.Tan(milliradians / 1000f) * distanceMeters * 100f;
                    return Mathf.RoundToInt(centimeters).ToString("+0;-0") + "cm";
                default:
                    return milliradians.ToString("+0.0;-0.0") + "mil";
            }
        }
    }
}
