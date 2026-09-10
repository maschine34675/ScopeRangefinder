using EFT.CameraControl;
using System.Collections.Generic;
using UnityEngine;

namespace ScopeRangefinder
{
    internal partial class ScopeRangefinderComponent
    {
        private const float ReadoutDragHitPadding = 10f;
        private const float ReadoutDragMinimumHitWidth = 56f;
        private const float ReadoutDragMinimumHitHeight = 36f;
        private const float ReadoutDragOutlineThickness = 2f;
        private const float OpticImageUvExpansion = 10f;
        private const float LensTextureFitTolerance = 0.05f;
        private const float MagnifiedLensImageZoneRatio = 1.4609f;
        private static readonly int OpticLensScalesId = Shader.PropertyToID("_Scales");
        private static readonly int OpticLensShiftsId = Shader.PropertyToID("_Shifts");
        private static readonly int OpticLensShiftDirectionId = Shader.PropertyToID("_ShiftDirection");
        private static readonly Dictionary<Mesh, LensTextureMapping> LensTextureMappings =
            new Dictionary<Mesh, LensTextureMapping>();

        private bool _readoutDragActive;
        private bool _readoutMouseOver;
        private bool _readoutRectValid;
        private Rect _readoutScreenRect;
        private Vector2 _readoutDragLastMouse;
        private string _readoutDragKey;
        private Vector2 _readoutBasisX;
        private Vector2 _readoutBasisY;
        private bool _opticBasisUnavailableLogged;
        private int _loggedOpticBasisMeshId;
        private readonly Vector3[] _readoutCornerBuffer = new Vector3[4];
        internal static bool IsMouseOverReadoutLive()
        {
            ScopeRangefinderComponent instance = _activeInstance;
            if (instance == null || !instance._layoutEditorVisible || !instance._readoutRectValid)
            {
                return false;
            }

            return instance._readoutDragActive
                || instance._readoutScreenRect.Contains((Vector2)Input.mousePosition);
        }
        private void HandleReadoutDrag()
        {
            if (!_layoutEditorVisible)
            {
                CancelReadoutDrag();
                return;
            }

            _readoutRectValid = TryGetReadoutScreenRect(out _readoutScreenRect);
            Vector2 mouse = Input.mousePosition;
            bool overEditorWindow = _layoutEditorRect.height <= 0f
                || _layoutEditorRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
            _readoutMouseOver = _readoutRectValid
                && !overEditorWindow
                && _readoutScreenRect.Contains(mouse);

            if (_readoutDragActive)
            {
                if (!Input.GetMouseButton(0)
                    || !_readoutRectValid
                    || _currentLayoutKey != _readoutDragKey)
                {
                    bool completed = _readoutRectValid && _currentLayoutKey == _readoutDragKey;
                    _readoutDragActive = false;
                    _readoutDragKey = null;
                    if (completed)
                    {
                        _editorStatus = "Position updated - Save Scope keeps it";
                    }

                    return;
                }

                Vector2 delta = mouse - _readoutDragLastMouse;
                _readoutDragLastMouse = mouse;
                if (delta != Vector2.zero)
                {
                    ApplyReadoutDragDelta(delta);
                }

                return;
            }

            if (_readoutMouseOver
                && Input.GetMouseButtonDown(0)
                && !string.IsNullOrEmpty(_currentLayoutKey)
                && _currentLayoutKey == _layoutEditorKey)
            {
                _readoutDragActive = true;
                _readoutDragKey = _currentLayoutKey;
                _readoutDragLastMouse = mouse;
            }
        }

