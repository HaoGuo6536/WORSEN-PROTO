// ============================================================================
// PureTestSelfTests.cs
// ============================================================================
// PURPOSE:
//   Provides deliberate pass, failure, unavailable-engine and exclusion cases
//   for the offline runner. This file is compiled only into the separate harness
//   self-test assembly, never Worsen.Tests or the Unity project.
// ARCHITECTURAL ROLE:
//   Offline verification test fixture · no runtime layer · integration tooling.
// KEY RESPONSIBILITIES:
//   - Exercise NUnit parameter sources, expected results and inherited hooks.
//   - Distinguish ordinary failures from actual engine-dependent exceptions.
//   - Verify skip, lifecycle failure and process timeout reporting.
// DEPENDENCIES:
//   - Existing NUnit and Unity managed assemblies, plus PureTestPolicy.
// USAGE NOTES:
//   Failure and timeout cases are intentional. The self-test verifier checks
//   each named outcome; exit 1 from the underlying runner is expected.
// ============================================================================
using System;
using System.Collections;
using System.IO;
using System.Security;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OfflineHarness.SelfTest
{
    public abstract class LifecycleBase
    {
        protected int Value;
        protected bool Ready;
        [OneTimeSetUp] public void Start() { Ready = true; }
        [SetUp] public void Before() { Assert.That(Ready, Is.True); Value = 5; }
        [TearDown] public void After() { Assert.That(Value, Is.EqualTo(6)); }
        [OneTimeTearDown] public void End() { Assert.That(Ready, Is.True); Ready = false; }
    }

    public sealed class Cases : LifecycleBase
    {
        private static IEnumerable FieldSource = new[] { new TestCaseData(3).Returns(6) };
        private static IEnumerable PropertySource => new[] { new TestCaseData(4).Returns(8) };
        private static IEnumerable MethodSource() { yield return new TestCaseData(5).Returns(10); }
        [SetUp] public void DerivedBefore() { Assert.That(Value, Is.EqualTo(5)); Value++; }
        [Test] public void Pass() { Assert.That(Value, Is.EqualTo(6)); }
        [TestCase(2, ExpectedResult = 4)] public int Expected(int value) => value * 2;
        [TestCase(2, ExpectedResult = 99)] public int WrongExpected(int value) => value * 2;
        [TestCaseSource(nameof(FieldSource))] public int Field(int value) => value * 2;
        [TestCaseSource(nameof(PropertySource))] public int Property(int value) => value * 2;
        [TestCaseSource(nameof(MethodSource))] public int Method(int value) => value * 2;
        [Test] public void Values([Values(1, 2)] int value) { Assert.That(value, Is.InRange(1, 2)); }
        [Test] public void AssertionFail() { Assert.Fail("deliberate assertion"); }
        [Test] public void OrdinaryNull() { object value = null; value.ToString(); }
        [Test] public void OrdinaryMissingMethod() { throw new MissingMethodException("ordinary application missing method"); }
        [Test] public void OrdinarySecurity() { throw new SecurityException("ordinary permission failure"); }
        [Test] public void MissingFile() { throw new FileNotFoundException("ordinary missing input"); }
        [Test] public void Engine() { _ = new GameObject("offline-self-test"); }
        [Test] public void Editor() { _ = UnityEditor.EditorApplication.isPlaying; }
        [Test, Ignore("deliberate")] public void Ignored() { Assert.Fail("must not run"); }
        [Test, Explicit] public void Explicit() { Assert.Fail("must not run"); }
        [Test, Category("RequiresFocus")] public void Focus() { Assert.Fail("must not run"); }
        [UnityTest] public IEnumerator Coroutine() { Assert.Fail("must not run"); yield break; }
        [Test] public void Timeout() { Thread.Sleep(System.Threading.Timeout.Infinite); }
        [Test] public void ZAfterTimeout() { Assert.Pass(); }
        [Test] public void ClassifierBoundaries()
        {
            Assert.That(PureTestPolicy.IsEnvironment("System.Security.SecurityException : ECall methods must be packaged into a system module.", "   at UnityEngine.Object.NativeCall()", "Error"), Is.True);
            Assert.That(PureTestPolicy.IsEnvironment("System.Security.SecurityException : ECall methods must be packaged into a system module.", "   at OfflineHarness.Cases.Test()", "Error"), Is.True);
            Assert.That(PureTestPolicy.IsEnvironment("System.MissingMethodException : InternalCall unavailable", "   at UnityEngine.Object.NativeCall()", "Error"), Is.True);
            Assert.That(PureTestPolicy.IsEnvironment("System.Security.SecurityException : ECall from application", "   at OfflineHarness.Cases.Test()", "Error"), Is.False);
            Assert.That(PureTestPolicy.IsEnvironment("System.InvalidOperationException : UnityEditor.fake", "   at OfflineHarness.Cases.Test()", "Error"), Is.False);
            Assert.That(PureTestPolicy.IsEnvironment("UnityEngine.UnityException : can only be called from the main thread", "", "Error"), Is.True);
            Assert.That(PureTestPolicy.IsEnvironment("UnityEngine.UnityException : Editor unavailable", "", "Error"), Is.True);
            Assert.That(PureTestPolicy.IsEnvironment("UnityEngine.UnityException : invalid value", "", "Error"), Is.False);
            Assert.That(PureTestPolicy.IsEnvironment("System.NullReferenceException : absent", "   at UnityEngine.Object.Method()\n   at OfflineHarness.Cases.Test()", "Error"), Is.True);
            Assert.That(PureTestPolicy.IsEnvironment("System.NullReferenceException : absent", "   at OfflineHarness.Cases.Test()\n   at UnityEngine.Object.Method()", "Error"), Is.False);
            Assert.That(PureTestPolicy.IsEnvironment("System.Security.SecurityException : denied", "", "Error"), Is.False);
            Assert.That(PureTestPolicy.IsEnvironment("System.MissingMethodException : missing", "", "Error"), Is.False);
            Assert.That(PureTestPolicy.IsEnvironment("assertion\nTearDown : System.Security.SecurityException : ECall unavailable", "", "Error"), Is.False);
        }
    }

    [Ignore("whole fixture")] public sealed class IgnoredFixture
    {
        [Test] public void Never() { Assert.Fail("must not run"); }
    }
    [Explicit] public sealed class ExplicitFixture
    {
        [Test] public void Never() { Assert.Fail("must not run"); }
    }
    [Category("RequiresFocus")] public sealed class FocusFixture
    {
        [Test] public void Never() { Assert.Fail("must not run"); }
    }
    public sealed class BadOneTimeSetup
    {
        [OneTimeSetUp] public void Start() { throw new InvalidOperationException("deliberate one-time setup"); }
        [Test] public void Never() { Assert.Fail("must not run"); }
    }
    public sealed class EngineOneTimeSetup
    {
        [OneTimeSetUp] public void Start() { _ = new GameObject("engine-setup"); }
        [Test] public void Never() { Assert.Fail("must not run"); }
    }
    public sealed class BadOneTimeTeardown
    {
        [Test] public void Pass() { }
        [OneTimeTearDown] public void End() { Assert.Fail("deliberate one-time teardown"); }
    }
    public sealed class EngineOneTimeTeardown
    {
        [Test] public void Pass() { }
        [OneTimeTearDown] public void End() { _ = new GameObject("engine-teardown"); }
    }
    public sealed class MixedFailure
    {
        [Test] public void Fail() { Assert.Fail("must remain a failure despite engine teardown"); }
        [TearDown] public void End() { _ = new GameObject("mixed-teardown"); }
    }
    public sealed class TimeoutOneTimeSetup
    {
        [OneTimeSetUp] public void Start() { Thread.Sleep(System.Threading.Timeout.Infinite); }
        [Test] public void Never() { Assert.Fail("must not run"); }
    }
    public sealed class TimeoutOneTimeTeardown
    {
        [Test] public void Pass() { }
        [OneTimeTearDown] public void End() { Thread.Sleep(System.Threading.Timeout.Infinite); }
    }
    public sealed class TimeoutSetup
    {
        [SetUp] public void Start() { Thread.Sleep(System.Threading.Timeout.Infinite); }
        [Test] public void Never() { Assert.Fail("must not run"); }
    }
    public sealed class TimeoutTeardown
    {
        [Test] public void Pass() { }
        [TearDown] public void End() { Thread.Sleep(System.Threading.Timeout.Infinite); }
    }
    public sealed class UnityLifecycle
    {
        [UnitySetUp] public IEnumerator Start() { yield return null; }
        [Test] public void Never() { Assert.Fail("must not run without UnitySetUp"); }
    }
    public sealed class MixedOneTimeEngineTeardown
    {
        [Test] public void Fail() { Assert.Fail("assertion must survive one-time engine teardown"); }
        [OneTimeTearDown] public void End() { _ = new GameObject("mixed-one-time"); }
    }
    public sealed class MixedOneTimeAssertionTeardown
    {
        [Test] public void Engine() { _ = new GameObject("mixed-one-time-test"); }
        [OneTimeTearDown] public void End() { Assert.Fail("assertion must survive engine test failure"); }
    }
    public sealed class BadSource
    {
        private static IEnumerable Source() { throw new InvalidOperationException("deliberate invalid source"); }
        [TestCaseSource(nameof(Source))] public void Never(int value) { Assert.Fail("must not run"); }
    }
    public sealed class EngineSource
    {
        private static IEnumerable Source() { _ = new GameObject("engine-source"); return new[] { 1 }; }
        [TestCaseSource(nameof(Source))] public void Never(int value) { Assert.Fail("must not run"); }
    }
}
