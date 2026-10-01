// ============================================================================
// FixtureTimeSetUpTests.cs
// ============================================================================
// PURPOSE:
//   Verifies that the assembly clock baseline and fixture action cooperate to
//   expose a leaking fixture without freezing the rest of the NUnit run. These
//   Edit Mode checks deliberately leak time only inside an asserted guard call.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Infrastructure.
// KEY RESPONSIBILITIES:
//   - Require the leaking fixture's full name and observed values in the failure.
//   - Verify both globals are repaired before the next fixture boundary runs.
//   - Cover clean boundaries, incoming contamination and fixed-step-only leaks.
//   - Check that every real fixture carries the guard and focus gates are categorized.
// DEPENDENCIES:
//   NUnit fixture metadata, UnityEngine.Time, reflection and test infrastructure.
// USAGE NOTES:
//   Synthetic fixture metadata has no Test methods, so no intentionally failing
//   fixture is discovered in the ordinary suite. Every fault injection has finally
//   cleanup. Native Unity execution remains the coordinator's verification gate.
// ============================================================================
using System;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using UnityEngine;

namespace Worsen.Tests.Infrastructure
{
    [FixtureTimeGuard, Timeout(30000)]
    public sealed class FixtureTimeSetUpTests
    {
        [Test]
        public void LeakingFixtureIsReportedByNameAndRestoredBeforeTheNextFixture()
        {
            var guard = new FixtureTimeGuardAttribute();
            var leaker = new TestFixture(new TypeWrapper(typeof(TimeScaleLeakingFixture)));
            var next = new TestFixture(new TypeWrapper(typeof(FollowingFixture)));
            float fixedStep = FixtureTimeSetUp.FixedStep;
            try
            {
                guard.BeforeTest(leaker);
                Time.timeScale = 0f;
                Time.fixedDeltaTime = fixedStep * 2f;
                var error = Assert.Throws<AssertionException>(() => guard.AfterTest(leaker));
                Assert.That(error.Message, Does.Contain(leaker.FullName).And.Contain("after fixture")
                    .And.Contain("Time.timeScale=0").And.Contain("Time.fixedDeltaTime="));
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep));
                Assert.DoesNotThrow(() => guard.BeforeTest(next));
                Assert.DoesNotThrow(() => guard.AfterTest(next));
            }
            finally { Time.timeScale = 1f; Time.fixedDeltaTime = fixedStep; }
        }

        [Test]
        public void FixedStepOnlyLeakIsAlsoNamedAndRepaired()
        {
            var guard = new FixtureTimeGuardAttribute();
            var fixture = new TestFixture(new TypeWrapper(typeof(TimeScaleLeakingFixture)));
            float fixedStep = FixtureTimeSetUp.FixedStep;
            try
            {
                guard.BeforeTest(fixture);
                Time.fixedDeltaTime = fixedStep * 0.5f;
                var error = Assert.Throws<AssertionException>(() => guard.AfterTest(fixture));
                Assert.That(error.Message, Does.Contain(fixture.FullName).And.Contain("Time.fixedDeltaTime="));
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep));
            }
            finally { Time.timeScale = 1f; Time.fixedDeltaTime = fixedStep; }
        }

        [Test]
        public void IncomingPauseFailsBeforeFixtureAndRestoresBothGlobals()
        {
            var guard = new FixtureTimeGuardAttribute();
            var fixture = new TestFixture(new TypeWrapper(typeof(FollowingFixture)));
            float fixedStep = FixtureTimeSetUp.FixedStep;
            try
            {
                Time.timeScale = 0f;
                Time.fixedDeltaTime = fixedStep * 2f;
                var error = Assert.Throws<AssertionException>(() => guard.BeforeTest(fixture));
                Assert.That(error.Message, Does.Contain(fixture.FullName).And.Contain("before fixture"));
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedStep));
            }
            finally { Time.timeScale = 1f; Time.fixedDeltaTime = fixedStep; }
        }

        [Test]
        public void EveryDiscoveredFixtureHasAClockGuard()
        {
            var assembly = new DefaultTestAssemblyBuilder().Build(GetType().Assembly,
                new System.Collections.Generic.Dictionary<string, object>());
            CheckGuards(assembly);
        }

        private static void CheckGuards(ITest test)
        {
            if (test is TestFixture)
                Assert.That(test.TypeInfo.GetCustomAttributes<FixtureTimeGuardAttribute>(true), Is.Not.Empty, test.FullName);
            foreach (ITest child in test.Tests) CheckGuards(child);
        }

        [Test]
        public void KnownCaptureGateFixturesHaveRequiresFocusCategory()
        {
            Type[] fixtures =
            {
                typeof(Camera.CriticalHealthFeedbackIntegrationTests), typeof(Camera.FeedbackRoutingIntegrationTests),
                typeof(Hunter.HunterChaseIntegrationTests), typeof(Hunter.HunterCutOffIntegrationTests),
                typeof(Hunter.HunterRouteComparisonTests), typeof(Player.LookBackTraversalIntegrationTests),
                typeof(Player.PlayerTraversalIntegrationTests), typeof(Floor.FloorOpposedCaptureTests),
                typeof(Level.TagArenaDesignSpeedAcceptanceTests), typeof(Player.PlayerContinuousChainAcceptanceTests)
            };
            foreach (Type fixture in fixtures)
                Assert.That(fixture.GetCustomAttributes(typeof(CategoryAttribute), true).Cast<CategoryAttribute>()
                    .Select(attribute => attribute.Name), Does.Contain("RequiresFocus"), fixture.FullName);
        }

        private sealed class TimeScaleLeakingFixture { }
        private sealed class FollowingFixture { }
    }
}
