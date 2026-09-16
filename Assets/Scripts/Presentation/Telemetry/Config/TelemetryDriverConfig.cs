// ============================================================================
// TelemetryDriverConfig.cs
// ============================================================================
// PURPOSE:
//   Controls telemetry output flushing without exposing mutable runtime settings. The Driver reads the asset and the tuning window edits it through serialized properties.
// ARCHITECTURAL ROLE:
//   DriverConfig (§7d) · Presentation · Telemetry.
// KEY RESPONSIBILITIES:
//   - Provide a bounded flush cadence.
// DEPENDENCIES:
//   - Unity ScriptableObject and Inspector attributes only.
// USAGE NOTES:
//   - Asset lives in Resources/ScriptableObjects/Presentation/Telemetry; existing assets preserve their tuning.
// ============================================================================
using UnityEngine;

namespace Worsen.Presentation.Telemetry
{
    [CreateAssetMenu(fileName = "TelemetryDriverConfig", menuName = "Worsen/Telemetry/Driver Config")]
    public sealed class TelemetryDriverConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int _flushEverySamples = 60;
        public int FlushEverySamples => Mathf.Max(1, _flushEverySamples);
    }
}