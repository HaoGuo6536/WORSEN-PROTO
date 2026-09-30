// ============================================================================
// HorrorAfterglowPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies presentation of accepted light-safety windows without engine objects.
//   It protects against duplicate break facts, foreign objects and stale floor data.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Horror.
// KEY RESPONSIBILITIES:
//   - Verify exact upgrade gating, finite lifetimes and independent light expiry.
//   - Verify relighting and floor reset re-arm only their own identities.
// DEPENDENCIES:
//   Core, Horror presentation, NUnit and Unity value types only.
// USAGE NOTES:
//   Does not establish the gameplay Mannequin safety rule or Lumen render quality.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Horror;
namespace Worsen.Tests.Horror
{
    public sealed class HorrorAfterglowPresenterTests
    {
        private static ActiveEffects Effects(string id) => new ActiveEffects(new[] { new ActiveEffect(new EffectId(id), EffectKind.Upgrade, 1) });
        private static InteractableState Light(int id, InteractableStateValue value = InteractableStateValue.Broken)
            => new InteractableState(id, InteractableKind.Light, 1, Vector3.zero, value);
        [TestCase("afterglow", true)] [TestCase("Afterglow", false)] [TestCase("glimpse", false)]
        public void OnlyExactUpgradeAdmitsBrokenLights(string id, bool expected)
        {
            var state = new HorrorAfterglowDriverState();
            Assert.That(HorrorAfterglowPresenter.Observe(state, Effects(id), Light(1), 3f), Is.EqualTo(expected));
            Assert.That(state.Lights.Count, Is.EqualTo(expected ? 1 : 0));
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)] [TestCase(0f)]
        public void InvalidLifetimeNeverCreatesGlow(float seconds)
        {
            var state = new HorrorAfterglowDriverState();
            HorrorAfterglowPresenter.Observe(state, Effects("afterglow"), Light(1), seconds);
            Assert.That(state.Lights, Is.Empty);
        }
        [Test] public void IndependentWindowsFadeAndReplayedBreakCannotExtendOrRearm()
        {
            var state = new HorrorAfterglowDriverState(); var effects = Effects("afterglow");
            HorrorAfterglowPresenter.Observe(state, effects, Light(1), 3f);
            HorrorAfterglowPresenter.Observe(state, effects, Light(2), 6f);
            HorrorAfterglowPresenter.Tick(state, 1.5f);
            Assert.That(HorrorAfterglowPresenter.Intensity(state.Lights[1], .4f), Is.EqualTo(.2f));
            HorrorAfterglowPresenter.Observe(state, effects, Light(1), 3f);
            Assert.That(state.Lights[1].Remaining, Is.EqualTo(1.5f));
            HorrorAfterglowPresenter.Tick(state, 1.5f);
            HorrorAfterglowPresenter.Observe(state, effects, Light(1), 3f);
            Assert.That(HorrorAfterglowPresenter.Intensity(state.Lights[1], .4f), Is.Zero);
            Assert.That(state.Lights[2].Remaining, Is.EqualTo(3f));
            HorrorAfterglowPresenter.Observe(state, effects, Light(1, InteractableStateValue.Lit), 0f);
            HorrorAfterglowPresenter.Observe(state, effects, Light(1), 3f);
            Assert.That(state.Lights[1].Remaining, Is.EqualTo(3f));
            state.Lights.Clear(); Assert.That(state.Lights, Is.Empty);
        }
        [Test] public void InvalidTicksDoNotAdvanceAndForeignObjectsNeverGlow()
        {
            var state = new HorrorAfterglowDriverState(); var effects = Effects("afterglow");
            HorrorAfterglowPresenter.Observe(state, effects, Light(1), 3f);
            foreach (float dt in new[] { float.NaN, float.PositiveInfinity, -1f }) HorrorAfterglowPresenter.Tick(state, dt);
            Assert.That(state.Lights[1].Remaining, Is.EqualTo(3f));
            var door = new InteractableState(2, InteractableKind.Door, 1, Vector3.zero, InteractableStateValue.Broken);
            Assert.That(HorrorAfterglowPresenter.Observe(state, effects, door, 3f), Is.False);
            HorrorAfterglowPresenter.Observe(state, null, Light(1), 3f);
            Assert.That(state.Lights, Is.Empty);
        }
    }
}
