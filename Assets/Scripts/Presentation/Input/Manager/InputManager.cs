// ============================================================================
// InputManager.cs
// ============================================================================
//
// PURPOSE:
//   Owns the persistent input service and forwards device frames to subscribers.
//   A SceneRoot explicitly initializes the service and uses the returned canonical
//   instance, so a repeated scene load cannot create a second input publisher.
//
// ARCHITECTURAL ROLE:
//   Manager (§1) · Presentation · Input (Service system).
//   Owns PlayerInputDriver; publishes Core input facts for an Orchestrator.
//
// KEY RESPONSIBILITIES:
//   - Initialize one persistent service and discard duplicate service roots.
//   - Pair Driver subscriptions with this component's enabled lifetime.
//   - Command one frame publication per caller-controlled fixed tick.
//   - Expose exact source selection and tick-aligned recording through the owned Driver.
//
// DEPENDENCIES:
//   - Core InputFrame; the Input system's own PlayerInputDriver only.
//
// USAGE NOTES:
//   - Lifecycle tier: Persistent (DontDestroyOnLoad); requires a dedicated root GameObject.
//   - SceneRoot must call Initialize and retain its returned canonical instance.
//   - OnEnable subscribes only after explicit initialization; OnDisable unsubscribes.
//   - Driver teardown is symmetric; this service caches no scene-owned objects.
//
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Input
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputDriver))]
    public sealed class InputManager : MonoBehaviour
    {
        [SerializeField] private PlayerInputDriver _driver;
        private bool _initialized;
        private bool _subscribed;

        public static InputManager Instance { get; private set; }
        public event Action<InputFrame> FramePublished;
        public InputSource Source => _initialized ? _driver.Source : InputSource.Live;
        public InputProbeRecord CurrentPlaybackRecord => _initialized ? _driver.CurrentPlaybackRecord : default;
        public string LastRecordingPath => _initialized ? _driver.LastRecordingPath : "";
        public string LastRecordingError => _initialized ? _driver.LastRecordingError : "Input is not initialized.";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        public InputManager Initialize()
        {
            if (Instance != null && Instance != this)
            {
                InputManager canonical = Instance;
                enabled = false;
                PlayerInputDriver duplicateDriver = GetComponent<PlayerInputDriver>();
                if (duplicateDriver != null)
                {
                    duplicateDriver.Teardown();
                    duplicateDriver.enabled = false;
                }
                Destroy(gameObject);
                return canonical;
            }

            Instance = this;
            if (_initialized)
                return this;

            DontDestroyOnLoad(gameObject);
            if (_driver == null)
                _driver = GetComponent<PlayerInputDriver>();
            _driver.Initialize();
            _initialized = true;
            if (isActiveAndEnabled)
                OnEnable();
            return this;
        }

        public void PublishFrame()
        {
            if (_initialized && isActiveAndEnabled)
                _driver.FlushFrame();
        }

        public void SetInputEnabled(bool enabled)
        {
            if (_initialized)
                _driver.SetInputEnabled(enabled);
        }

        public bool StartPlayback(RunCaptureMetadata metadata, IReadOnlyList<InputProbeRecord> records) =>
            _initialized && _driver.StartPlayback(metadata, records);
        public bool LoadPlayback(string absolutePath) => _initialized && _driver.LoadPlayback(absolutePath);
        public bool SetSource(InputSource source) => _initialized && _driver.SetSource(source);
        public void BeginRecording(RunCaptureMetadata metadata)
        {
            if (_initialized) _driver.BeginRecording(metadata);
        }
        public bool RecordProbe(InputProbeRecord record) => _initialized && _driver.RecordProbe(record);
        public bool SaveRecording(long endTick, bool complete) => _initialized && _driver.SaveRecording(endTick, complete);

        private void OnEnable()
        {
            if (!_initialized || _subscribed)
                return;
            _driver.FrameCaptured += HandleFrameCaptured;
            _subscribed = true;
            _driver.SetOwnerEnabled(true);
        }

        private void OnDisable()
        {
            if (!_subscribed)
                return;
            _driver.FrameCaptured -= HandleFrameCaptured;
            _subscribed = false;
            _driver.SetOwnerEnabled(false);
        }

        private void OnDestroy()
        {
            OnDisable();
            if (_initialized && _driver != null)
                _driver.Teardown();
            if (Instance == this)
                Instance = null;
            FramePublished = null;
        }

        private void HandleFrameCaptured(InputFrame frame)
        {
            FramePublished?.Invoke(frame);
        }
    }
}
