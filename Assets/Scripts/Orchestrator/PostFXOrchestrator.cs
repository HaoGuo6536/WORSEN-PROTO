// ============================================================================
// PostFXOrchestrator.cs
// ============================================================================
// PURPOSE:
//   Routes committed Session facts into the PostFX presentation service.
//   It keeps presentation independent of Domain types and survives explicit
//   scene assembly without relying on component startup order.
// ARCHITECTURAL ROLE:
//   Orchestrator (§6) · Orchestrator · PostFX target.
// KEY RESPONSIBILITIES:
//   - Pair HorrorEffects cleanse/revival facts with targeted blindness and catch-latch clearing.
//   - Pair Run grace, Blinder/Blind trap and Progression effects; restore effects after capture resets.
//   - Forward confirmed consumption before terminal presentation; preserve ordinary injury.
//   - Restore the Environment-approved hunter rim after capture resets (default off).
//   - Share Horror's single startle admission with the Director intrusion's visual strength.
// DEPENDENCIES:
//   - Session.HorrorEffects publishes consumable cleanse and revival boundaries.
//   - Session.Run typed fact channels/Progression and Presentation.PostFX/Horror; Core payloads downstream.
//   - Domain.Floor trap facts are translated into duration-only presentation commands.
//   - CameraManager supplies a configured consumption duration only during Configure.
//   - HorrorManager supplies the whole-run clock unless Configure injects a test clock.
//   - EnvironmentManager supplies the configured look-back rim strength, never gameplay visibility.
// USAGE NOTES:
//   Scene-owned; release subscriptions before the scene volume is destroyed. Setup provides serialized references before activation.
//   Initialize Camera before Configure to synchronize its configured duration.
//   Serialized duration preserves older setup paths. No runtime sequence state lives here.
//   Director slow-player intrusions are provisionally earned; IntrusionSample has no earned flag.
//   Missing or uninitialized Horror degrades to subtle feedback, never an unbudgeted startle.
//   An explicitly injected clock takes precedence; invalid readings still fail closed.
//   Run.ElapsedSeconds is floor-local in Expedition and must not be used as the whole-run clock.
//   Legacy scene setup initializes HorrorEffects after Configure. CaptureStarted retries
//   that persistent-service binding before gameplay; explicit injection remains preferred.
// ============================================================================

using System;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Session.Run;
using Worsen.Session.Progression;
using Worsen.Session.HorrorEffects;
using Worsen.Domain.Floor;
using Worsen.Presentation.PostFX;
using Worsen.Presentation.Camera;
using Worsen.Presentation.Horror;
using Worsen.Presentation.Environment;

