// ============================================================================
// PlayerManager.cs
// ============================================================================
// PURPOSE:
//   Sequences probe, decision, movement, committed state and published Player facts.
//   This is part of the solo movement prototype. Explicit inputs keep its
//   behavior reproducible and its ownership visible during integration.
// ARCHITECTURAL ROLE:
//   Manager (§1) · Domain · Player (Entity system).
// KEY RESPONSIBILITIES:
//   - Apply aggregate run health/movement modifiers through the Controller and publish health changes.
//   - Keep game rules, passive state, and engine interactions in separate roles.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Scene-owned; Initialize creates a fresh life, Teardown clears it. No competing FixedUpdate loop; Run Session supplies each tick.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Player
{
    [RequireComponent(typeof(PlayerDriver))]
    public sealed class PlayerManager : MonoBehaviour, IEntityHandle
    {
        [SerializeField] private PlayerDriver _driver;
        private PlayerBehaviorState _state;
        private PlayerController _controller;
        private PlayerProfile _profile;
        public EntityId Id => _state?.Id ?? EntityId.None;
        public IReadOnlyPlayerState ReadOnlyState => _state;
        public PlayerMovementSample LastMovementSample => _state?.LastMovementSample ?? default;
        public InputProbeRecord LastProbeRecord => _state?.LastProbeRecord ?? default;
        public IReadOnlyList<PlayerTraversalFact> LastTraversalFacts => _state?.LastTraversalFacts ?? Array.Empty<PlayerTraversalFact>();
        public event Action<EntityId, float, float> OnHealthChanged;
        public event Action<EntityId, Vector3> OnDied;
        public event Action<EntityId, bool> OnLookBackChanged;
        public event Action<PlayerMovementSample> OnMovementSample;
        public event Action<PlayerTraversalFact> OnTraversal;
        public event Action<InputProbeRecord> InputProbeRecorded;

        private void Awake() { if (_driver == null) _driver = GetComponent<PlayerDriver>(); }
        public void Initialize(PlayerProfile profile, EntityContext context)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!context.Id.IsValid || context.Random == null) throw new ArgumentException("Player requires an assigned id and shared random source.");
            if (_driver == null) _driver = GetComponent<PlayerDriver>();
            _driver.Initialize();
            _profile = profile;
            _state = new PlayerBehaviorState();
            _controller = new PlayerController(_state, profile, context.Random);
            _controller.Reset(context.Id, _driver.Position, _driver.Heading);
        }

        public void Tick(InputFrame frame, float dt, long tick)
        {
            if (_controller == null) return;
            bool wasLookingBack = _state.LookBack;
            MovementProbe probe = _driver.Probe();
            PlayerTickResult result = _controller.Tick(frame, probe, dt, tick);
            PlayerMoveResult movement = result.Traversing
                ? _driver.MoveTraversal(result.TraversalStart, result.TraversalTarget, result.TraversalProgress,
                    result.TraversalHeight, _state.Velocity, _state.HeadingDegrees, dt, _controller.MaximumMovementSpeed)
                : _driver.Move(result.Displacement, _state.Velocity, result.Crouched, _state.HeadingDegrees, dt);
            _controller.CommitPose(movement);
            var resolution = new MovementResolution(movement.Position, movement.Velocity, movement.Grounded, movement.Ceiling, _driver.EyePosition);
            var record = new InputProbeRecord(InputProbeRecord.CurrentSchemaVersion, tick, frame, probe, dt, resolution);
            _controller.CommitFrame(_driver.EyePosition, record, result.Facts);
            _driver.ShowMovement(_state.MovementState);
            if (wasLookingBack != _state.LookBack) OnLookBackChanged?.Invoke(Id, _state.LookBack);
            OnMovementSample?.Invoke(LastMovementSample);
            InputProbeRecorded?.Invoke(record);
            foreach (PlayerTraversalFact fact in LastTraversalFacts) OnTraversal?.Invoke(fact);
        }

        public void ApplyHit(float damage, Vector3 killerPosition)
        {
            if (_controller == null) return;
            PlayerHitResult result = _controller.ApplyHit(damage);
            if (result.Changed) OnHealthChanged?.Invoke(Id, _state.Health, _state.MaxHealth);
            if (result.Died) OnDied?.Invoke(Id, killerPosition);
        }
        public void ApplyRunModifiers(float health, float maximumHealth, float movementMultiplier)
        {
            if (_controller == null) return;
            PlayerHitResult result = _controller.ApplyRunModifiers(health, maximumHealth, movementMultiplier);
            if (result.Changed) OnHealthChanged?.Invoke(Id, _state.Health, _state.MaxHealth);
            if (result.Died) OnDied?.Invoke(Id, _state.Position);
        }
        public void ApplyLungeHit(Vector3 killerPosition) { if (_profile != null) ApplyHit(_profile.LungeDamage, killerPosition); }
        public void Teardown()
        {
            if (_driver != null) _driver.Teardown();
            _controller = null;
            _state = null;
            _profile = null;
        }
        private void OnDestroy() { Teardown(); }
    }
}
