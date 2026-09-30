// ============================================================================
// PostFXUpgradeTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the catalogue-only upgrades through the production PostFX presenter.
//   Explicit time verifies finite reveals and blindness duration without rendering.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · PostFX.
// KEY RESPONSIBILITIES:
//   - Verify exact identities, one-shot duration scaling and independent cleanup.
//   - Verify Glimpse press edges, expiry, release, removal and catch suppression.
// DEPENDENCIES:
//   NUnit, Core and PostFX; temporary configuration only.
// USAGE NOTES:
//   Pure logic fixture; renderer integration remains a coordinator Unity check.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.PostFX;
namespace Worsen.Tests.PostFX
{
    public sealed class PostFXUpgradeTests
    {
        private PostFXDriverConfig config;
        private PostFXDriverState state;
        private PostFXPresenter presenter;
        [SetUp] public void Setup()
        { config = ScriptableObject.CreateInstance<PostFXDriverConfig>(); state = new PostFXDriverState(); presenter = new PostFXPresenter(); }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(config);
        private void Effects(params string[] ids) => presenter.SetActiveEffects(state,
            new ActiveEffects(System.Array.ConvertAll(ids, id => new ActiveEffect(new EffectId(id), EffectKind.Upgrade, 1))));

        [TestCase("mirror-skin", 2f)]
        [TestCase("Mirror-Skin", 4f)]
        [TestCase("glimpse", 4f)]
        public void MirrorSkinScalesEachApplicationOnce(string id, float expected)
        {
            Effects(id); presenter.SetBlindness(state, 4f, config);
            Assert.That(state.BlindnessRemaining, Is.EqualTo(expected));
            Effects(id); presenter.Tick(state, config, .5f);
            Assert.That(state.BlindnessRemaining, Is.EqualTo(expected - .5f));
            presenter.SetBlindness(state, 4f, config);
            Assert.That(state.BlindnessRemaining, Is.EqualTo(expected));
            Effects(); presenter.Tick(state, config, expected);
            Assert.That(state.Blackout, Is.Zero);
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)] [TestCase(0f)]
        public void MirrorSkinRejectsInvalidDurations(float seconds)
        { Effects("mirror-skin"); presenter.SetBlindness(state, seconds, config); Assert.That(state.BlindnessRemaining, Is.Zero); }
        [Test] public void CleanseStillClearsTimedAndCatalogueBlindnessWithMirrorSkin()
        {
            Effects("mirror-skin", "blinded"); presenter.SetBlindness(state, 4f, config);
            presenter.SetBlindness(state, 0f, config); presenter.Tick(state, config, 0f);
            Assert.That(state.Blackout, Is.Zero);
            presenter.Reset(state); Assert.That(state.ActiveEffects, Is.Null);
        }
        [Test] public void GlimpseRequiresUpgradeAndNewPressAndCannotRefreshWhileHeld()
        {
            presenter.SetLookBack(state, config, true); Assert.That(state.GlimpseRemaining, Is.Zero);
            Effects("glimpse"); presenter.SetLookBack(state, config, true); Assert.That(state.GlimpseRemaining, Is.Zero);
            presenter.SetLookBack(state, config, false); presenter.SetLookBack(state, config, true);
            Assert.That(state.GlimpseRemaining, Is.EqualTo(config.GlimpseSeconds));
            presenter.Tick(state, config, config.GlimpseSeconds);
            presenter.SetLookBack(state, config, true); Assert.That(state.GlimpseRemaining, Is.Zero);
        }
        [TestCase("release")] [TestCase("remove")] [TestCase("hand")] [TestCase("hunter")] [TestCase("reset")]
        public void GlimpseCannotLeakPastItsBoundary(string boundary)
        {
            Effects("glimpse"); presenter.SetLookBack(state, config, true);
            if (boundary == "release") presenter.SetLookBack(state, config, false);
            if (boundary == "remove") Effects();
            if (boundary == "hand") presenter.PlayConsumed(state, 1f);
            if (boundary == "hunter") presenter.SetInjury(state, 0f, 100f);
            if (boundary == "reset") presenter.Reset(state);
            presenter.Tick(state, config, 0f); Assert.That(state.GlimpseRemaining, Is.Zero);
        }

        [Test] public void OrdinaryRimIsOptInRearViewOnlyAndBlindnessNeverRevealsHunters()
        {
            presenter.SetLookBack(state, config, true); Assert.That(presenter.OutlineStrength(state), Is.Zero);
            presenter.SetHunterRim(state, .08f); Assert.That(presenter.OutlineStrength(state), Is.EqualTo(.08f));
            presenter.SetLookBack(state, config, false); Assert.That(presenter.OutlineStrength(state), Is.Zero);
            Effects("glimpse"); presenter.SetLookBack(state, config, true);
            Assert.That(presenter.OutlineStrength(state), Is.EqualTo(1f));
            presenter.SetBlindness(state, 1f, config); presenter.Tick(state, config, 0f);
            Assert.That(presenter.OutlineStrength(state), Is.Zero);
            presenter.Reset(state); Assert.That(state.HunterRim, Is.Zero);
        }
    }
}
