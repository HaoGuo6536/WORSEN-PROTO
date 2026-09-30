// ============================================================================
// FixtureTimeSetUp.cs
// ============================================================================
// PURPOSE:
//   Establishes the test assembly's normal engine clock before any fixture runs.
//   It retains the configured fixed step across Play Mode domain reloads so the
//   fixture action can report clock leaks and repair them without hiding failures.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test support (§11) · Infrastructure (assembly scope).
// KEY RESPONSIBILITIES:
//   - Capture the configured fixed step once and enforce normal time at run edges.
//   - Keep the baseline available across domain reloads and erase it after the run.
// DEPENDENCIES:
//   NUnit, UnityEngine.Time and UnityEditor.SessionState; test-only fixture action.
// USAGE NOTES:
//   Intentionally outside a namespace: NUnit then scopes SetUpFixture to the whole
//   assembly, not one system. It is not a per-fixture hook; FixtureTimeGuard supplies
//   that hook on every fixture. No runtime layer reads or writes this baseline.
// ============================================================================
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Tests.Infrastructure;

[SetUpFixture]
public sealed class FixtureTimeSetUp
{
    internal const string FixedStepKey = "Worsen.Tests.FixtureTimeSetUp.FixedDeltaTime";

    internal static float FixedStep
    {
        get
        {
            float value = SessionState.GetFloat(FixedStepKey, float.NaN);
            Assert.That(!float.IsNaN(value) && !float.IsInfinity(value) && value > 0f,
                Is.True, "FixtureTimeSetUp must establish a valid fixedDeltaTime baseline before fixtures run.");
            return value;
        }
    }

    [OneTimeSetUp]
    public void BeforeAssembly()
    {
        float fixedStep = Time.fixedDeltaTime;
        Assert.That(!float.IsNaN(fixedStep) && !float.IsInfinity(fixedStep) && fixedStep > 0f,
            Is.True, "The configured Time.fixedDeltaTime must be finite and positive.");
        SessionState.SetFloat(FixedStepKey, fixedStep);
        FixtureTimeGuardAttribute.AssertAndRestore("Worsen.Tests (assembly)", "before assembly");
    }

    [OneTimeTearDown]
    public void AfterAssembly()
    {
        try { FixtureTimeGuardAttribute.AssertAndRestore("Worsen.Tests (assembly)", "after assembly"); }
        finally { SessionState.EraseFloat(FixedStepKey); }
    }
}