        private void CancelReadoutDrag()
        {
            _readoutDragActive = false;
            _readoutDragKey = null;
            _readoutMouseOver = false;
            _readoutRectValid = false;
        }
        private void ApplyReadoutDragDelta(Vector2 deltaPixels)
        {
            if (_usingMainCameraScope)
            {
                float canvasScale = _canvas != null ? _canvas.scaleFactor : 0f;
                if (canvasScale <= 0f)
                {
                    return;
                }

                _editorOffsetX += deltaPixels.x / canvasScale / 1920f;
                _editorOffsetY += deltaPixels.y / canvasScale / 1080f;
                return;
            }
            float determinant = _readoutBasisX.x * _readoutBasisY.y - _readoutBasisY.x * _readoutBasisX.y;
            if (Mathf.Abs(determinant) < 1e-6f)
            {
                return;
            }

            float deltaNdcX = (deltaPixels.x * _readoutBasisY.y - deltaPixels.y * _readoutBasisY.x) / determinant;
            float deltaNdcY = (deltaPixels.y * _readoutBasisX.x - deltaPixels.x * _readoutBasisX.y) / determinant;
            if (float.IsNaN(deltaNdcX) || float.IsNaN(deltaNdcY)
                || float.IsInfinity(deltaNdcX) || float.IsInfinity(deltaNdcY))
            {
                return;
            }
            _editorOffsetX += deltaNdcX * 0.5f;
            _editorOffsetY += deltaNdcY * 0.5f;
        }
        private bool TryGetReadoutScreenRect(out Rect rect)
        {
            rect = default;
            if (_usingMainCameraScope)
            {
                if (!_overlayDisplayVisible || _panelRect == null)
                {
                    return false;
                }
                _panelRect.GetWorldCorners(_readoutCornerBuffer);
                float minX = _readoutCornerBuffer[0].x;
                float minY = _readoutCornerBuffer[0].y;
                float maxX = minX;
                float maxY = minY;
                for (int i = 1; i < _readoutCornerBuffer.Length; i++)
                {
                    minX = Mathf.Min(minX, _readoutCornerBuffer[i].x);
                    minY = Mathf.Min(minY, _readoutCornerBuffer[i].y);
                    maxX = Mathf.Max(maxX, _readoutCornerBuffer[i].x);
                    maxY = Mathf.Max(maxY, _readoutCornerBuffer[i].y);
                }

                rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            }
            else
            {
                if (!_opticDisplayVisible
                    || _reticleReadoutRoot == null
                    || !_reticleReadoutRoot.activeSelf)
                {
                    return false;
                }

                Camera scopeCamera = _activeScopeCamera;
                if (scopeCamera == null)
                {
                    return false;
                }
                float depth = ResolveReticleReadoutDepth(scopeCamera);
                float halfHeight = depth * Mathf.Tan(scopeCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float halfWidth = halfHeight * scopeCamera.aspect;
                if (halfWidth <= 0f || halfHeight <= 0f)
                {
                    return false;
                }
                if (!TryGetOpticImageScreenBasis(
                        scopeCamera,
                        out Vector2 imageCenter,
                        out Vector2 imageAxisX,
                        out Vector2 imageAxisY))
                {
                    return false;
                }

                float uiScale = float.IsNaN(_appliedLayoutUiScale) ? ScopeCanvasDefaultUiScale : _appliedLayoutUiScale;
                float meshScale = ReadoutBaseScale * uiScale * CalculateReadoutZoomCompensation(scopeCamera, depth);
                float offsetX = float.IsNaN(_appliedLayoutOffsetX) ? 0f : _appliedLayoutOffsetX;
                float offsetY = float.IsNaN(_appliedLayoutOffsetY) ? 0f : _appliedLayoutOffsetY;
                GetReadoutBlockEnvelope(out Vector2 blockSize, out Vector2 blockCenter);
                Vector2 anchorShift = AnchorShiftLocal(_appliedLayoutAnchor);

                float viewX = offsetX * 2f * halfWidth + (anchorShift.x + blockCenter.x) * meshScale;
                float viewY = offsetY * 2f * halfHeight + (anchorShift.y + blockCenter.y) * meshScale;
                float centerNdcX = viewX / halfWidth;
                float centerNdcY = viewY / halfHeight;
                float halfNdcX = blockSize.x * meshScale / (2f * halfWidth);
                float halfNdcY = blockSize.y * meshScale / (2f * halfHeight);
                Vector2 cornerX = imageAxisX * halfNdcX;
                Vector2 cornerY = imageAxisY * halfNdcY;
                Vector2 blockOnScreen = imageCenter + imageAxisX * centerNdcX + imageAxisY * centerNdcY;
                Vector2 spread = new Vector2(
                    Mathf.Abs(cornerX.x) + Mathf.Abs(cornerY.x),
                    Mathf.Abs(cornerX.y) + Mathf.Abs(cornerY.y));
                rect = new Rect(
                    blockOnScreen.x - spread.x,
                    blockOnScreen.y - spread.y,
                    spread.x * 2f,
                    spread.y * 2f);
            }

            float growX = Mathf.Max(ReadoutDragHitPadding * 2f, ReadoutDragMinimumHitWidth - rect.width);
            float growY = Mathf.Max(ReadoutDragHitPadding * 2f, ReadoutDragMinimumHitHeight - rect.height);
            rect.xMin -= growX * 0.5f;
            rect.xMax += growX * 0.5f;
            rect.yMin -= growY * 0.5f;
            rect.yMax += growY * 0.5f;
            return true;
        }
        private bool TryGetOpticImageScreenBasis(
            Camera scopeCamera,
            out Vector2 center,
            out Vector2 axisX,
            out Vector2 axisY)
        {
            center = Vector2.zero;
            axisX = Vector2.zero;
            axisY = Vector2.zero;

            Renderer lens = _activeOpticSight != null ? _activeOpticSight.LensRenderer : null;
            Camera mainCamera = CameraManager.Instance?.Camera;
            if (lens == null || mainCamera == null || !lens.enabled)
            {
                return false;
            }

            Mesh lensMesh = lens.GetComponent<MeshFilter>()?.sharedMesh;
            Material lensMaterial = lens.sharedMaterial;
            if (lensMesh == null
                || lensMaterial == null
                || !lensMaterial.HasProperty(OpticLensScalesId)
                || !lensMaterial.HasProperty(OpticLensShiftsId)
                || !lensMaterial.HasProperty(OpticLensShiftDirectionId))
            {
                LogOpticBasisUnavailableOnce();
                return false;
            }

            float imageScale = lensMaterial.GetVector(OpticLensScalesId).x;
            float shiftAmount = lensMaterial.GetVector(OpticLensShiftsId).x;
            Vector3 shiftDirection = (Vector3)lensMaterial.GetVector(OpticLensShiftDirectionId);
            if (Mathf.Abs(imageScale) < 1e-4f)
            {
                return false;
            }
            bool fromRealUvs = TryGetLensTextureMapping(
                lensMesh,
                out Vector3 localCenter,
                out Vector3 localFirstStep,
                out Vector3 localSecondStep);
            if (!fromRealUvs
                && !TryEstimateLensTextureMapping(
                    lensMesh,
                    out localCenter,
                    out localFirstStep,
                    out localSecondStep))
            {
                return false;
            }
            Matrix4x4 lensToWorld = lens.transform.localToWorldMatrix;
            Vector3 referenceColumn = lensToWorld.GetColumn(0);
            Vector3 rescaledColumn = lensToWorld.GetColumn(1);
            if (rescaledColumn.sqrMagnitude > 1e-12f)
            {
                lensToWorld.SetColumn(1, rescaledColumn.normalized * referenceColumn.magnitude);
            }

            Vector3 worldCenter = lensToWorld.MultiplyPoint3x4(
                localCenter * imageScale + shiftDirection * shiftAmount);
            Vector3 worldFirst = lensToWorld.MultiplyVector(localFirstStep * imageScale);
            Vector3 worldSecond = lensToWorld.MultiplyVector(localSecondStep * imageScale);
            if (!fromRealUvs)
            {
                Transform scopeTransform = scopeCamera.transform;
                Vector3 cameraRight = scopeTransform.right;
                Vector3 cameraUp = scopeTransform.up;
                if (Mathf.Abs(Vector3.Dot(worldFirst.normalized, cameraRight))
                    < Mathf.Abs(Vector3.Dot(worldSecond.normalized, cameraRight)))
                {
                    (worldFirst, worldSecond) = (worldSecond, worldFirst);
                }

                if (Vector3.Dot(worldFirst, cameraRight) < 0f)
                {
                    worldFirst = -worldFirst;
                }

                if (Vector3.Dot(worldSecond, cameraUp) < 0f)
                {
                    worldSecond = -worldSecond;
                }
            }

            Vector3 screenCenter = mainCamera.WorldToScreenPoint(worldCenter);
            if (screenCenter.z <= 0f)
            {
                return false;
            }

            Vector3 screenFirst = mainCamera.WorldToScreenPoint(worldCenter + worldFirst);
            Vector3 screenSecond = mainCamera.WorldToScreenPoint(worldCenter + worldSecond);
            if (screenFirst.z <= 0f || screenSecond.z <= 0f)
            {
                return false;
            }
            float pixelToScreenX = mainCamera.pixelWidth > 0 ? Screen.width / (float)mainCamera.pixelWidth : 1f;
            float pixelToScreenY = mainCamera.pixelHeight > 0 ? Screen.height / (float)mainCamera.pixelHeight : 1f;
            center = new Vector2(screenCenter.x * pixelToScreenX, screenCenter.y * pixelToScreenY);
            axisX = new Vector2(screenFirst.x * pixelToScreenX, screenFirst.y * pixelToScreenY) - center;
            axisY = new Vector2(screenSecond.x * pixelToScreenX, screenSecond.y * pixelToScreenY) - center;
            if (axisX.sqrMagnitude < 1e-6f || axisY.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            _readoutBasisX = axisX;
            _readoutBasisY = axisY;
            LogOpticBasisOnce(lensMesh, fromRealUvs, imageScale, shiftAmount, mainCamera, worldCenter, axisX, axisY);
            return true;
        }
        private void LogOpticBasisOnce(
            Mesh lensMesh,
            bool fromRealUvs,
            float imageScale,
            float shiftAmount,
            Camera mainCamera,
            Vector3 worldCenter,
            Vector2 basisX,
            Vector2 basisY)
        {
            int meshId = lensMesh.GetInstanceID();
            if (!Plugin.LogScopeKeys.Value || meshId == _loggedOpticBasisMeshId)
            {
                return;
            }

            _loggedOpticBasisMeshId = meshId;
            Plugin.LogSource?.LogInfo(
                $"Optic image basis for '{lensMesh.name}': "
                + $"{(fromRealUvs ? "mesh texture coordinates" : "estimated from bounds")}"
                + $" (readable={lensMesh.isReadable}), extents={lensMesh.bounds.extents.ToString("F4")}, "
                + $"scale={imageScale.ToString("F2")}, shift={shiftAmount.ToString("F3")}, "
                + $"distance={Vector3.Distance(mainCamera.transform.position, worldCenter).ToString("F3")}m, "
                + $"screen units per view={basisX.magnitude.ToString("F1")}x{basisY.magnitude.ToString("F1")}px.");
        }
        private static bool TryGetLensTextureMapping(
            Mesh mesh,
            out Vector3 center,
            out Vector3 firstStep,
            out Vector3 secondStep)
        {
            center = Vector3.zero;
            firstStep = Vector3.zero;
            secondStep = Vector3.zero;
            if (mesh == null)
            {
                return false;
            }

            if (LensTextureMappings.TryGetValue(mesh, out LensTextureMapping cached))
            {
                center = cached.Center;
                firstStep = cached.FirstStep;
                secondStep = cached.SecondStep;
                return cached.IsValid;
            }

            bool resolved = TryFitLensTextureMapping(mesh, out center, out firstStep, out secondStep);
            LensTextureMappings[mesh] = new LensTextureMapping(resolved, center, firstStep, secondStep);
            return resolved;
        }

        private static bool TryFitLensTextureMapping(
            Mesh mesh,
            out Vector3 center,
            out Vector3 firstStep,
            out Vector3 secondStep)
        {
            center = Vector3.zero;
            firstStep = Vector3.zero;
            secondStep = Vector3.zero;
            if (!mesh.isReadable)
            {
                return false;
            }

            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            if (vertices == null || uvs == null || vertices.Length < 3 || uvs.Length < vertices.Length)
            {
                return false;
            }

            Vector2 meanUv = Vector2.zero;
            Vector3 meanPosition = Vector3.zero;
            for (int i = 0; i < vertices.Length; i++)
            {
                meanUv += uvs[i];
                meanPosition += vertices[i];
            }

            meanUv /= vertices.Length;
            meanPosition /= vertices.Length;

            float spreadUu = 0f;
            float spreadUv = 0f;
            float spreadVv = 0f;
            Vector3 mixedU = Vector3.zero;
            Vector3 mixedV = Vector3.zero;
            for (int i = 0; i < vertices.Length; i++)
            {
                float u = uvs[i].x - meanUv.x;
                float v = uvs[i].y - meanUv.y;
                Vector3 position = vertices[i] - meanPosition;
                spreadUu += u * u;
                spreadUv += u * v;
                spreadVv += v * v;
                mixedU += position * u;
                mixedV += position * v;
            }

            float determinant = spreadUu * spreadVv - spreadUv * spreadUv;
            if (determinant <= 1e-6f * spreadUu * spreadVv)
            {
                return false;
            }

            Vector3 perU = (mixedU * spreadVv - mixedV * spreadUv) / determinant;
            Vector3 perV = (mixedV * spreadUu - mixedU * spreadUv) / determinant;
            Vector3 origin = meanPosition - perU * meanUv.x - perV * meanUv.y;
            float tolerance = LensTextureFitTolerance * mesh.bounds.extents.magnitude;
            float toleranceSquared = tolerance * tolerance;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 predicted = origin + perU * uvs[i].x + perV * uvs[i].y;
                if ((predicted - vertices[i]).sqrMagnitude > toleranceSquared)
                {
                    return false;
                }
            }
            float perNdc = 0.5f / OpticImageUvExpansion;
            center = origin + (perU + perV) * 0.5f;
            firstStep = perU * perNdc;
            secondStep = perV * perNdc;
            return firstStep.sqrMagnitude > 0f && secondStep.sqrMagnitude > 0f;
        }
        private static bool TryEstimateLensTextureMapping(
            Mesh mesh,
            out Vector3 center,
            out Vector3 firstStep,
            out Vector3 secondStep)
        {
            center = Vector3.zero;
            firstStep = Vector3.zero;
            secondStep = Vector3.zero;
            if (mesh == null)
            {
                return false;
            }
            Bounds localBounds = mesh.bounds;
            Vector3 extents = localBounds.extents;
            int normalAxis = 0;
            if (extents.y < extents[normalAxis])
            {
                normalAxis = 1;
            }

            if (extents.z < extents[normalAxis])
            {
                normalAxis = 2;
            }

            int firstAxis = (normalAxis + 1) % 3;
            int secondAxis = (normalAxis + 2) % 3;
            if (extents[firstAxis] <= 0f || extents[secondAxis] <= 0f)
            {
                return false;
            }

            center = localBounds.center;
            firstStep[firstAxis] = extents[firstAxis] / OpticImageUvExpansion / MagnifiedLensImageZoneRatio;
            secondStep[secondAxis] = extents[secondAxis] / OpticImageUvExpansion / MagnifiedLensImageZoneRatio;
            return true;
        }

        private readonly struct LensTextureMapping
        {
            internal LensTextureMapping(bool isValid, Vector3 center, Vector3 firstStep, Vector3 secondStep)
            {
                IsValid = isValid;
                Center = center;
                FirstStep = firstStep;
                SecondStep = secondStep;
            }

            internal bool IsValid { get; }

            internal Vector3 Center { get; }

            internal Vector3 FirstStep { get; }

            internal Vector3 SecondStep { get; }
        }

        private void LogOpticBasisUnavailableOnce()
        {
            if (_opticBasisUnavailableLogged)
            {
                return;
            }

            _opticBasisUnavailableLogged = true;
            Plugin.LogSource?.LogDebug(
                "Dragging the readout is unavailable for this sight: its lens renderer does not "
                + "expose the optic image material, so the on-screen position cannot be resolved. "
                + "The editor's offset controls are unaffected.");
        }
        private void DrawReadoutDragHighlight()
        {
            if (!_readoutRectValid || Event.current.type != EventType.Repaint)
            {
                return;
            }
            Rect gui = new Rect(
                _readoutScreenRect.x,
                Screen.height - _readoutScreenRect.yMax,
                _readoutScreenRect.width,
                _readoutScreenRect.height);

            Color color = _readoutDragActive
                ? new Color(0.45f, 1f, 0.55f, 0.9f)
                : _readoutMouseOver
                    ? new Color(1f, 1f, 1f, 0.75f)
                    : new Color(1f, 1f, 1f, 0.22f);

            Color previous = GUI.color;
            GUI.color = color;
            float t = ReadoutDragOutlineThickness;
            GUI.DrawTexture(new Rect(gui.x, gui.y, gui.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(gui.x, gui.yMax - t, gui.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(gui.x, gui.y + t, t, gui.height - 2f * t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(gui.xMax - t, gui.y + t, t, gui.height - 2f * t), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
