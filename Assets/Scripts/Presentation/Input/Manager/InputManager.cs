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
