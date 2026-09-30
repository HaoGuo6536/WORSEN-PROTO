// ============================================================================
// FixtureTimeGuardAttribute.cs
// ============================================================================
// PURPOSE:
//   Checks the engine clock at each NUnit fixture boundary, including pure tests.
//   A leaked pause or fixed step fails the responsible fixture by its full name;
//   restoration happens before asserting so subsequent fixtures can still execute.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test support (§11) · Infrastructure.
// KEY RESPONSIBILITIES:
//   - Wrap fixture setup and teardown with named time-scale and fixed-step checks.
//   - Restore both engine globals before reporting a failed boundary check.
// DEPENDENCIES:
//   NUnit suite actions, UnityEngine.Time and the assembly FixtureTimeSetUp baseline.
// USAGE NOTES:
//   Apply to every test fixture, not methods. NUnit's assembly SetUpFixture alone
//   cannot wrap individual fixtures. No baseline is held in an attribute instance
//   or static field: Unity domain reloads reconstruct them during integration tests.
//   Global clock writes are owned only by this test boundary, never runtime logic.
// ============================================================================
using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine;

namespace Worsen.Tests.Infrastructure
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class FixtureTimeGuardAttribute : NUnitAttribute, ITestAction
    {
        public ActionTargets Targets => ActionTargets.Suite;

        // Outside the Unity runtime (the headless pure-test tier) engine calls throw, so the
        // guard is a no-op there; inside Unity it always enforces the clock.
        private static bool? engineAvailable;
        internal static bool EngineAvailable
        {
            get
            {
                if (engineAvailable.HasValue) return engineAvailable.Value;
                try { engineAvailable = ProbeEngine(); }
                catch (Exception) { engineAvailable = false; }
                return engineAvailable.Value;
            }
        }

        // Separate, non-inlined method so a missing engine binding throws inside the caller's try.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static bool ProbeEngine() => Time.timeScale >= 0f;

        public void BeforeTest(ITest test)
        {
            if (EngineAvailable && IsFixture(test)) AssertAndRestore(test.FullName, "before fixture");
        }

        public void AfterTest(ITest test)
        {
            if (EngineAvailable && IsFixture(test)) AssertAndRestore(test.FullName, "after fixture");
        }

        private static bool IsFixture(ITest test) => test.IsSuite && test.TypeInfo != null && test.Method == null;

        internal static void AssertAndRestore(string fixtureName, string boundary)
        {
            float expectedStep = FixtureTimeSetUp.FixedStep;
            float actualScale = Time.timeScale;
            float actualStep = Time.fixedDeltaTime;
            // Repair first, even when both checks fail. Exact equality is deliberate:
            // these are global settings, not accumulated floating-point calculations.
            Time.timeScale = 1f;
            Time.fixedDeltaTime = expectedStep;
            if (actualScale != 1f || actualStep != expectedStep)
                Assert.Fail(FormattableString.Invariant(
                    $"Clock leak {boundary}: fixture '{fixtureName}' left Time.timeScale={actualScale:R} (expected 1), Time.fixedDeltaTime={actualStep:R} (expected {expectedStep:R}). Both values were restored before this failure."));
        }
    }
}
