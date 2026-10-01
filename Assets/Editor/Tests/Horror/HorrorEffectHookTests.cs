// ============================================================================
// HorrorEffectHookTests.cs
// ============================================================================
// PURPOSE:
//   Verifies catalogue hooks remain neutral unless their exact configured id is active.
//   Every hook is checked against unrelated outputs so one effect cannot enable another.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Horror.
// KEY RESPONSIBILITIES:
//   - Verify fog, torch-count and Wick isolation, removal and neutral run reset.
// DEPENDENCIES:
//   - Core effects, HorrorPresenter and NUnit.
// USAGE NOTES:
//   Pure Edit Mode; actual Environment torch budgeting requires the owning system's route.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Horror;

namespace Worsen.Tests.Horror
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HorrorEffectHookTests
    {
        [TestCase("darker-floors", true, false, false)]
        [TestCase("cat-eyes", false, true, false)]
        [TestCase("wick", false, false, true)]
        [TestCase("blinded", false, false, false)]
        [TestCase("Darker-Floors", false, false, false)]
        public void ExactIdsChangeOnlyTheirOwnOutputs(string id, bool dark, bool eyes, bool wick)
        {
            var config = ScriptableObject.CreateInstance<HorrorDriverConfig>();
            try
            {
                var p = new HorrorPresenter(); var s = new HorrorDriverState();
                p.SetActiveEffects(s, config, null);
                Assert.That(s.TorchCountMultiplier, Is.EqualTo(1f)); Assert.That(s.Wick, Is.False);
                p.SetActiveEffects(s, config, new ActiveEffects(new[] { new ActiveEffect(new EffectId(id), EffectKind.Curse, 1) }));
                Assert.That(s.HookFogDistanceMultiplier, Is.EqualTo(dark ? config.DarkerFogDistanceMultiplier : 1f));
                Assert.That(s.HookFogStartMultiplier, Is.EqualTo(eyes ? config.CatEyesFogStartMultiplier : 1f));
                Assert.That(s.TorchCountMultiplier, Is.EqualTo(dark ? config.DarkerTorchCountMultiplier : 1f));
                Assert.That(s.Wick, Is.EqualTo(wick));
                Assert.That(s.FlashlightMultiplier, Is.EqualTo(1f));
                p.SetActiveEffects(s, config, default(ActiveEffects));
                Assert.That(s.HookFogStartMultiplier + s.HookFogDistanceMultiplier + s.TorchCountMultiplier, Is.EqualTo(3f));
                Assert.That(s.Wick, Is.False);
                p.ResetRun(s); Assert.That(s.ActiveEffects, Is.Null);
            }
            finally { Object.DestroyImmediate(config); }
        }
    }
}
