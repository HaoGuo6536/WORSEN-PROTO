// ============================================================================
// DirectorManager.cs
// ============================================================================
// PURPOSE:
//   Connects the scene's Director rules to committed Domain state and Hunters.
//   The Session calls this service after Player, Hunter, Chase, and Floor have
//   advanced, so hints and presentation facts describe one ordered simulation.
// ARCHITECTURAL ROLE:
//   Manager (§1) · Domain · Director (Service system).
// KEY RESPONSIBILITIES:
//   - Inject typed dependencies and snapshot registered entities for pure rules.
//   - Deliver hints downward through HunterRegistry and publish Core facts upward.
//   - Clear all scene-owned history during explicit teardown.
//   - Commit initialization atomically and stop stale publication after callbacks.
//   - Route room hints, environmental noises and requested retreats to Hunter owners.
// DEPENDENCIES:
//   - Player Registry and read-only state for committed pose and health.
//   - Hunter Registry and read-only pose/target state; calls HunterManager.ReceiveHint.
//   - Chase read-only state for chase identity and Floor read-only exit state.
//   - Core shared immutable hint, intrusion, and pressure observations.
//   - Optional injected Level view for room inference; Floor view is passed to Hunters.
// USAGE NOTES:
//   Scene-owned Service system; no singleton, subscriptions, or competing tick.
//   Initialize is called explicitly by scene assembly. Tick(float dt,long tick)
//   is called only by Session after Floor. Retreat trials consume injected Random.
//   SetLevelView and SetClosedDoors require scene-owner routing; no scene discovery.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Domain.Hunter;
using Worsen.Domain.Chase;
using Worsen.Domain.Floor;
using Worsen.Domain.Level;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Director
{
    [DisallowMultipleComponent]
    public sealed class DirectorManager : MonoBehaviour
    {
        private DirectorController _controller;
        private IReadOnlyChaseState _chase;
        private IReadOnlyFloorState _floor;

        public event Action<HintPayload> OnHintIssued;
        public event Action<IntrusionSample> OnIntrusion;
        public event Action<DirectorPressureSample> OnPressureSampled;
        public event Action<HintPayload, int> OnRegionHintIssued;
        public event Action<EntityId, long> OnRetreatIssued;
        public event Action<EntityId, NoiseEvent> OnNoiseHintIssued;
        public void SetLevelView(IReadOnlyLevelState level) { _controller?.SetLevelView(level); }
        public void SetClosedDoors(IReadOnlyDictionary<int, bool> doors) { _controller?.SetClosedDoors(doors); }
        public void HearNoise(NoiseEvent noise) { _controller?.HearNoise(noise); }

        public void Initialize(DirectorConfig config, System.Random random, IReadOnlyChaseState chase, IReadOnlyFloorState floor)
        {
            if (chase == null) throw new ArgumentNullException(nameof(chase));
            if (floor == null) throw new ArgumentNullException(nameof(floor));
            var controller = new DirectorController(new DirectorBehaviorState(), config, random);
            _chase = chase;
            _floor = floor;
            _controller = controller;
        }

        public void Tick(float dt, long tick)
        {
            if (_controller == null) return;
            var owner = _controller;
            var players = new List<DirectorPlayerSample>();
            foreach (var manager in PlayerRegistry.Items)
            {
                if (manager == null || manager.ReadOnlyState == null) continue;
                var view = manager.ReadOnlyState;
                players.Add(new DirectorPlayerSample(view.Id, view.Position, view.Velocity, view.IsAlive,
                    _chase.HasActiveChase && _chase.PlayerId == view.Id));
                if (view.RecentNoises != null) foreach (var noise in view.RecentNoises) owner.HearNoise(noise);
            }
            var hunters = new List<DirectorHunterSample>();
            foreach (var manager in HunterRegistry.Items)
            {
                if (manager == null || manager.ReadOnlyState == null) continue;
                var view = manager.ReadOnlyState;
                manager.SetFloorView(_floor);
                hunters.Add(new DirectorHunterSample(view.Id, view.TargetId, view.Position, view.IsActive,
                    manager.IsPursuing, manager.HearingModel));
            }
            var result = owner.Tick(tick, dt, players, hunters, _floor.ExitState == ExitState.Open);
            foreach (var hint in result.Hints)
            {
                if (!ReferenceEquals(owner, _controller)) return;
                if (!HunterRegistry.TryGet(hint.Hunter, out var hunter)) continue;
                bool regionDelivered = false;
                HintPayload delivered = hint;
                foreach (var region in result.Regions)
                {
                    if (region.Hint.Hunter != hint.Hunter) continue;
                    hunter.ReceiveRegionHint(region.Hint, region.RoomId); regionDelivered = true; delivered = region.Hint;
                    OnRegionHintIssued?.Invoke(region.Hint, region.RoomId); break;
                }
                if (!regionDelivered) hunter.ReceiveHint(hint);
                if (!ReferenceEquals(owner, _controller)) return;
                OnHintIssued?.Invoke(delivered);
            }
            foreach (var id in result.Retreats)
            {
                if (!ReferenceEquals(owner, _controller)) return;
                if (HunterRegistry.TryGet(id, out var hunter) && hunter.RequestRetreat()) OnRetreatIssued?.Invoke(id, tick);
            }
            foreach (var noise in result.Noises)
            {
                if (!ReferenceEquals(owner, _controller)) return;
                if (!HunterRegistry.TryGet(noise.Hunter, out var hunter)) continue;
                hunter.HearNoise(noise.Noise); OnNoiseHintIssued?.Invoke(noise.Hunter, noise.Noise);
            }
            foreach (var intrusion in result.Intrusions)
            {
                if (!ReferenceEquals(owner, _controller)) return;
                OnIntrusion?.Invoke(intrusion);
            }
            foreach (var sample in result.Pressure)
            {
                if (!ReferenceEquals(owner, _controller)) return;
                OnPressureSampled?.Invoke(sample);
            }
        }

        public void Teardown()
        {
            _controller?.Reset();
            _controller = null;
            _chase = null;
            _floor = null;
        }

        private void OnDestroy() { Teardown(); }
    }
}
