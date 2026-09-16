// ============================================================================
// ProceduralManager.cs
// ============================================================================
// PURPOSE:
//   Coordinates one scene-owned generated floor and exposes it only after the
//   logic and physical navigation checks succeed. The session can then supply
//   its graph to Level, spawn actors and initialize the ordinary Floor loop.
// ARCHITECTURAL ROLE:
//   Manager (§1) · Domain · Procedural (Service system).
// KEY RESPONSIBILITIES:
//   - Sequence seeded generation, physical construction and admission.
//   - Publish immutable Core graph data and spawn value types for scene assembly.
//   - Forward room destruction samples into owned masonry presentation.
// DEPENDENCIES:
//   - Core LevelGraph only outside this system; no Domain sibling calls.
// USAGE NOTES:
//   Scene-owned Service system with explicit Initialize/Teardown. The boot/session
//   owner injects both configs and destroys actors before replacing the map.
//   No Update, singleton, implicit authored fallback or global render changes.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Procedural
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ProceduralDriver))]
    public sealed class ProceduralManager : MonoBehaviour
    {
        [SerializeField] private ProceduralConfig _config;
        [SerializeField] private ProceduralDriverConfig _driverConfig;
        [SerializeField] private ProceduralDriver _driver;
        private readonly ProceduralBehaviorState _state = new ProceduralBehaviorState();
        private ProceduralController _controller;
        public bool IsReady => _state.IsReady;
        public LevelGraph Graph => IsReady ? _state.Layout.Graph : null;
        public Vector3 PlayerSpawnPosition => _state.Layout?.PlayerSpawnPosition ?? Vector3.zero;
        public Quaternion PlayerSpawnRotation => _state.Layout?.PlayerSpawnRotation ?? Quaternion.identity;
        public IReadOnlyList<Vector3> HunterSpawnPositions => _state.Layout?.HunterSpawnPositions ?? Array.Empty<Vector3>();
        public IReadOnlyList<GeneratedRoomSample> PresentationRooms => _state.Layout?.PresentationRooms ?? Array.Empty<GeneratedRoomSample>();
        public IReadOnlyList<ProceduralRoomModule> RoomModules => _state.Layout?.Modules ?? Array.Empty<ProceduralRoomModule>();
        public IReadOnlyList<ProceduralDoorPlan> Doors => _state.Layout?.Doors ?? Array.Empty<ProceduralDoorPlan>();
        public string LayoutManifest => _state.Layout?.Manifest ?? string.Empty;
        public IReadOnlyList<LevelMarkerRecord> TraversalMarkers => _driver != null ? _driver.TraversalMarkers : Array.Empty<LevelMarkerRecord>();
        public event Action<bool> ReadinessChanged;

        public void Initialize(ProceduralConfig config, ProceduralDriverConfig driverConfig, int runSeed, int roundIndex, bool merchantRefuge = false, float optionalWindowMultiplier = 1f)
        {
            Teardown();
            if (config != null) _config = config;
            if (driverConfig != null) _driverConfig = driverConfig;
            if (_config == null || _driverConfig == null) throw new InvalidOperationException("Procedural configuration is unwired.");
            if (_driver == null) _driver = GetComponent<ProceduralDriver>();
            _controller = new ProceduralController(_state, _config, new System.Random(ProceduralController.LayoutSeed(runSeed, roundIndex)));
            try
            {
                var layout = _controller.Generate(runSeed, roundIndex, merchantRefuge, optionalWindowMultiplier);
                _driver.Build(layout, _config, _driverConfig);
                _controller.Admit();
                ReadinessChanged?.Invoke(true);
            }
            catch (Exception exception)
            {
                Teardown();
                throw new InvalidOperationException("Procedural generation failed for seed " + runSeed + ", round " + roundIndex + ".", exception);
            }
        }

        public void SetRoomDestruction(RoomDestructionSample sample)
        { if (IsReady && _driver != null) _driver.SetRoomDestruction(sample); }

        public void Teardown()
        {
            _controller?.Reset();
            _controller = null;
            if (_driver != null) _driver.Teardown();
            ReadinessChanged?.Invoke(false);
        }
        private void OnDestroy() => Teardown();
    }
}
