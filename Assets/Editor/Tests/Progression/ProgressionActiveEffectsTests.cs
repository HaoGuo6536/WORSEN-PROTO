// ============================================================================
// ProgressionActiveEffectsTests.cs
// ============================================================================
// PURPOSE:
//   Exercises catalogue-backed choices alongside the legacy progression state machine.
//   Frozen revisions, floor retention and duplicate threats are checked through
//   accepted commands rather than direct mutation of run state.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Progression.
// KEY RESPONSIBILITIES:
//   - Isolate selection/shop assertions from automatic events, tested in their own fixture.
//   - Require explicit slot activation before the one-charge ward can break a grab.
//   - Check all four effect kinds, reroll-restocked pedestals, held items and frozen revisions.
//   - Check configurable cadence, mandatory choices and catalogue offer requirements.
// DEPENDENCIES:
//   - Core, Session Progression, NUnit and temporary Unity config allocation.
// USAGE NOTES:
//   Edit Mode; no generated floor or asset writes. Test catalogue is deliberately small.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Progression;

namespace Worsen.Tests.Progression
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProgressionActiveEffectsTests
    {
        private ProgressionConfig config;
        private EffectCatalogueConfig catalogue;
        private ProgressionSessionController controller;
        private ProgressionSnapshot Snapshot => controller.Snapshot();
        private IReadOnlyActiveEffects Active => controller.EffectsSnapshot().ActiveEffects;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<ProgressionConfig>();
            Set(config, "_eventPool", Array.Empty<ProgressionEventKind>());
            catalogue = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
            Set(config, "_effectCatalogue", catalogue);
            Set(config, "_threats", new[] { new ProgressionEntryConfig("echo", "Echo", "Adds an Echo.") });
            Set(config, "_curses", new[] { new ProgressionEntryConfig("legacy", "Legacy", "Changes footsteps.", traits: ProgressionTraits.EchoDebt) });
            Set(config, "_offers", new[] {
                new ProgressionEntryConfig("speed-boost", "Speed Boost", "Increases sprint speed.", price: 1, stockPerVisit: 4, movementSpeedMultiplier: 1.2f),
                new ProgressionEntryConfig("wax-ward", "Wax Ward", "Breaks a grab.", price: 1, repeatable: true, grantsWaxWard: true) });
            Set(catalogue, "_entries", new[] {
                new EffectCatalogueEntry("echo", EffectKind.Threat, FearAxis.Time, "Echo", "Adds an Echo."),
                new EffectCatalogueEntry("weaver", EffectKind.Threat, FearAxis.Agency, "Weaver", "Adds a Weaver."),
                new EffectCatalogueEntry("slow-mend", EffectKind.Curse, FearAxis.Stakes, "Slow Mend", "Reduces healing."),
                new EffectCatalogueEntry("no-regen", EffectKind.Curse, FearAxis.Stakes, "No Regen", "Removes healing.", floor: 4, prerequisite: "slow-mend"),
                new EffectCatalogueEntry("echo-shorter-delay", EffectKind.Curse, FearAxis.Time, "Shorter Delay", "Shortens delay.", cap: 2, hunters: new[] { "echo" }),
                new EffectCatalogueEntry("weaver-quick-spin", EffectKind.Curse, FearAxis.Time, "Quick Spin", "Shortens warning.", hunters: new[] { "weaver" }),
                new EffectCatalogueEntry("speed-boost", EffectKind.Upgrade, FearAxis.Agency, "Speed Boost", "Increases sprint speed.", cap: 3, price: 1),
                new EffectCatalogueEntry("wax-ward", EffectKind.Consumable, FearAxis.Agency, "Wax Ward", "Breaks a grab.", price: 1) });
            Restart();
        }
        [TearDown]
        public void TearDown() { UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(catalogue); }
        private void Restart()
        {
            controller = new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(71));
            controller.StartRun(71);
        }

        [TestCase(1, 2)] [TestCase(2, 2)] [TestCase(3, 4)]
        public void SelectionClockDoesNotCountShopsAndInterveningFloorsArePure(int interval, int shopInterval)
        {
            Set(config, "_selectionInterval", interval); Set(config, "_shopInterval", shopInterval); Restart();
            int shops = 0;
            for (int combat = 0; combat < 12; combat++)
            {
                if (controller.GenerationRequest().IsShop)
                {
                    Assert.That(combat % shopInterval, Is.Zero);
                    Assert.That(Snapshot.Phase, Is.EqualTo(ProgressionPhase.Generating));
                    controller.ConfirmFloorReady(Snapshot.GenerationId);
                    controller.ContinueShop(Snapshot.Revision); shops++;
                }
                bool selection = combat % interval == 0;
                Assert.That(Snapshot.Phase, Is.EqualTo(selection ? ProgressionPhase.ChooseThreat : ProgressionPhase.Generating));
                if (selection)
                {
                    Assert.That(controller.ChooseThreat("", Snapshot.Revision), Is.False, "No decline action.");
                    Assert.That(controller.ConfirmFloorReady(Snapshot.GenerationId), Is.False);
                }
                OpenFloor();
                Assert.That(Active.Stacks(new EffectId("echo")), Is.EqualTo(combat / interval + 1));
                Assert.That(Snapshot.Effects.ActiveThreatBudget, Is.EqualTo(combat / interval + 1));
                controller.CompleteFloor(Snapshot.GenerationId);
            }
            Assert.That(shops, Is.EqualTo(11 / shopInterval));
        }

        [Test]
        public void CommittedCurseOffersEnforcePrerequisiteRoundHunterAndCap()
        {
            controller.ChooseThreat("echo", Snapshot.Revision);
            Assert.That(Snapshot.Choices.Select(c => c.Id), Does.Not.Contain("no-regen").And.Not.Contain("weaver-quick-spin"));
            Assert.That(controller.ChooseCurse("no-regen", Snapshot.Revision), Is.False);
            Assert.That(controller.ChooseCurse("slow-mend", Snapshot.Revision), Is.True);
            OpenFloor(); controller.CompleteFloor(Snapshot.GenerationId);
            Assert.That(Active.Has(new EffectId("slow-mend")), Is.True);
            NextSelection();
            Assert.That(Snapshot.Round, Is.EqualTo(4));
            controller.ChooseThreat("echo", Snapshot.Revision);
            Assert.That(Snapshot.Choices.Select(c => c.Id), Does.Contain("no-regen"));
            Assert.That(controller.ChooseCurse("no-regen", Snapshot.Revision), Is.True);
            for (int stack = 1; stack <= 2; stack++)
            {
                OpenFloor(); controller.CompleteFloor(Snapshot.GenerationId); NextSelection();
                controller.ChooseThreat("echo", Snapshot.Revision);
                Assert.That(controller.ChooseCurse("echo-shorter-delay", Snapshot.Revision), Is.True);
                Assert.That(Active.Stacks(new EffectId("echo-shorter-delay")), Is.EqualTo(stack));
            }
            OpenFloor(); controller.CompleteFloor(Snapshot.GenerationId); NextSelection();
            controller.ChooseThreat("echo", Snapshot.Revision);
            Assert.That(Snapshot.Choices.Select(c => c.Id), Does.Not.Contain("echo-shorter-delay").And.Not.Contain("weaver-quick-spin"));
            Assert.That(controller.ChooseCurse("echo-shorter-delay", Snapshot.Revision), Is.False);
        }

        [Test]
        public void FrozenViewsRetainStacksAcrossFloorsTrackHeldWardAndResetOnRestart()
        {
            var empty = controller.EffectsSnapshot();
            controller.ChooseThreat("echo", Snapshot.Revision);
            controller.ChooseCurse("legacy", Snapshot.Revision);
            var chosen = controller.EffectsSnapshot();
            Assert.That(Snapshot.Effects.Traits, Is.EqualTo(ProgressionTraits.EchoDebt));
            OpenFloor();
            for (int anchor = 0; anchor < 20; anchor++) controller.RecordGoldenCollected(Snapshot.GenerationId, anchor);
            controller.CompleteFloor(Snapshot.GenerationId); OpenFloor(); controller.CompleteFloor(Snapshot.GenerationId);
            controller.ConfirmFloorReady(Snapshot.GenerationId);
            for (int copy = 0; copy < 3; copy++)
            {
                if (copy > 0) Assert.That(controller.RerollShop(Snapshot.Revision), Is.True);
                Assert.That(controller.Purchase("speed-boost", Snapshot.Revision), Is.True);
            }
            Assert.That(controller.Purchase("speed-boost", Snapshot.Revision), Is.False);
            Assert.That(Snapshot.Effects.MovementSpeedMultiplier, Is.EqualTo(1f), "Player applies catalogue sprint effects exactly once.");
            Assert.That(controller.Purchase("wax-ward", Snapshot.Revision), Is.True);
            var stocked = controller.EffectsSnapshot();
            Assert.That(Active.Single(e => e.Id.Value == "speed-boost").Kind, Is.EqualTo(EffectKind.Upgrade));
            Assert.That(Active.Stacks(new EffectId("speed-boost")), Is.EqualTo(3));
            Assert.That(Active.Single(e => e.Id.Value == "wax-ward").Kind, Is.EqualTo(EffectKind.Consumable));
            controller.ContinueShop(Snapshot.Revision); OpenFloor();
            Assert.That(controller.TryConsumeSelected(Snapshot.GenerationId, Snapshot.Revision, "wax-ward"), Is.True);
            Assert.That(controller.TryConsumeWaxWard(Snapshot.GenerationId), Is.True);
            Assert.That(Active.Has(new EffectId("wax-ward")), Is.False);
            Assert.That(stocked.ActiveEffects.Has(new EffectId("wax-ward")), Is.True);
            Assert.That(chosen.ActiveEffects.Count, Is.EqualTo(2));
            Assert.That(empty.ActiveEffects.Count, Is.Zero);
            controller.StartRun(71);
            Assert.That(Active.Count, Is.Zero);
            Assert.That(Snapshot.Revision, Is.GreaterThan(stocked.Progression.Revision));
            Assert.That(chosen.ActiveEffects.Stacks(new EffectId("echo")), Is.EqualTo(1));
        }

        [Test]
        public void CatalogueHealthEffectDoesNotAlsoChangeTheSessionBaseline()
        {
            Set(config, "_curses", new[] { new ProgressionEntryConfig("thin-skin", "Thin Skin", "Reduces maximum health.", maximumHealthDelta: -25f) });
            Set(catalogue, "_entries", new[] { new EffectCatalogueEntry("thin-skin", EffectKind.Curse, FearAxis.Stakes, "Thin Skin", "Reduces maximum health.") });
            Restart(); OpenFloor();
            Assert.That(Active.Has(new EffectId("thin-skin")), Is.True);
            Assert.That(Snapshot.Effects.MaximumHealth, Is.EqualTo(100f));
        }

        private void OpenFloor()
        {
            if (Snapshot.Phase == ProgressionPhase.ChooseThreat) Assert.That(controller.ChooseThreat("echo", Snapshot.Revision), Is.True);
            if (Snapshot.Phase == ProgressionPhase.ChooseCurse) Assert.That(controller.ChooseCurse(Snapshot.Choices[0].Id, Snapshot.Revision), Is.True);
            Assert.That(controller.ConfirmFloorReady(Snapshot.GenerationId), Is.True);
        }
        private void NextSelection()
        {
            for (int guard = 0; guard < 12 && Snapshot.Phase != ProgressionPhase.ChooseThreat; guard++)
            {
                if (controller.GenerationRequest().IsShop)
                { controller.ConfirmFloorReady(Snapshot.GenerationId); controller.ContinueShop(Snapshot.Revision); }
                else { OpenFloor(); controller.CompleteFloor(Snapshot.GenerationId); }
            }
            Assert.That(Snapshot.Phase, Is.EqualTo(ProgressionPhase.ChooseThreat));
        }
        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
