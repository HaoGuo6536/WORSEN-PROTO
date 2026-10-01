// ============================================================================
// ProceduralNavFallbackGenerationTests.cs
// ============================================================================
// PURPOSE:
//   Verifies bounded template-to-organic recovery and fail-closed organic failure.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Preserve failed seeds/reasons without declaring a validated organic floor absent.
//   - Require organic exhaustion before the terminal no-floor state after templates.
// DEPENDENCIES:
//   - NUnit and Domain.Procedural only.
// USAGE NOTES:
//   Fail inputs model physical rejection; this fixture does not run a native bake.
// ============================================================================
using System;
using NUnit.Framework;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralNavFallbackGenerationTests
    {
        [TestCase(0)] [TestCase(3)] [TestCase(8)]
        public void ExhaustedTemplatesEnterBoundedOrganicRecoveryInsteadOfNoFloor(int retries)
        {
            var state = new ProceduralBehaviorState(); var generation = new ProceduralGenerationController(state);
            generation.Begin(133745427, 2, retries);
            for (int i = 0; i <= retries; i++)
                Assert.That(generation.Fail("unreachable|hunter " + i, "template-" + i, true), Is.True);
            Assert.That(state.UsedFallback, Is.False); Assert.That(state.GenerationSucceeded, Is.False);
            Assert.That(state.IsReady, Is.False); Assert.That(state.Layout, Is.Null);
            Assert.That(state.AttemptSeed, Is.EqualTo(133745427)); Assert.That(state.AttemptIndex, Is.Zero);
            Assert.That(state.GenerationManifest, Does.Not.Contain("NoFloor"));
            Assert.That(state.OrganicFallbackReason, Is.EqualTo("template-attempts-exhausted:unreachable|hunter " + retries));
            Assert.That(state.GenerationManifest, Does.Contain(Uri.EscapeDataString(state.OrganicFallbackReason)));
            generation.Succeed("organic-layout");
            Assert.That(state.GenerationSucceeded, Is.True); Assert.That(state.UsedFallback, Is.False);
            Assert.That(state.GenerationManifest, Does.Contain("fallback=Organic|generationSucceeded=true"));
            Assert.That(state.GenerationManifest, Does.Contain("layout=template-0"));
            Assert.That(state.GenerationManifest, Does.Contain("result=validated,layout=organic-layout"));
            Assert.That(state.IsReady, Is.False, "Only the Manager can expose a physically admitted map.");
            Assert.Throws<InvalidOperationException>(() => generation.Fail("late", null, true));
            generation.Begin(1, 1, 0);
            Assert.That(state.OrganicFallbackReason, Is.Empty); Assert.That(state.TemplateFailureReason, Is.Empty);
            generation.Succeed("fresh-template");
            Assert.That(state.GenerationManifest, Does.Contain("fallback=none"));
        }

        [TestCase(int.MaxValue)] [TestCase(int.MinValue)]
        public void OrganicFailureMustExhaustItsOwnSeedSequenceBeforeNoFloor(int seed)
        {
            var state = new ProceduralBehaviorState(); var generation = new ProceduralGenerationController(state);
            generation.Begin(seed, 2, 3);
            for (int i = 0; i <= 3; i++) Assert.That(generation.Fail("template-nav", "template", true), Is.True);
            for (int i = 0; i <= 3; i++)
            {
                Assert.That(state.AttemptSeed, Is.EqualTo(unchecked(seed + i * ProceduralGenerationController.SeedStride)));
                Assert.That(generation.Fail("organic-nav", "organic"), Is.EqualTo(i < 3));
            }
            Assert.That(state.GenerationSucceeded, Is.False); Assert.That(state.UsedFallback, Is.True);
            Assert.That(state.IsReady, Is.False); Assert.That(state.Layout, Is.Null);
            Assert.That(state.GenerationManifest, Does.Contain("stage=organic"));
            Assert.That(state.GenerationManifest, Does.Contain("fallback=NoFloorAwaitingSession|generationSucceeded=false"));
            Assert.Throws<InvalidOperationException>(() => generation.Fail("unbounded", null, true));
            Assert.Throws<InvalidOperationException>(() => generation.Succeed("unadmitted"));
        }
    }
}
