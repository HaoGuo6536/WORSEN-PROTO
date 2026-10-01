// ============================================================================
// TickingManager.cs
// ============================================================================
// PURPOSE:
//   Bridges the Ticking module's pure rules to its owned world-key driver.
//   It publishes sound, guidance and noise facts for external owners without
//   referencing Audio, HUD, Floor collection or Session implementation types.
// ARCHITECTURAL ROLE:
//   Manager (§1), Entity system facet · Domain · Hunter Ticking.
// KEY RESPONSIBILITIES:
//   - Register the clock module, resolve trigger identities and sequence bounded placement.
//   - Inject verified rear-pocket targets before the shared movement decision.
//   - Expose immutable Core facts with the owning hunter identity for duplicates.
//   - Refresh complete key navigation every tick, including the first tick across a room boundary.
// DEPENDENCIES:
//   - Own controller/driver, shared HunterController and injected Player/Hunter views.
// USAGE NOTES:
//   Scene-owned; HunterManager calls PrepareTick before and PublishTick after decisions.
//   No independent Update. Subscriptions pair OnEnable/OnDisable. Teardown removes
//   the key and clears guidance; the Hunter Factory owns the containing entity.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter.Archetypes.Ticking
{
    [RequireComponent(typeof(TickingDriver))]
    public sealed class TickingManager : HunterArchetypeManager, IHunterTickingModule
    {
        public static void Register(HunterArchetypeFactory factory)
            => factory.Register<TickingConfig, TickingManager>((config, profile, random) => new TickingController(config, random));
        public override IHunterTickingModule Ticking => this;
        protected override void Configure() => Initialize((TickingController)Rules, Shared, (TickingConfig)Profile.ArchetypeRules, Hunter, Player);
        public override bool PrepareTick(float dt, long tick) { PrepareTick(); return true; }
        public override void AfterReaction(HunterTickResult result) => PublishTick();
        public override void TeardownModule() => Teardown();
        private TickingDriver _driver;
        private TickingController _controller;
        private HunterController _shared;
        private TickingConfig _config;
        private IReadOnlyHunterState _hunter;
        private IReadOnlyPlayerState _player;
        public override EntityId Id => _hunter?.Id ?? EntityId.None;

        public event Action<TickingSoundFact> OnSound;
        public event Action<TickingGuidanceFact> OnGuidance;
        public event Action<TickingNoiseFact> OnNoise;
        private void OnEnable()
        { if (_driver == null) _driver = GetComponent<TickingDriver>(); _driver.OnKeyContact += HandleKey; }
        private void OnDisable()
        { if (_driver != null) _driver.OnKeyContact -= HandleKey; Teardown(); }
        public void Initialize(TickingController controller, HunterController shared, TickingConfig config,
            IReadOnlyHunterState hunter, IReadOnlyPlayerState player)
        {
            Teardown();
            if (_driver == null) _driver = GetComponent<TickingDriver>();
            _driver.Initialize(config.DriverConfig);
            if (config.DriverConfig.SoundIds == null || config.DriverConfig.SoundIds.Count != Enum.GetValues(typeof(TickingSound)).Length)
                throw new InvalidOperationException("Ticking sound set must contain tick, winding, stop, wake and key appearance ids.");
            _controller = controller; _shared = shared; _config = config; _hunter = hunter; _player = player;
            _shared.RefreshDormancy();
        }
        public void PublishTick()
        {
            if (_controller == null || !isActiveAndEnabled) return;
            if (_controller.KeyDue)
            {
                for (int i = 0; i < _config.PlacementAttempts && !_controller.HasKey; i++)
                    if (_driver.TrySampleKey(_player.Position, _controller.KeyCandidate(), IsPlayer, out Vector3 position))
                        _controller.PlaceKey(position, true);
                if (!_controller.HasKey) _controller.DeferPlacement();
            }
            if (_controller.HasKey)
            {
                _driver.ShowKey(_controller.KeyPosition, _controller.KeySerial);
                var corners = _driver.QueryGuidance(_player.Position, _controller.KeyPosition, out var direction);
                _controller.SetGuidancePath(corners, direction);
            }
            else _driver.ClearKey();
            PublishClockFacts();
        }
        public void PrepareTick()
        {
            if (_controller == null || !_controller.Dormant || !isActiveAndEnabled) return;
            _controller.SetFollowTarget(_hunter.Position, false);
            for (int i = 0; i < _config.PlacementAttempts; i++)
                if (_driver.TrySampleFollow(_hunter.Position, _controller.FollowCandidate(i), out Vector3 position) &&
                    _controller.SetFollowTarget(position, true)) break;
        }
        private bool IsPlayer(Collider other)
        { IEntityHandle handle = other.GetComponentInParent<IEntityHandle>(); return handle != null && _player != null && handle.Id == _player.Id; }
        private void HandleKey(Collider other, int serial)
        {
            if (_controller == null) return;
            IEntityHandle handle = other.GetComponentInParent<IEntityHandle>();
            if (handle == null || !_controller.TakeKey(handle.Id, serial)) return;
            _shared.RefreshDormancy(); _driver.ClearKey(); PublishClockFacts();
        }
        private void PublishClockFacts()
        {
            while (_controller.TakeSound(out TickingSoundFact fact))
            {
                // Waking gives one explicit clue; subsequent sight loss and search
                // use the shared four-tunable pursuit, not permanent omniscience.
                if (fact.Sound == TickingSound.Wake)
                    _shared.ReceiveHint(new HintPayload(Id, _player.Id, fact.Tick, fact.Tick, _player.Position, 0f, 0f, 1f));
                OnSound?.Invoke(new TickingSoundFact(fact.Sound, fact.Position, fact.Interval, fact.Tick,
                    Id, _config.DriverConfig.SoundIds[(int)fact.Sound]));
            }
            while (_controller.TakeNoise(out NoiseEvent noise)) OnNoise?.Invoke(new TickingNoiseFact(Id, noise));
            OnGuidance?.Invoke(new TickingGuidanceFact(_controller.Guidance, _controller.GuidanceValid, _hunter.Tick));
        }
        public void Teardown()
        {
            if (_controller != null) OnGuidance?.Invoke(new TickingGuidanceFact(_controller.Guidance, false, _hunter.Tick));
            if (_driver != null) _driver.ClearKey();
            _controller = null; _shared = null; _config = null; _hunter = null; _player = null;
        }
        private void OnDestroy() { Teardown(); }
    }
}
