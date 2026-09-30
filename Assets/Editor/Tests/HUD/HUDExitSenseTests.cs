// ============================================================================
// HUDExitSenseTests.cs
// ============================================================================
// PURPOSE:
//   Verifies Exit Sense is a separate display channel, not a replacement objective.
//   Exercises the existing Floor-owned Mimic policy before presenting its snapshot.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · HUD.
// KEY RESPONSIBILITIES:
//   - Cover independent bearings, chase visibility and snapshot removal.
//   - Keep Mimics excluded unless Floor admits a live Faithless window.
// DEPENDENCIES:
//   Core, Floor guidance, HUD, Run routing, UI Toolkit and NUnit.
// USAGE NOTES:
//   Pure bearings plus one native UI/routing case; gameplay eligibility stays in Floor.
// ============================================================================
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Presentation.HUD;
using Worsen.Session.Run;
using Worsen.Orchestrator;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.HUD
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HUDExitSenseTests
    {
        [Test] public void RunGuidanceReachesIndependentExitVisualAndDisableClearsIt()
        {
            var owner = new GameObject("Exit Sense routing"); owner.SetActive(false);
            var config = ScriptableObject.CreateInstance<HUDDriverConfig>();
            var run = owner.AddComponent<RunSessionManager>(); var hud = owner.AddComponent<HUDManager>();
            var driver = owner.AddComponent<HUDDriver>(); var route = owner.AddComponent<HUDOrchestrator>();
            var visual = owner.AddComponent<HUDVisualDriver>(); var root = new VisualElement(); var state = new HUDDriverState();
            void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
            void Lifecycle(string name) => typeof(HUDOrchestrator).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(route, null);
            try
            {
                Set(driver, "_state", state); Set(driver, "_presenter", new HUDPresenter()); Set(driver, "_config", config);
                Set(hud, "_driver", driver); Set(hud, "_initialized", true); Set(route, "_run", run); Set(route, "_hud", hud);
                Lifecycle("OnEnable"); Lifecycle("OnEnable");
                var channel = typeof(RunSessionManager).GetField("GuidanceChanged", BindingFlags.Instance | BindingFlags.NonPublic);
                var receiver = (Delegate)channel.GetValue(run); Assert.That(receiver.GetInvocationList().Length, Is.EqualTo(1));
                var exit = new GuidanceTarget(GuidanceKind.ExitThroughWalls, Vector3.right, Vector3.right * 20f);
                receiver.DynamicInvoke((object)new[] { exit }); hud.SetChaseMode(true);
                Assert.That(state.ExitSenseTarget, Is.EqualTo(exit));
                for (int i = 0; i < 2; i++)
                {
                    visual.Bind(root, config); visual.Apply(state);
                    Assert.That(root.Query<VisualElement>("exit-sense-cue").ToList().Count, Is.EqualTo(1));
                    Assert.That(root.Q("exit-sense-group").parent, Is.SameAs(root));
                    Assert.That(root.Q("exit-sense-group").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                    Assert.That(root.Q("direction-group").style.display.value, Is.EqualTo(DisplayStyle.None));
                    visual.Unbind(); Assert.That(root.childCount, Is.Zero);
                }
                Lifecycle("OnDisable"); Assert.That(state.ExitSenseTarget, Is.Null); Assert.That(channel.GetValue(run), Is.Null);
            }
            finally { Lifecycle("OnDisable"); visual.Unbind(); UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(config); }
        }
        [Test] public void ExitSenseRetainsTargetAndIndependentCameraBearingDuringChase()
        {
            var p = new HUDPresenter(); var s = new HUDDriverState();
            var exit = new GuidanceTarget(GuidanceKind.ExitThroughWalls, Vector3.left, Vector3.left * 20f);
            p.SetGuidance(s, new[] { new GuidanceTarget(GuidanceKind.WhiteArrow, Vector3.forward, Vector3.forward),
                new GuidanceTarget(GuidanceKind.GoldenSense, Vector3.right, Vector3.right), exit });
            p.SetChaseMode(s, true); p.SetHeading(s, 90f);
            Assert.That(s.ExitSenseTarget, Is.EqualTo(exit));
            Assert.That(s.ExitSenseVisible && s.DirectionVisible && s.GoldenSenseVisible, Is.True);
            Assert.That(s.ChromeVisible, Is.False);
            Assert.That(s.ExitSenseArrowDegrees, Is.EqualTo(-180f).Within(.001f));
            Assert.That(s.ArrowDegrees, Is.EqualTo(-90f).Within(.001f));
            p.SetViewRotation(s, Quaternion.identity);
            Assert.That(s.ExitSenseArrowDegrees, Is.EqualTo(-90f).Within(.001f));
            p.SetGuidance(s, null);
            Assert.That(s.ExitSenseTarget, Is.Null); Assert.That(s.ExitSenseVisible, Is.False);
            Assert.That(s.ExitSenseArrowDegrees, Is.Zero);
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(0f)]
        public void InvalidExitDirectionNeverDraws(float x)
        {
            var p = new HUDPresenter(); var s = new HUDDriverState();
            p.SetGuidance(s, new[] { new GuidanceTarget(GuidanceKind.ExitThroughWalls, new Vector3(x, 0f, 0f), Vector3.one) });
            Assert.That(s.ExitSenseVisible, Is.False);
        }
        [TestCase(false)] [TestCase(true)]
        public void MimicOnlyReplacesWhiteInsideFloorAcceptedFaithlessWindow(bool cursed)
        {
            var floor = new FloorGuidanceController(new FloorGuidanceBehaviorState());
            var player = new EntityId(1); var hunter = new EntityId(7);
            if (cursed) floor.SetActiveEffects(new ActiveEffects(new[] {
                new ActiveEffect(FloorGuidanceController.FaithlessArrow, EffectKind.Curse, 1) }));
            floor.ReceiveMimic(new MimicFact(hunter, player, MimicFactKind.Pose, 1, Vector3.right * 4f, golden: true));
            floor.ReceiveMimic(new MimicFact(hunter, player, MimicFactKind.FaithlessWindow, 1, Vector3.right * 4f, seconds: 2f));
            var normal = new[] { new GuidanceTarget(GuidanceKind.WhiteArrow, Vector3.forward, Vector3.forward, 2),
                new GuidanceTarget(GuidanceKind.ExitThroughWalls, Vector3.back, Vector3.back) };
            var p = new HUDPresenter(); var s = new HUDDriverState();
            p.SetGuidance(s, floor.Apply(normal, player, Vector3.zero));
            Assert.That(s.WorldDirection, Is.EqualTo(cursed ? Vector3.right : Vector3.forward));
            Assert.That(s.ExitSenseTarget, Is.EqualTo(normal[1]));
            floor.Tick(2f); p.SetGuidance(s, floor.Apply(normal, player, Vector3.zero));
            Assert.That(s.WorldDirection, Is.EqualTo(Vector3.forward));
        }
    }
}
