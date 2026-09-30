// ============================================================================
// HorrorCollapsePresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies per-floor deep darkness without camera, render assets or gameplay ticks.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Presentation · Horror.
// KEY RESPONSIBILITIES:
//   - Assert phase weighting, duplicate/unknown/exit rejection and monotonic outputs.
//   - Check injected smoothing, multiplicative darkness and new-floor reset.
// DEPENDENCIES:
//   NUnit, Core room values and Horror's pure presenters/config/state.
// USAGE NOTES:
//   Edit Mode; only a transient config is allocated and always destroyed.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Horror;

namespace Worsen.Tests.Horror
{
    public sealed class HorrorCollapsePresenterTests
    {
        private HorrorDriverConfig _config;
        private HorrorDriverState _state;
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<HorrorDriverConfig>();
            _state = new HorrorDriverState();
            HorrorCollapsePresenter.BeginFloor(_state, new[] { Room(1), Room(2), Room(3), Room(3) }, 3);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(_config);
        [Test] public void PhasesNotLocalProgressDefineFractionAndExitCannotChangeIt()
        {
            Observe(1, RoomPhase.Telegraph, 1f); Observe(2, RoomPhase.Telegraph, 1f);
            Assert.That(_state.CollapseFraction, Is.Zero);
            Observe(1, RoomPhase.Closed); Observe(2, RoomPhase.Encroaching, 0f);
            Assert.That(_state.CollapseFraction, Is.EqualTo(.75f));
            Observe(2, RoomPhase.Encroaching, 1f); Observe(3, RoomPhase.Closed); Observe(999, RoomPhase.Closed);
            Assert.That(_state.CollapseFraction, Is.EqualTo(.75f));
            Observe(2, RoomPhase.Tearing);
            Assert.That(_state.CollapseFraction, Is.EqualTo(.75f), "Tearing cannot undo encroachment darkness.");
            Observe(2, RoomPhase.Closed); Assert.That(_state.CollapseFraction, Is.EqualTo(1f));
        }
        [Test] public void FogAndTorchesDieMonotonicallyWithInjectedTimeAndComposeWithDarkerFloors()
        {
            var effects = new ActiveEffects(new[] { new ActiveEffect(new EffectId("darker-floors"), EffectKind.Curse, 1) });
            new HorrorPresenter().SetActiveEffects(_state, _config, effects);
            float darker = _state.TorchCountMultiplier;
            Assert.That(darker, Is.LessThan(1f));
            Assert.That(HorrorCollapsePresenter.FogNear(_state, _config), Is.EqualTo(24f));
            Observe(1, RoomPhase.Closed); Observe(2, RoomPhase.Closed);
            Assert.That(HorrorCollapsePresenter.Tick(_state, _config, float.NaN), Is.False);
            Assert.That(HorrorCollapsePresenter.Tick(_state, _config, -1f), Is.False);
            Assert.That(HorrorCollapsePresenter.Tick(_state, _config, 0f), Is.False);
            float near = 24f, torches = darker;
            for (int i = 0; i < 8; i++)
            {
                HorrorCollapsePresenter.Tick(_state, _config, .25f);
                float nextNear = HorrorCollapsePresenter.FogNear(_state, _config);
                float nextTorches = HorrorCollapsePresenter.TorchMultiplier(_state, _config);
                Assert.That(nextNear, Is.LessThanOrEqualTo(near));
                Assert.That(nextTorches, Is.LessThanOrEqualTo(torches));
                near = nextNear; torches = nextTorches;
            }
            Assert.That(near, Is.EqualTo(10f)); Assert.That(torches, Is.EqualTo(darker * .4f).Within(.0001f));
            var settings = _config.Settings; settings.FogNearMeters = near;
            new HorrorPresenter().CalculateAtmosphere(_state, settings, 100f);
            Assert.That(_state.FogCurveStart, Is.EqualTo(.1f * _config.DarkerFogDistanceMultiplier).Within(.0001f));
            HorrorCollapsePresenter.BeginFloor(_state, new[] { Room(7), Room(8) }, 8);
            Observe(1, RoomPhase.Closed);
            Assert.That(_state.CollapseFraction, Is.Zero); Assert.That(_state.SmoothedCollapseFraction, Is.Zero);
            Assert.That(HorrorCollapsePresenter.FogNear(_state, _config), Is.EqualTo(24f));
            Assert.That(HorrorCollapsePresenter.TorchMultiplier(_state, _config), Is.EqualTo(darker));
            new HorrorPresenter().ResetRound(_state);
            Assert.That(_state.CollapseRooms, Is.Empty); Assert.That(_state.HasCollapseFloor, Is.False);
        }
        [Test] public void EmptyOrOnlyExitFloorStaysReadable()
        {
            HorrorCollapsePresenter.BeginFloor(_state, new[] { Room(3) }, 3);
            Observe(3, RoomPhase.Closed);
            Assert.That(_state.CollapseFraction, Is.Zero);
            Assert.That(HorrorCollapsePresenter.Fraction(null, .5f), Is.Zero);
        }
        private void Observe(int id, RoomPhase phase, float progress = 1f)
            => HorrorCollapsePresenter.Observe(_state, new RoomDestructionSample(id, phase, progress), _config);
        private static GeneratedRoomSample Room(int id) => new GeneratedRoomSample(id, default, false, false, null);
    }
}
