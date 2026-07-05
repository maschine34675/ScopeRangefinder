using UnityEngine;
using UnityEngine.Rendering;

namespace ScopeRangefinder
{
    internal sealed class OpticReadoutCommandBuffer : MonoBehaviour
    {
        private const CameraEvent ReadoutCameraEvent = CameraEvent.AfterImageEffects;

        private CommandBuffer _commandBuffer;
        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _commandBuffer = new CommandBuffer
            {
                name = "Scope Readout Buffer"
            };
            _camera.RemoveCommandBuffer(ReadoutCameraEvent, _commandBuffer);
            _camera.AddCommandBuffer(ReadoutCameraEvent, _commandBuffer);
        }

        private void OnPreCull()
        {
            if (_commandBuffer == null || _camera == null)
            {
                return;
            }

            _commandBuffer.Clear();
            ScopeRangefinderComponent.PopulateReticleReadoutCommandBuffer(_commandBuffer, _camera);
        }

        private void OnDestroy()
        {
            if (_commandBuffer != null && _camera != null)
            {
                _camera.RemoveCommandBuffer(ReadoutCameraEvent, _commandBuffer);
                _commandBuffer.Clear();
                _commandBuffer = null;
            }
        }
    }
}
