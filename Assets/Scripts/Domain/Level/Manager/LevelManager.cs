// ============================================================================
// LevelManager.cs
// ============================================================================
// PURPOSE:
//   Initializes the scene-owned Level service before scene readiness is published.
//   It connects marker lifecycle events, the Registry and pure graph assembly,
//   providing downstream consumers a typed read-only state rather than scene searches.
// ARCHITECTURAL ROLE:
//   Manager (§1) · Domain · Level (Service system).
// KEY RESPONSIBILITIES:
//   - Reconcile enabled markers, sequence rebuilds and publish readiness facts.
//   - Revalidate incomplete removal snapshots after lifecycle callbacks settle.
//   - Accept a generated graph through explicit initialization, without markers.
// DEPENDENCIES:
//   - Core level contracts; no other Domain system and no upper runtime layer.
// USAGE NOTES:
//   Scene-owned Service system. SceneRoot or Session calls Initialize explicitly;
//   this service never subscribes to an upper-layer SceneReady event. Initialization
//   failures propagate so the boot owner cannot announce a usable partial graph.
//   Marker removal invalidates readiness immediately. An incomplete removal is
//   revalidated on the next active LateUpdate before reporting an authoring error,
//   because scene unload may disable children before this Manager. This callback
//   flushes a lifecycle diagnostic only; it does not tick gameplay or read time.
// ============================================================================

using System;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Level
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LevelDriver), typeof(LevelMarkerRegistry))]
    public sealed class LevelManager : MonoBehaviour
    {
        [SerializeField] private LevelDriver _driver;
        [SerializeField] private LevelMarkerRegistry _registry;
        private readonly LevelBehaviorState _state = new LevelBehaviorState();
        private LevelController _controller;
        private bool _initialized;
        private bool _subscribed;
        private bool _removalValidationPending;

        public IReadOnlyLevelState ReadOnlyState => _state;
        public event Action<bool> ReadinessChanged;

        public IReadOnlyLevelState Initialize()
        {
            if (_driver == null) _driver = GetComponent<LevelDriver>();
            if (_registry == null) _registry = GetComponent<LevelMarkerRegistry>();
            if (_controller == null) _controller = new LevelController(_state);
            _driver.Initialize();
            _initialized = true;
            if (isActiveAndEnabled && !_subscribed) OnEnable();
            else Reconcile();
            return _state;
        }

        public IReadOnlyLevelState InitializeGenerated(LevelGraph graph)
        {
            Teardown();
            if (_controller == null) _controller = new LevelController(_state);
            _controller.LoadGenerated(graph);
            ReadinessChanged?.Invoke(true);
            return _state;
        }

        public void Teardown()
        {
            OnDisable();
            if (_driver != null) _driver.Teardown();
            if (_registry != null) _registry.Clear();
            _controller?.Invalidate();
            _initialized = false;
        }

        private void OnEnable()
        {
            if (!_initialized || _subscribed) return;
            _driver.MarkerEnabled += HandleMarkerEnabled;
            _driver.MarkerDisabled += HandleMarkerDisabled;
            _subscribed = true;
            _driver.enabled = true;
            Reconcile();
        }

        private void OnDisable()
        {
            _removalValidationPending = false;
            if (_subscribed)
            {
                _driver.MarkerEnabled -= HandleMarkerEnabled;
                _driver.MarkerDisabled -= HandleMarkerDisabled;
                _subscribed = false;
            }
            if (_driver != null) _driver.enabled = false;
            _controller?.Invalidate();
            if (_initialized) ReadinessChanged?.Invoke(false);
        }

        private void OnDestroy() => Teardown();

        private void LateUpdate()
        {
            if (!_initialized || !_removalValidationPending) return;
            _removalValidationPending = false;
            try
            {
                _controller.Rebuild(_registry.Records);
                ReadinessChanged?.Invoke(true);
            }
            catch (ArgumentException exception)
            {
                _controller.Invalidate();
                ReadinessChanged?.Invoke(false);
                Debug.LogError("Level marker registration is incomplete: " + exception.Message, this);
            }
        }

        private void Reconcile()
        {
            _registry.Clear();
            foreach (var record in _driver.CaptureEnabledMarkers()) _registry.Register(record);
            _controller.Rebuild(_registry.Records);
            _removalValidationPending = false;
            ReadinessChanged?.Invoke(true);
        }

        private void HandleMarkerEnabled(LevelMarkerRecord record)
        {
            try
            {
                _registry.Register(record);
                _controller.Rebuild(_registry.Records);
                _removalValidationPending = false;
                ReadinessChanged?.Invoke(true);
            }
            catch (ArgumentException exception)
            {
                _controller.Invalidate();
                ReadinessChanged?.Invoke(false);
                Debug.LogError("Level marker registration is incomplete: " + exception.Message, this);
            }
        }

        private void HandleMarkerDisabled(LevelMarkerRecord record)
        {
            _registry.Unregister(record.Id);
            try
            {
                _controller.Rebuild(_registry.Records);
                _removalValidationPending = false;
                ReadinessChanged?.Invoke(true);
            }
            catch (ArgumentException)
            {
                _controller.Invalidate();
                _removalValidationPending = true;
                ReadinessChanged?.Invoke(false);
            }
        }
    }
}
