// ============================================================================
// LevelDriver.cs
// ============================================================================
// PURPOSE:
//   Owns marker subscriptions and captures scene authoring data for LevelManager.
//   Initial reconciliation includes enabled children whose lifecycle callbacks
//   happened before the Manager was initialized.
// ARCHITECTURAL ROLE:
//   Driver (§7a) · Domain · Level.
// KEY RESPONSIBILITIES:
//   - Relay marker lifecycle facts and provide current active snapshots.
// DEPENDENCIES:
//   - Core level contracts; no other Domain system and no upper runtime layer.
// USAGE NOTES:
//   Scene-owned; no global engine effects and no tunables requiring DriverConfig.
//   Markers are serialized by setup; child lookup is initialization self-heal only.
//   The Manager commands this Driver; marker event payloads are Core values.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Level
{
    [DisallowMultipleComponent]
    public sealed class LevelDriver : MonoBehaviour
    {
        [SerializeField] private LevelMarker[] _markers;
        private bool _initialized;
        private bool _subscribed;

        public event Action<LevelMarkerRecord> MarkerEnabled;
        public event Action<LevelMarkerRecord> MarkerDisabled;

        public void Initialize()
        {
            if (!_initialized)
            {
                if (_markers == null || _markers.Length == 0)
                    _markers = GetComponentsInChildren<LevelMarker>(true);
                _initialized = true;
            }
            if (isActiveAndEnabled) OnEnable();
        }

        public IReadOnlyList<LevelMarkerRecord> CaptureEnabledMarkers()
        {
            var result = new List<LevelMarkerRecord>();
            if (_markers == null) return result;
            foreach (var marker in _markers)
                if (marker != null && marker.isActiveAndEnabled)
                    result.Add(marker.Capture());
            return result;
        }

        public void Teardown()
        {
            OnDisable();
            _initialized = false;
        }

        private void OnEnable()
        {
            if (!_initialized || _subscribed) return;
            foreach (var marker in _markers)
            {
                if (marker == null) continue;
                marker.MarkerEnabled += HandleMarkerEnabled;
                marker.MarkerDisabled += HandleMarkerDisabled;
            }
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            foreach (var marker in _markers)
            {
                if (marker == null) continue;
                marker.MarkerEnabled -= HandleMarkerEnabled;
                marker.MarkerDisabled -= HandleMarkerDisabled;
            }
            _subscribed = false;
        }

        private void HandleMarkerEnabled(LevelMarkerRecord record) => MarkerEnabled?.Invoke(record);
        private void HandleMarkerDisabled(LevelMarkerRecord record) => MarkerDisabled?.Invoke(record);
    }
}