namespace Worsen.Orchestrator
{
    public sealed class PostFXOrchestrator : MonoBehaviour
    {
        [SerializeField] private RunSessionManager _run;
        [SerializeField] private PostFXManager _postFX;
        [SerializeField] private HorrorManager _horror;
        private ProgressionSessionManager _progression;
        private HorrorEffectsManager _effects;
        private EnvironmentManager _environment;
        private Func<double> _runSeconds;
        [SerializeField, Range(0.1f, 2f)] private float _consumptionSeconds = 0.9f;
        public void Configure(RunSessionManager run, PostFXManager postFX, CameraManager camera = null,
            HorrorManager horror = null, Func<double> runSeconds = null, ProgressionSessionManager progression = null,
            HorrorEffectsManager effects = null, EnvironmentManager environment = null)
        {
            OnDisable(); _run = run; _postFX = postFX; _horror = horror; _runSeconds = runSeconds;
            _progression = progression;
            _effects = effects;
            _environment = environment;
            if (camera != null) _consumptionSeconds = camera.ConsumptionSeconds;
            if (isActiveAndEnabled) OnEnable();
        }
        private void OnEnable()
        {
            OnDisable();
            if (_run == null || _postFX == null) return;
            _run = RunSessionManager.Instance ?? _run;
            _postFX.Initialize();
            _effects = _effects != null ? _effects : HorrorEffectsManager.Instance;
            if (_effects != null)
            { _effects.SensesCleansed += OnSensesCleansed; _effects.PlayerRevived += OnPlayerRevived; }
            _run.PlayerMovementPublished += OnMovement;
            _run.CaptureStarted += OnCaptureStarted;
            _run.HunterFacts.ProximityPublished += OnProximity;
            _run.HealthChanged += OnHealth;
            _run.IntrusionPublished += OnIntrusion;
            _run.CollapseHandPublished += OnCollapseHand;
            _run.PlayerFacts.OnGraceStarted += OnGraceStarted;
            _run.PlayerFacts.OnGraceEnded += OnGraceEnded;
            _run.TrapSprung += OnTrapSprung;
            _run.HunterFacts.BlinderHitPublished += OnBlinderHit;
            if (_progression != null) _progression.EffectsSnapshotChanged += OnEffectsSnapshot;
            OnActiveEffectsChanged(_progression != null ? _progression.EffectsSnapshot.ActiveEffects : null);
            RestoreHunterRim();
        }
        private void OnDisable()
        {
            if (_effects != null)
            { _effects.SensesCleansed -= OnSensesCleansed; _effects.PlayerRevived -= OnPlayerRevived; }
            if (_progression != null) _progression.EffectsSnapshotChanged -= OnEffectsSnapshot;
            if (_run == null) return;
            _run.PlayerMovementPublished -= OnMovement;
            _run.CaptureStarted -= OnCaptureStarted;
            _run.HunterFacts.ProximityPublished -= OnProximity;
            _run.HealthChanged -= OnHealth;
            _run.IntrusionPublished -= OnIntrusion;
            _run.CollapseHandPublished -= OnCollapseHand;
            _run.PlayerFacts.OnGraceStarted -= OnGraceStarted;
            _run.PlayerFacts.OnGraceEnded -= OnGraceEnded;
            _run.TrapSprung -= OnTrapSprung;
            _run.HunterFacts.BlinderHitPublished -= OnBlinderHit;
        }
        private void OnDestroy() => OnDisable();
        private void OnEffectsSnapshot(ProgressionSnapshot snapshot, IReadOnlyActiveEffects effects) => OnActiveEffectsChanged(effects);
        private void OnTrapSprung(FloorTrapSprungFact fact)
        { if (fact.Kind == FloorTrapKind.Blind) OnBlindTrap(_postFX.BlindTrapSeconds); }
        private void OnBlinderHit(BlinderHitFact fact) => _postFX.SetBlindness(fact.Duration);
        private void OnMovement(PlayerMovementSample sample) => _postFX.SetLookBack(sample.LookBack);
        public void OnGraceStarted(GraceWindowFact fact) => _postFX.SetGrace(fact, true);
        public void OnGraceEnded(GraceWindowFact fact) => _postFX.SetGrace(fact, false);
        public void OnActiveEffectsChanged(IReadOnlyActiveEffects effects) => _postFX.SetActiveEffects(effects);
        public void OnBlindTrap(float seconds) => _postFX.SetBlindness(seconds);
        private void OnSensesCleansed(SensoryCleanseFact fact) => _postFX.SetBlindness(0f);
        private void OnPlayerRevived(EntityId id) => _postFX.ClearConsumed();
        private void OnCaptureStarted(RunCaptureMetadata metadata)
        {
            if (_effects == null) OnEnable();
            _postFX.ResetEffects();
            RestoreHunterRim();
            OnActiveEffectsChanged(_progression != null ? _progression.EffectsSnapshot.ActiveEffects : null);
        }
        private void RestoreHunterRim() => _postFX.SetHunterRim(_environment != null ? _environment.HunterRim(true) : 0f);
        private void OnProximity(ProximitySample sample) => _postFX.SetProximity(sample.Closeness);
        private void OnHealth(EntityId id, float health, float maximum) => _postFX.SetInjury(health, maximum);
        private void OnIntrusion(IntrusionSample sample)
        {
            bool admitted = _horror != null && _horror.TryStartle(_runSeconds != null ? _runSeconds() : _horror.RunElapsedSeconds, true);
            _postFX.PlayIntrusion(sample.DurationSeconds, admitted);
        }
        private void OnCollapseHand(CollapseHandFact fact)
        {
            if (fact.Kind == CollapseHandEventKind.Consumed) _postFX.PlayConsumed(_consumptionSeconds);
        }
    }
}

