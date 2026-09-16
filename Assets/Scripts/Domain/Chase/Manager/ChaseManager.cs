// ============================================================================
// ChaseManager.cs
// ============================================================================
// PURPOSE:
//   Sequences the scene's chase rules after all hunter poses have been committed.
//   The service publishes identity-bearing pursuit facts and primitive proximity
//   without exposing mutable state to feedback or telemetry systems.
// ARCHITECTURAL ROLE:
//   Manager (§1) · Domain · Chase (Service system).
// KEY RESPONSIBILITIES:
//   - Supply current registered Hunter views to the Controller.
//   - Publish phase changes and route Session-accepted catches exactly once.
// DEPENDENCIES:
//   - Player read-only state, HunterRegistry and read-only Hunter views; Core facts.
// USAGE NOTES:
//   Scene-owned; explicit Initialize precedes Session ticking. No singleton or
//   competing FixedUpdate loop. Session must call RecordCatch only after damage acceptance.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
namespace Worsen.Domain.Chase
{
    public sealed class ChaseManager : MonoBehaviour
    {
        private ChaseBehaviorState _state;
        private ChaseController _controller;
        public IReadOnlyChaseState ReadOnlyState => _state;
        public event Action<ChaseFact> OnChaseStarted;
        public event Action<ChaseFact> OnChaseLost;
        public event Action<ChaseFact> OnChaseEnded;
        public event Action<ChaseFact> OnPhaseChanged;
        public event Action<ProximitySample> OnProximityChanged;
        public void Initialize(ChaseConfig config, IReadOnlyPlayerState player)
        {
            _state = new ChaseBehaviorState();
            _controller = new ChaseController(_state, config, player);
        }
        public void Tick(float dt, long tick)
        {
            if (_controller == null) return;
            var hunters = new List<IReadOnlyHunterState>();
            foreach (HunterManager hunter in HunterRegistry.Items)
                if (hunter != null && hunter.ReadOnlyState != null) hunters.Add(hunter.ReadOnlyState);
            Publish(_controller.Tick(hunters, dt, tick));
        }
        public void RecordCatch(HunterHit acceptedHit)
        { if (_controller != null) Publish(_controller.RecordCatch(acceptedHit)); }
        private void Publish(ChaseTickResult result)
        {
            if (result.Started) OnChaseStarted?.Invoke(result.Fact);
            if (result.Lost) OnChaseLost?.Invoke(result.Fact);
            if (result.Ended) OnChaseEnded?.Invoke(result.Fact);
            if (result.PhaseChanged) OnPhaseChanged?.Invoke(result.Fact);
            if (result.Proximity.Player.IsValid) OnProximityChanged?.Invoke(result.Proximity);
        }
        public void Teardown() { _controller = null; _state = null; }
        private void OnDestroy() { Teardown(); }
    }
}
