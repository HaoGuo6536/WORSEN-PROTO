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
//   - Sequence seeded generation, physical admission, organic recovery and no-floor failure.
//   - Publish Core graphs, validated hunter capacity and future Passage gold counts.
//   - Route destruction and interactable state into owned presentation.
//   - Publish theme, threshold, puzzle and admitted shrine facts for external routing.
//   - Publish Passage tiles, anchors and ordered collapse facts; reset on teardown.
// DEPENDENCIES:
//   - Core graph, interactable and destruction contracts; no Domain sibling calls.
// USAGE NOTES:
//   Scene-owned Service system with explicit Initialize/Teardown. The boot/session
//   owner injects both configs and destroys actors before replacing the map.
//   No Update, singleton, implicit authored fallback or global render changes.
//   UsedFallback means terminal no-floor, not a validated organic recovery floor;
//   the latter retains its reason and all failed attempts in LayoutManifest.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
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
        public bool GenerationSucceeded => _state.GenerationSucceeded;
        public bool UsedFallback => _state.UsedFallback;
        public int FallbackCount => _state.FallbackCount;
        public string FallbackReason => _state.OrganicFallbackReason;
        public float ExitDoorYaw => _state.Layout?.ExitDoorYaw ?? 0f;
        public LevelGraph Graph => IsReady ? _state.Layout.Graph : null;
        public Vector3 PlayerSpawnPosition => _state.Layout?.PlayerSpawnPosition ?? Vector3.zero;
        public Quaternion PlayerSpawnRotation => _state.Layout?.PlayerSpawnRotation ?? Quaternion.identity;
        public IReadOnlyList<Vector3> HunterSpawnPositions => _state.Layout?.HunterSpawnPositions ?? Array.Empty<Vector3>();
        public int ValidatedHunterSpawnCapacity => IsReady ? _state.Layout.ValidatedHunterSpawnCapacity : 0;
        public int FuturePassageGoldenAnchorCount => IsReady ? _state.Layout.FuturePassageGoldenAnchorCount : 0;
        public bool RoomHasAuthoredFurniture(int roomId) => IsReady && _state.Layout.TemplateRooms.Any(room => room.RoomId == roomId);
        public string RoomThemeId(int roomId) => IsReady ? ProceduralBiomeUtility.Theme(_state.Layout, roomId)?.Id ?? _state.Layout.ThemeId : string.Empty;
        public IReadOnlyList<Vector3> RoomLightSockets(int roomId)
            => !IsReady ? null : _state.Layout.Interactables
                .Where(p => p.State.RoomId == roomId && p.State.Kind == InteractableKind.Light).Select(p => p.State.Position).ToArray();
        public IReadOnlyList<Vector3> RoomBoundary(int roomId)
            => !IsReady ? null : ProceduralTemplateValidationUtility.PresentationBoundary(_state.Layout, roomId);
        public IReadOnlyList<GeneratedRoomSample> PresentationRooms => _state.Layout?.PresentationRooms ?? Array.Empty<GeneratedRoomSample>();
        public IReadOnlyList<ProceduralRoomModule> RoomModules => _state.Layout?.Modules ?? Array.Empty<ProceduralRoomModule>();
        public IReadOnlyList<ProceduralDoorPlan> Doors => _state.Layout?.Doors ?? Array.Empty<ProceduralDoorPlan>();
        public IReadOnlyList<ProceduralShrineSite> ShrineSites => IsReady ? _state.Layout.ShrineSites : Array.Empty<ProceduralShrineSite>();
        public string LayoutManifest => _state.GenerationManifest;
        public IReadOnlyList<InteractableState> Interactables => _state.Layout == null ? Array.Empty<InteractableState>() :
            Array.AsReadOnly(_state.Layout.Interactables.Select(plan => plan.State).ToArray());
        public IReadOnlyList<LevelMarkerRecord> TraversalMarkers => _driver != null ? _driver.TraversalMarkers : Array.Empty<LevelMarkerRecord>();
        public event Action<bool> ReadinessChanged;
        public event Action<int, int, string> GenerationFallbackPublished;
        public string ThemeId => IsReady ? _state.Layout.ThemeId : string.Empty;
        public event Action<string, string, string, string, string> ThemePublished;
        public event Action<int, string, string> RoomThemePublished;
        public event Action<int, int, int, Vector3, Vector3> ThresholdFreezePublished;
        public event Action<int, int, Vector3> OptionalPuzzleRewardPublished;
        public event Action<int, int, int> PuzzleSolved;
        public event Action<int, int, IReadOnlyList<Vector3>> PassageOpened;
        public event Action<int, int, int, Vector3> PassageTileCollapsed;
        public IReadOnlyList<LevelAnchor> LinedPocketAnchors => IsReady ? _driver.LinedPocketAnchors : Array.Empty<LevelAnchor>();

        private void OnEnable()
        {
            if (_driver == null) _driver = GetComponent<ProceduralDriver>();
            _driver.PuzzleSolved += OnPuzzleSolved; _driver.PassageTileCollapsed += OnPassageTileCollapsed;
        }
        private void OnDisable()
        { if (_driver != null) { _driver.PuzzleSolved -= OnPuzzleSolved; _driver.PassageTileCollapsed -= OnPassageTileCollapsed; } }
        private void OnPassageTileCollapsed(int site, int pocket, int tile, Vector3 position)
            => PassageTileCollapsed?.Invoke(site, pocket, tile, position);
        private void OnPuzzleSolved(int puzzle, int room, int reward) => PuzzleSolved?.Invoke(puzzle, room, reward);
        private bool IsPuzzleActor(Collider collider) => collider.GetComponentInParent<IEntityHandle>()?.Id == _driver.PuzzlePlayerId;
        public void TickPuzzles(PlayerMovementSample sample, float deltaTime)
        { if (IsReady) _driver.TickPuzzles(sample, deltaTime); }
        public void CompletePuzzleVault(int surfaceId, bool succeeded)
        { if (IsReady) _driver.CompletePuzzleVault(surfaceId, succeeded); }

        public bool ActivatePassage(int siteIndex)
        {
            if (!IsReady || !_driver.ActivatePassage(_state.Layout, siteIndex, _config, out var plan)) return false;
            PassageOpened?.Invoke(siteIndex, plan.PocketRoomId, plan.TilePositions);
            return true;
        }

        public void Initialize(ProceduralConfig config, ProceduralDriverConfig driverConfig, int runSeed, int roundIndex, bool merchantRefuge = false, float optionalWindowMultiplier = 1f, int? themeSeed = null, int requiredHunterCount = 1, int shrineRoomCount = 0)
        {
            Teardown();
            if (config != null) _config = config;
            if (driverConfig != null) _driverConfig = driverConfig;
            if (_config == null || _driverConfig == null) throw new InvalidOperationException("Procedural configuration is unwired.");
            if (_driver == null) _driver = GetComponent<ProceduralDriver>();
            var generation = new ProceduralGenerationController(_state);
            generation.Begin(runSeed, roundIndex, _config.GenerationRetries);
            while (true)
            {
                _controller = new ProceduralController(_state, _config,
                    new System.Random(ProceduralController.LayoutSeed(_state.AttemptSeed, roundIndex)));
                try
                {
                    var layout = _controller.Generate(_state.AttemptSeed, roundIndex, merchantRefuge, optionalWindowMultiplier, themeSeed ?? runSeed,
                        requiredHunterCount, _state.OrganicFallbackReason, shrineRoomCount);
                    _driver.Build(layout, _config, _driverConfig, IsPuzzleActor);
                    generation.Succeed(layout.Manifest + layout.InteractableManifest, layout.TemplateFallbackReason);
                    _controller.Admit();
                    break;
                }
                catch (Exception exception)
                {
                    _driver.Teardown();
                    if (generation.Fail(exception.GetType().Name + ": " + exception.Message,
                        _state.Layout == null ? null : _state.Layout.Manifest + _state.Layout.InteractableManifest,
                        _state.Layout?.UsesTemplates ?? false)) continue;
                    ReadinessChanged?.Invoke(false);
                    throw new InvalidOperationException("Procedural fallback: NoFloorAwaitingSession. " + LayoutManifest, exception);
                }
            }
            // Subscriber failures are not generation failures and must never trigger a retry.
            if (_state.FallbackCount != 0)
            {
                Debug.LogWarning("Procedural template fallback: seed=" + _state.AttemptSeed + " round=" + roundIndex + " reason=" + FallbackReason);
                GenerationFallbackPublished?.Invoke(_state.AttemptSeed, roundIndex, FallbackReason);
            }
            ReadinessChanged?.Invoke(true);
            var ready = _state.Layout;
            ThemePublished?.Invoke(ready.ThemeId, ready.Theme?.LightSource ?? "torch", ready.Theme?.SoundZone ?? "castle-stone",
                ready.Theme?.FogLook ?? "black-mist", ready.Theme?.HandLook ?? "shadow-hands");
            foreach (var room in ready.Modules)
            {
                var theme = ProceduralBiomeUtility.Theme(ready, room.RoomId);
                var template = ready.TemplateRooms.FirstOrDefault(r => r.RoomId == room.RoomId);
                RoomThemePublished?.Invoke(room.RoomId, theme?.Id ?? ready.ThemeId, template?.Template.Id ?? theme?.Families[(int)room.Kind] ?? room.Kind.ToString());
            }
            foreach (var freeze in ready.FreezeRooms)
                ThresholdFreezePublished?.Invoke(freeze.RoomId, freeze.BehindRoomId, freeze.AnchorId, ready.Doors[freeze.DoorIndex].Center, freeze.Hunter);
            // Retain the subscription surface for compatibility, but never publish
            // puzzle anchors into Floor's golden-cake reward registration path.
            _ = OptionalPuzzleRewardPublished;
        }

        public bool ValidateHunterSpawn(Vector3 position, out string reason)
        {
            if (!IsReady) { reason = "floor-not-ready"; return false; }
            return ProceduralSpawnUtility.Validate(_state.Layout, position, _config.DoorWidth,
                _state.Layout.MinimumHunterSpawnRooms, out reason);
        }

        public void ApplyInteractableState(InteractableState state)
        { if (IsReady && _driver != null) _driver.ApplyInteractableState(state); }

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
