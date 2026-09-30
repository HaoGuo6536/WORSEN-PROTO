// ============================================================================
// FogSpikeProfile.cs
// ============================================================================
// PURPOSE:
//   Holds repeatable synthetic scene and measurement controls for the fog spike.
//   These are fixture settings, not collapse rules or shipping performance evidence.
// ARCHITECTURAL ROLE:
//   Content SO (§4b) · Presentation · Fog.
// KEY RESPONSIBILITIES:
//   - Describe room geometry, camera pacing and the declared measurement window.
// DEPENDENCIES:
//   - UnityEngine serialized values only.
// USAGE NOTES:
//   Mirrored asset is created by FogSpikeSetup and reused without changing tuning.
//   Budgets are fixed acceptance criteria: 1.5 ms GPU at 1920x1080, .3 ms upload CPU.
// ============================================================================
using UnityEngine;

namespace Worsen.Presentation.Fog
{
    [CreateAssetMenu(menuName = "Worsen/Fog/Spike Profile")]
    public sealed class FogSpikeProfile : ScriptableObject
    {
        [SerializeField, Min(8f)] private float _roomSize = 12f;
        [SerializeField, Min(3.5f)] private float _roomHeight = 4f;
        [SerializeField, Min(.01f)] private float _wallThickness = .2f;
        [SerializeField, Min(.1f)] private float _secondsPerRoom = 2f;
        [SerializeField, Min(.1f)] private float _secondsPerCameraSegment = 4f;
        [SerializeField, Min(.1f)] private float _eyeHeight = 1.6f;
        [SerializeField, Range(30f, 100f)] private float _fieldOfView = 70f;
        [SerializeField, Min(1)] private int _warmupFrames = 60;
        [SerializeField, Min(1)] private int _sampleFrames = 300;
        [SerializeField, Min(1f)] private float _timeoutSeconds = 120f;
        [SerializeField, Range(0f, 1f)] private float _minimumProbeProgress = .05f;
        [SerializeField, Min(0f)] private float _lightIntensity = 2f;
        [SerializeField, Min(0f)] private float _lightCeilingOffset = .5f;
        [SerializeField, Min(.01f)] private float _cameraNearClip = .1f;
        [SerializeField, Min(1f)] private float _cameraFarRoomMultiplier = 8f;
        public float RoomSize => Mathf.Max(8f, _roomSize);
        public float RoomHeight => Mathf.Max(3.5f, _roomHeight);
        public float WallThickness => Mathf.Max(.01f, _wallThickness);
        public float SecondsPerRoom => Mathf.Max(.1f, _secondsPerRoom);
        public float SecondsPerCameraSegment => Mathf.Max(.1f, _secondsPerCameraSegment);
        public float EyeHeight => Mathf.Max(.1f, _eyeHeight);
        public float FieldOfView => _fieldOfView;
        public int WarmupFrames => Mathf.Max(1, _warmupFrames);
        public int SampleFrames => Mathf.Max(1, _sampleFrames);
        public float TimeoutSeconds => Mathf.Max(1f, _timeoutSeconds);
        public float MinimumProbeProgress => Mathf.Clamp01(_minimumProbeProgress);
        public float LightIntensity => Mathf.Max(0f, _lightIntensity);
        public float LightCeilingOffset => Mathf.Max(0f, _lightCeilingOffset);
        public float CameraNearClip => Mathf.Max(.01f, _cameraNearClip);
        public float CameraFarRoomMultiplier => Mathf.Max(1f, _cameraFarRoomMultiplier);
    }
}
