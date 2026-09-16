// ============================================================================
// TelemetryManager.cs
// ============================================================================
// PURPOSE:
//   Owns the canonical persistent telemetry service and its Driver. Session routing sends immutable observations here, and this boundary never reads Domain state or constructs gameplay results.
// ARCHITECTURAL ROLE:
//   Manager (§1) · Presentation · Telemetry (Service system).
// KEY RESPONSIBILITIES:
//   - Initialize a canonical service; forward capture, movement, traversal and lifecycle commands.
// DEPENDENCIES:
//   - Core capture/player facts and own TelemetryDriver only.
// USAGE NOTES:
//   - Persistent (DontDestroyOnLoad). Initialize returns the canonical instance; no capture begins before explicit BeginSession. Teardown closes interrupted files.
// ============================================================================
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Telemetry
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TelemetryDriver))]
    public sealed class TelemetryManager : MonoBehaviour
    {
        [SerializeField] private TelemetryDriver _driver;
        private bool _initialized;
        public static TelemetryManager Instance { get; private set; }
        public string LastError => _initialized ? _driver.LastError : "Telemetry is not initialized.";
        public string LastOutputPath => _initialized ? _driver.LastOutputPath : "";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
        public TelemetryManager Initialize()
        {
            if (Instance != null && Instance != this)
            {
                var canonical = Instance;
                enabled = false;
                Destroy(gameObject);
                return canonical;
            }
            Instance = this;
            if (_initialized) return this;
            DontDestroyOnLoad(gameObject);
            if (_driver == null) _driver = GetComponent<TelemetryDriver>();
            _driver.Initialize();
            _initialized = true;
            return this;
        }
        public void BeginSession(RunCaptureMetadata metadata) { if (_initialized) _driver.BeginSession(metadata); }
        public void Record(TelemetrySample sample) { if (_initialized) _driver.Record(sample); }
        public void RecordMovement(PlayerMovementSample sample) { if (_initialized) _driver.RecordMovement(sample); }
        public void RecordTraversal(PlayerTraversalFact fact) { if (_initialized) _driver.RecordTraversal(fact); }
        public bool EndSession(long endTick, bool complete) => _initialized && _driver.EndSession(endTick, complete);
        private void OnDisable() { if (_initialized) _driver.Suspend(); }
        private void OnDestroy()
        {
            if (_initialized) _driver.Teardown();
            if (Instance == this) Instance = null;
        }
    }
}
