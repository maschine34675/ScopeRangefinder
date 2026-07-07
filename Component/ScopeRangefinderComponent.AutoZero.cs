using Comfort.Common;
using EFT;
using EFT.Animations;
using EFT.InventoryLogic;
using UnityEngine;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
        private const float AutoZeroTrajectoryStep = 0.025f;
        private const float AutoZeroTrajectoryMaxTime = 6f;
        private const float AutoZeroLineWidth = 0.015f;
        private const float AutoZeroWidthPerMeter = 0.0005f;

        private const float AutoZeroSnapSqrDistance = 4e-6f;

        private SightComponent _autoZeroSight;
        private int _autoZeroScopeIndex = -1;
        private int _autoZeroPointIndex = -1;
        private Vector3[] _autoZeroLiveArray;
        private Vector3[] _autoZeroOriginalPoints;
        private int _autoZeroLastDistance;
        private AmmoTemplate _autoZeroLastAmmo;
        private Vector3 _autoZeroTargetPoint;
        private Vector3 _autoZeroAppliedPoint;
        private Vector3 _autoZeroPointVelocity;
        private bool _autoZeroPointInitialized;
        private const int SpreadCircleSegments = 48;

        private LineRenderer _autoZeroTrajectoryLine;
        private Material _autoZeroTrajectoryMaterial;
        private Vector3[] _autoZeroTrajectoryBuffer;
        private float _autoZeroAppliedTrajectoryLength = -1f;
        private Color _autoZeroAppliedNearColor;
        private Color _autoZeroAppliedFarColor;
        private LineRenderer _autoZeroSpreadCircle;
        private Material _autoZeroSpreadCircleMaterial;
        private Vector3[] _autoZeroSpreadCircleBuffer;
        private Color _autoZeroAppliedSpreadColor;

        private void UpdateAutoZero(Player player, ProceduralWeaponAnimation weaponAnimation)
        {
            if (!Plugin.AutoZeroEnabled.Value)
            {
                RestoreAutoZero(weaponAnimation);
                SetTrajectoryPreviewVisible(false);
                return;
            }

            if (player?.HandsController is not Player.FirearmController firearmController
                || firearmController.Item is not Weapon weapon
                || weaponAnimation?.CurrentAimingMod == null)
            {
                RestoreAutoZero(weaponAnimation);
                SetTrajectoryPreviewVisible(false);
                return;
            }

            SightComponent sight = weaponAnimation.CurrentAimingMod;
            int scopeIndex = sight.SelectedScopeIndex;
            if (!TryGetMutableCalibrationPoints(sight, scopeIndex, out Vector3[] points, out int pointIndex))
            {
                RestoreAutoZero(weaponAnimation);
                SetTrajectoryPreviewVisible(false);
                return;
            }

            AmmoTemplate ammo = weapon.CurrentAmmoTemplate ?? weapon.Template?.DefAmmoTemplate;
            int distance = Mathf.Clamp(Mathf.RoundToInt(_lastMeasuredDistance), 1, Mathf.RoundToInt(Plugin.MaxDistance.Value));
            bool hasMeasurement = _lastRaycastHit && ammo != null;

            if (hasMeasurement)
            {
                UpdateTrajectoryPreview(weaponAnimation, weapon, ammo, distance);
            }
            else
            {
                SetTrajectoryPreviewVisible(false);
            }

            if (Plugin.AutoZeroMode.Value == AutoZeroMode.Continuous)
            {
                UpdateContinuousZero(weaponAnimation, weapon, sight, scopeIndex, points, pointIndex, ammo, distance, hasMeasurement);
            }
            else
            {
                UpdateHotkeyZero(player, weaponAnimation, weapon, sight, scopeIndex, points, pointIndex, ammo, distance, hasMeasurement);
            }
        }

        private void UpdateContinuousZero(
            ProceduralWeaponAnimation weaponAnimation,
            Weapon weapon,
            SightComponent sight,
            int scopeIndex,
            Vector3[] points,
            int pointIndex,
            AmmoTemplate ammo,
            int distance,
            bool hasMeasurement)
        {
            if (!hasMeasurement)
            {
                RestoreAutoZero(weaponAnimation);
                return;
            }

            EnsureAutoZeroBackup(sight, scopeIndex, points);

            bool targetChanged = _autoZeroLastDistance != distance || _autoZeroLastAmmo != ammo;
            if (targetChanged)
            {
                if (!TryCalculateCalibrationPoint(weapon, ammo, distance, out Vector3 calibrationPoint))
                {
                    RestoreAutoZero(weaponAnimation);
                    return;
                }

                _autoZeroTargetPoint = calibrationPoint;
                _autoZeroLastDistance = distance;
                _autoZeroLastAmmo = ammo;
            }

            _autoZeroPointIndex = pointIndex;
            ApplyCalibrationPointStep(sight, scopeIndex, points, pointIndex, weaponAnimation);
        }

        private void UpdateHotkeyZero(
            Player player,
            ProceduralWeaponAnimation weaponAnimation,
            Weapon weapon,
            SightComponent sight,
            int scopeIndex,
            Vector3[] points,
            int pointIndex,
            AmmoTemplate ammo,
            int distance,
            bool hasMeasurement)
        {
            bool applied = _autoZeroSight == sight && _autoZeroScopeIndex == scopeIndex;

            if (applied && pointIndex != _autoZeroPointIndex)
            {
                RestoreAutoZero(weaponAnimation);
                return;
            }

            if (Plugin.AutoZeroHotkey.Value.IsDown()
                && hasMeasurement
                && TryCalculateCalibrationPoint(weapon, ammo, distance, out Vector3 calibrationPoint))
            {
                EnsureAutoZeroBackup(sight, scopeIndex, points);

                if (!_autoZeroPointInitialized)
                {
                    _autoZeroAppliedPoint = points[pointIndex];
                    _autoZeroPointVelocity = Vector3.zero;
                    _autoZeroPointInitialized = true;
                }

                _autoZeroTargetPoint = calibrationPoint;
                _autoZeroLastDistance = distance;
                _autoZeroLastAmmo = ammo;
                _autoZeroPointIndex = pointIndex;
                player.ShowAmmoCountZeroingPanel($"{distance}m");
                applied = true;
            }

            if (!applied)
            {
                return;
            }

            ApplyCalibrationPointStep(sight, scopeIndex, points, pointIndex, weaponAnimation);
        }

        private void ApplyCalibrationPointStep(
            SightComponent sight,
            int scopeIndex,
            Vector3[] points,
            int pointIndex,
            ProceduralWeaponAnimation weaponAnimation)
        {
            if (points != _autoZeroLiveArray)
            {
                return;
            }

            Vector3 previous = _autoZeroAppliedPoint;
            bool firstApply = !_autoZeroPointInitialized;
            float transitionTime = Plugin.AutoZeroTransitionTime.Value;

            if (firstApply || transitionTime <= 0f)
            {
                _autoZeroAppliedPoint = _autoZeroTargetPoint;
                _autoZeroPointVelocity = Vector3.zero;
                _autoZeroPointInitialized = true;
            }
            else if (_autoZeroAppliedPoint != _autoZeroTargetPoint)
            {
                _autoZeroAppliedPoint = Vector3.SmoothDamp(
                    _autoZeroAppliedPoint,
                    _autoZeroTargetPoint,
                    ref _autoZeroPointVelocity,
                    transitionTime);

                if ((_autoZeroAppliedPoint - _autoZeroTargetPoint).sqrMagnitude < AutoZeroSnapSqrDistance)
                {
                    _autoZeroAppliedPoint = _autoZeroTargetPoint;
                    _autoZeroPointVelocity = Vector3.zero;
                }
            }

            if (!firstApply && previous == _autoZeroAppliedPoint)
            {
                return;
            }

            points[pointIndex] = _autoZeroAppliedPoint;
            sight.OpticCalibrationPoints[scopeIndex] = points;
            weaponAnimation.method_2();
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
            if (_autoZeroSight == sight && _autoZeroScopeIndex == scopeIndex && _autoZeroLiveArray == points)
            {
                return;
            }

            RestoreAutoZero();
            _autoZeroSight = sight;
            _autoZeroScopeIndex = scopeIndex;
            _autoZeroLiveArray = points;
            _autoZeroOriginalPoints = (Vector3[])points.Clone();
            _autoZeroLastDistance = -1;
            _autoZeroLastAmmo = null;
            _autoZeroPointIndex = -1;
            _autoZeroPointInitialized = false;
            _autoZeroPointVelocity = Vector3.zero;
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
                && _autoZeroOriginalPoints != null
                && ReferenceEquals(_autoZeroSight.OpticCalibrationPoints[_autoZeroScopeIndex], _autoZeroLiveArray))
            {
                _autoZeroSight.OpticCalibrationPoints[_autoZeroScopeIndex] = _autoZeroOriginalPoints;
                restored = true;
            }

            _autoZeroSight = null;
            _autoZeroScopeIndex = -1;
            _autoZeroPointIndex = -1;
            _autoZeroLiveArray = null;
            _autoZeroOriginalPoints = null;
            _autoZeroLastDistance = -1;
            _autoZeroLastAmmo = null;
            _autoZeroPointInitialized = false;
            _autoZeroPointVelocity = Vector3.zero;

            if (restored)
            {
                weaponAnimation?.method_2();
            }
        }

        internal static bool TryGetZeroingPanelText(out string panelText)
        {
            panelText = null;
            ScopeRangefinderComponent instance = _activeInstance;
            if (instance == null
                || !Plugin.AutoZeroEnabled.Value
                || instance._autoZeroSight == null)
            {
                return false;
            }

            if (Plugin.AutoZeroMode.Value == AutoZeroMode.Continuous)
            {
                panelText = "auto";
                return true;
            }

            if (instance._autoZeroLastDistance <= 0)
            {
                return false;
            }

            panelText = $"{instance._autoZeroLastDistance}m";
            return true;
        }

        private void UpdateTrajectoryPreview(
            ProceduralWeaponAnimation weaponAnimation,
            Weapon weapon,
            AmmoTemplate ammo,
            int targetDistance)
        {
            if (!Plugin.ShowTrajectoryPreview.Value)
            {
                SetTrajectoryPreviewVisible(false);
                return;
            }

            Transform fireport = weaponAnimation?.HandsContainer?.Fireport;
            if (fireport == null || weapon == null || ammo == null || targetDistance <= 0)
            {
                SetTrajectoryPreviewVisible(false);
                return;
            }

            int pointCount = BuildTrajectoryPoints(fireport, ammo, weapon.SpeedFactor, targetDistance, out float arcLength);
            if (pointCount < 2)
            {
                SetTrajectoryPreviewVisible(false);
                return;
            }

            EnsureTrajectoryLine();
            ApplyTrajectoryStyleIfChanged(arcLength);
            _autoZeroTrajectoryLine.positionCount = pointCount;
            _autoZeroTrajectoryLine.SetPositions(_autoZeroTrajectoryBuffer);
            _autoZeroTrajectoryLine.enabled = true;

            UpdateSpreadCircle(
                weapon,
                ammo,
                fireport.position,
                _autoZeroTrajectoryBuffer[pointCount - 1],
                targetDistance);
        }

        private void UpdateSpreadCircle(
            Weapon weapon,
            AmmoTemplate ammo,
            Vector3 muzzlePosition,
            Vector3 impactPoint,
            float targetDistance)
        {
            if (!Plugin.AutoZeroImpactSpreadCircle.Value)
            {
                SetSpreadCircleVisible(false);
                return;
            }

            float spreadTangent = CalculateCenterOfImpactAt100m(weapon, ammo) / 100f;
            if (spreadTangent * targetDistance <= 0.002f)
            {
                SetSpreadCircleVisible(false);
                return;
            }

            Vector3 axis = impactPoint - muzzlePosition;
            float impactDistance = axis.magnitude;
            if (impactDistance < 0.5f)
            {
                SetSpreadCircleVisible(false);
                return;
            }

            axis /= impactDistance;
            Vector3 right = Vector3.Cross(axis, Vector3.up);
            if (right.sqrMagnitude < 1e-6f)
            {
                right = Vector3.right;
            }

            right.Normalize();
            Vector3 circleUp = Vector3.Cross(right, axis);

            EnsureSpreadCircle();
            ApplySpreadCircleStyle(targetDistance, spreadTangent * impactDistance);

            Vector3 surfaceNormal = _lastHitNormal;
            bool projectOntoSurface = Mathf.Abs(Vector3.Dot(axis, surfaceNormal)) > 0.005f;
            float planeDistance = Vector3.Dot(impactPoint - muzzlePosition, surfaceNormal);
            float maxRayLength = impactDistance * 2f;

            for (int i = 0; i < SpreadCircleSegments; i++)
            {
                float angle = i * (2f * Mathf.PI / SpreadCircleSegments);
                Vector3 spreadDirection = Mathf.Cos(angle) * right + Mathf.Sin(angle) * circleUp;
                Vector3 rayDirection = (axis + spreadTangent * spreadDirection).normalized;

                float rayLength = impactDistance;
                if (projectOntoSurface)
                {
                    float denominator = Vector3.Dot(rayDirection, surfaceNormal);
                    rayLength = Mathf.Abs(denominator) > 1e-5f ? planeDistance / denominator : maxRayLength;
                    if (rayLength < 0f || rayLength > maxRayLength)
                    {
                        rayLength = maxRayLength;
                    }
                }

                _autoZeroSpreadCircleBuffer[i] = muzzlePosition + rayDirection * rayLength;
            }

            _autoZeroSpreadCircle.positionCount = SpreadCircleSegments;
            _autoZeroSpreadCircle.SetPositions(_autoZeroSpreadCircleBuffer);
            _autoZeroSpreadCircle.enabled = true;
        }

        private static float CalculateCenterOfImpactAt100m(Weapon weapon, AmmoTemplate ammo)
        {
            float centerOfImpact = weapon.GetTotalCenterOfImpact(false);
            float ammoFactor = ammo != null ? ammo.AmmoFactor : 1f;
            float barrelDeviation = weapon.GetBarrelDeviation();

            double buffSpread = weapon.GetItemComponent<BuffComponent>()?.WeaponSpread ?? 1.0;
            if (buffSpread < 1e-4)
            {
                buffSpread = 1.0;
            }

            float overheatMult = 1f;
            BackendConfigSettingsClass backend = Singleton<BackendConfigSettingsClass>.Instance;
            if (backend != null && weapon.MalfState != null)
            {
                float problemsStart = backend.Overheat.OverheatProblemsStart;
                if (weapon.MalfState.LastShotOverheat >= problemsStart)
                {
                    overheatMult = Mathf.Lerp(
                        1f,
                        backend.Overheat.MaxCOIIncreaseMult,
                        (weapon.MalfState.LastShotOverheat - problemsStart)
                            / (backend.Overheat.MaxOverheat - problemsStart));
                }
            }

            return centerOfImpact * ammoFactor * barrelDeviation * (float)buffSpread * overheatMult;
        }

        private void EnsureSpreadCircle()
        {
            if (_autoZeroSpreadCircleBuffer == null)
            {
                _autoZeroSpreadCircleBuffer = new Vector3[SpreadCircleSegments];
            }

            if (_autoZeroSpreadCircle != null)
            {
                return;
            }

            var circleObject = new GameObject("ScopeRangefinderSpreadCircle");
            circleObject.transform.SetParent(transform, false);
            _autoZeroSpreadCircle = circleObject.AddComponent<LineRenderer>();
            _autoZeroSpreadCircle.useWorldSpace = true;
            _autoZeroSpreadCircle.loop = true;
            _autoZeroSpreadCircle.positionCount = 0;
            Shader circleShader = Shader.Find("GUI/Text Shader") ?? Shader.Find("Sprites/Default");
            _autoZeroSpreadCircleMaterial = new Material(circleShader);
            _autoZeroSpreadCircleMaterial.renderQueue = 4500;
            _autoZeroSpreadCircle.material = _autoZeroSpreadCircleMaterial;
            _autoZeroSpreadCircle.enabled = false;
            _autoZeroAppliedSpreadColor = default;
        }

        private void ApplySpreadCircleStyle(float targetDistance, float ringRadius)
        {
            float width = Mathf.Min(
                Mathf.Max(0.002f, targetDistance * AutoZeroWidthPerMeter),
                ringRadius * 0.3f);
            _autoZeroSpreadCircle.startWidth = width;
            _autoZeroSpreadCircle.endWidth = width;

            Color color = Plugin.AutoZeroSpreadCircleColor.Value;
            if (color != _autoZeroAppliedSpreadColor)
            {
                _autoZeroAppliedSpreadColor = color;
                _autoZeroSpreadCircleMaterial.color = color;
            }
        }

        private void SetSpreadCircleVisible(bool visible)
        {
            if (_autoZeroSpreadCircle != null)
            {
                _autoZeroSpreadCircle.enabled = visible;
            }
        }

        private int BuildTrajectoryPoints(
            Transform fireport,
            AmmoTemplate ammo,
            float speedFactor,
            int targetDistance,
            out float arcLength)
        {
            arcLength = 0f;
            int maxPoints = Mathf.FloorToInt(AutoZeroTrajectoryMaxTime / AutoZeroTrajectoryStep) + 1;
            if (_autoZeroTrajectoryBuffer == null || _autoZeroTrajectoryBuffer.Length < maxPoints)
            {
                _autoZeroTrajectoryBuffer = new Vector3[maxPoints];
            }

            int pointCount = 0;
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

                for (int i = 0; i < maxPoints; i++)
                {
                    EftBulletClass.PredictedTrajectoryCalculation(
                        out Vector3 localPosition,
                        out _,
                        trajectoryInfo,
                        i * AutoZeroTrajectoryStep);
                    Vector3 worldPoint = fireport.position + fireport.TransformDirection(localToFireport * localPosition);
                    _autoZeroTrajectoryBuffer[pointCount] = worldPoint;
                    if (pointCount > 0)
                    {
                        arcLength += Vector3.Distance(_autoZeroTrajectoryBuffer[pointCount - 1], worldPoint);
                    }

                    pointCount++;

                    if (localPosition.magnitude >= targetDistance)
                    {
                        break;
                    }
                }
            }
            finally
            {
                if (trajectoryInfo != null && Singleton<GameWorld>.Instantiated)
                {
                    Singleton<GameWorld>.Instance.TrajectoryCalculatorPool.Return(trajectoryInfo);
                }
            }

            return pointCount;
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
            _autoZeroTrajectoryMaterial = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("GUI/Text Shader"));
            _autoZeroTrajectoryLine.material = _autoZeroTrajectoryMaterial;
            _autoZeroTrajectoryLine.enabled = false;
            _autoZeroAppliedTrajectoryLength = -1f;
        }

        private void ApplyTrajectoryStyleIfChanged(float arcLength)
        {
            Color nearColor = Plugin.AutoZeroTrajectoryNearColor.Value;
            Color farColor = Plugin.AutoZeroTrajectoryFarColor.Value;
            if (nearColor == _autoZeroAppliedNearColor
                && farColor == _autoZeroAppliedFarColor
                && Mathf.Abs(arcLength - _autoZeroAppliedTrajectoryLength) < 1f)
            {
                return;
            }

            _autoZeroAppliedNearColor = nearColor;
            _autoZeroAppliedFarColor = farColor;
            _autoZeroAppliedTrajectoryLength = arcLength;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(nearColor, 0f),
                    new GradientColorKey(farColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(nearColor.a, 0f),
                    new GradientAlphaKey(farColor.a, 1f)
                });
            _autoZeroTrajectoryLine.colorGradient = gradient;

            float farWidth = Mathf.Max(AutoZeroLineWidth, arcLength * AutoZeroWidthPerMeter);
            _autoZeroTrajectoryLine.widthCurve = AnimationCurve.Linear(0f, AutoZeroLineWidth, 1f, farWidth);
        }

        private void SetTrajectoryPreviewVisible(bool visible)
        {
            if (_autoZeroTrajectoryLine != null)
            {
                _autoZeroTrajectoryLine.enabled = visible;
            }

            if (!visible)
            {
                SetSpreadCircleVisible(false);
            }
        }

        private void DestroyTrajectoryVisualization()
        {
            if (_autoZeroTrajectoryLine != null)
            {
                Destroy(_autoZeroTrajectoryLine.gameObject);
                _autoZeroTrajectoryLine = null;
            }

            if (_autoZeroSpreadCircle != null)
            {
                Destroy(_autoZeroSpreadCircle.gameObject);
                _autoZeroSpreadCircle = null;
            }

            if (_autoZeroSpreadCircleMaterial != null)
            {
                Destroy(_autoZeroSpreadCircleMaterial);
                _autoZeroSpreadCircleMaterial = null;
            }

            if (_autoZeroTrajectoryMaterial != null)
            {
                Destroy(_autoZeroTrajectoryMaterial);
                _autoZeroTrajectoryMaterial = null;
            }

            _autoZeroTrajectoryBuffer = null;
            _autoZeroSpreadCircleBuffer = null;
            _autoZeroAppliedTrajectoryLength = -1f;
        }
    }
}
