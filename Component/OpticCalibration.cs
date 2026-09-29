using System;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using UnityEngine;

namespace ScopeRangefinder
{
    internal static class OpticCalibration
    {
        private const float CalibrationTimeStep = 0.001f;
        internal static Vector3[] CalculatePoints(int[] ascendingDistances, AmmoTemplate ammo, float speedFactor)
        {
            if (ascendingDistances == null || ammo == null || !Singleton<GameWorld>.Instantiated)
            {
                return null;
            }

            var points = new Vector3[ascendingDistances.Length];
            TrajectoryCalculator trajectory = null;
            try
            {
                Shot.FormTrajectory(
                    Vector3.zero,
                    Vector3.forward * (ammo.InitialSpeed * speedFactor),
                    ammo.BulletMassGram,
                    ammo.BulletDiameterMilimeters,
                    ammo.BallisticCoeficient,
                    out trajectory);

                Vector3 position = Vector3.zero;
                float reachedSqr = 0f;
                float time = 0f;
                for (int i = 0; i < ascendingDistances.Length; i++)
                {
                    float targetSqr = ascendingDistances[i] * ascendingDistances[i];
                    while (reachedSqr < targetSqr)
                    {
                        Shot.PredictedTrajectoryCalculation(out position, out _, trajectory, time);
                        reachedSqr = position.sqrMagnitude;
                        time += CalibrationTimeStep;
                    }

                    points[i] = position;
                }

                return points;
            }
            finally
            {
                if (trajectory != null && Singleton<GameWorld>.Instantiated)
                {
                    Singleton<GameWorld>.Instance.TrajectoryCalculatorPool.Return(trajectory);
                }
            }
        }
        internal static bool TryCalculateScope(Weapon weapon, SightComponent sight, int scopeIndex, AmmoTemplate ammo)
        {
            int[] distances = sight.GetScopeCalibrationDistances(scopeIndex);
            Vector3[][] allPoints = sight.OpticCalibrationPoints;
            if (distances == null || distances.Length == 0 || ammo == null
                || allPoints == null || scopeIndex < 0 || scopeIndex >= allPoints.Length)
            {
                return false;
            }

            int[] ascending = distances.Distinct().OrderBy(distance => distance).ToArray();
            Vector3[] calculated = CalculatePoints(ascending, ammo, weapon.SpeedFactor);
            if (calculated == null)
            {
                return false;
            }

            var points = new Vector3[distances.Length];
            for (int i = 0; i < distances.Length; i++)
            {
                points[i] = calculated[Array.IndexOf(ascending, distances[i])];
            }

            allPoints[scopeIndex] = points;
            return true;
        }
    }
}
