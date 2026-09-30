// ============================================================================
// ProgressionEventControllerTests.cs
// ============================================================================
// PURPOSE:
//   Exercises seeded events and their real Progression transaction integration.
//   Temporary configs keep event admission, expiry and mutation selection observable
//   without constructing hunters, generating floors or changing project assets.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Progression.
// KEY RESPONSIBILITIES:
//   - Cover cadence, shops, exactly-once application, frozen views and run reset.
//   - Check tell-only mutations and a single generic selection/shelter message.
//   - Verify catalogue migration and unrestricted same-axis shrine/event coexistence.
// DEPENDENCIES:
//   - NUnit, Progression, Hunter profile data, Shrine pure logic and catalogue setup.
// USAGE NOTES:
//   Edit Mode. Reflection authors temporary configs; the coordinator executes tests.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Shrine;
using Worsen.Session.Progression;
using Worsen.Editor.Progression;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Progression
{
    public sealed class ProgressionEventControllerTests
    {
        private ProgressionConfig config;
        private EffectCatalogueConfig catalogue;
        private HunterProfile profile;
        private ProgressionSessionBehaviorState state;
        private ProgressionEventController events;
        [SetUp] public void SetUp()
        {
            config = ScriptableObject.CreateInstance<ProgressionConfig>();
            catalogue = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
            profile = ScriptableObject.CreateInstance<HunterProfile>();
            Set(profile, "_archetypeKey", "weaver");
            Set(profile, "_mutationPool", new[] { Mutation(HunterTunable.Acceleration, 30f, "uneven-steps") });
            Set(config, "_mutationProfiles", new[] { profile }); Set(config, "_effectCatalogue", catalogue);
            Set(config, "_threats", new[] { new ProgressionEntryConfig("weaver", "Weaver", "Adds a hunter.") });
            Set(config, "_curses", new[] { new ProgressionEntryConfig("legacy", "Legacy", "Changes footsteps.") });
            Set(catalogue, "_entries", new[] {
                new EffectCatalogueEntry("weaver", EffectKind.Threat, FearAxis.Agency, "Weaver", "Adds a hunter."),
                new EffectCatalogueEntry("darker-floors", EffectKind.Curse, FearAxis.Information, "Dark", "Reduces visibility."),
                new EffectCatalogueEntry("weaver-quick-spin", EffectKind.Curse, FearAxis.Time, "Spin", "Shortens warning.", cap: 3, hunters: new[] { "weaver" }) });
            state = new ProgressionSessionBehaviorState();
            var session = new ProgressionSessionController(state, config, new System.Random(73));
            session.StartRun(73); session.ChooseThreat("weaver", session.Snapshot().Revision);
            events = new ProgressionEventController(state, config, catalogue, new System.Random(73)); events.ResetRun();
        }
        [TearDown] public void TearDown()
        { Object.DestroyImmediate(config); Object.DestroyImmediate(catalogue); Object.DestroyImmediate(profile); }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private void Round(int round, bool shop = false)
        {
            typeof(ProgressionSessionBehaviorState).GetProperty("Round").SetValue(state, round);
            typeof(ProgressionSessionBehaviorState).GetProperty("IsShop").SetValue(state, shop);
        }
        private ActiveEffects Active => events.Combined(new ActiveEffects(new[] {
            new ActiveEffect(new EffectId("weaver"), EffectKind.Threat, state.ThreatCount) }));
        private void Pool(ProgressionEventKind kind) => Set(config, "_eventPool", new[] { kind });
        private static HunterMutationData Mutation(HunterTunable tunable, float value, string tell)
        { var entry = new HunterMutationData(); Set(entry, "_tunable", tunable); Set(entry, "_value", value); Set(entry, "_tellId", tell); return entry; }

        [Test] public void FirstEventIsEightAndDueShopDefersWithoutConsumingCadence()
        {
            Pool(ProgressionEventKind.ExtraHunter);
            for (int round = 1; round < 8; round++) { Round(round); Assert.That(events.BeginRound(Active), Is.False); }
            Round(8); Assert.That(events.BeginRound(Active), Is.True);
            long due = (long)typeof(ProgressionSessionBehaviorState).GetField("NextEventRound", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state);
            Assert.That(due, Is.InRange(15L, 17L));
            Round((int)due, true); Assert.That(events.BeginRound(Active), Is.False);
            Round((int)due + 1); Assert.That(events.BeginRound(Active), Is.True);
            Assert.That(events.History.Select(e => e.Round), Is.EqualTo(new[] { 8, (int)due + 1 }));
        }
        [TestCase(ProgressionEventKind.EnvironmentalHazard)]
        [TestCase(ProgressionEventKind.HunterUpgrade)]
        [TestCase(ProgressionEventKind.ExtraHunter)]
        [TestCase(ProgressionEventKind.Random)]
        [TestCase(ProgressionEventKind.HiddenMutation)]
        public void EachKindCommitsOneOutcomeAndOneFact(ProgressionEventKind kind)
        {
            Pool(kind); Round(8);
            Assert.That(events.BeginRound(Active), Is.True);
            var fact = events.History.Single();
            Assert.That(fact.Kind, Is.EqualTo(kind)); Assert.That(fact.Round, Is.EqualTo(8));
            Assert.That(fact.Axis, Is.Not.EqualTo(FearAxis.None));
            Assert.That(events.BeginRound(Active), Is.False);
            Assert.That(events.TryTakeFact(out var published), Is.True); Assert.That(published, Is.EqualTo(fact));
            Assert.That(events.TryTakeFact(out _), Is.False);
            if (fact.ResolvedKind == ProgressionEventKind.EnvironmentalHazard)
                Assert.That(events.Combined(default(ActiveEffects)).Stacks(new EffectId("darker-floors")), Is.EqualTo(1));
            else if (fact.ResolvedKind == ProgressionEventKind.HunterUpgrade)
            {
                Assert.That(state.CurseCount, Is.EqualTo(1)); Assert.That(fact.EffectId, Is.EqualTo("weaver-quick-spin"));
                var session = new ProgressionSessionController(state, config, new System.Random(73));
                Assert.That(session.EffectsSnapshot().ActiveEffects.Stacks(new EffectId(fact.EffectId)), Is.EqualTo(1));
            }
            else if (fact.ResolvedKind == ProgressionEventKind.ExtraHunter) Assert.That(state.ThreatCount, Is.EqualTo(2));
            else Assert.That(events.RetainedMutations["weaver"].Count, Is.EqualTo(1));
        }
        [Test] public void HazardsLastThreeCombatFloorsPauseAtShopAndNeverBecomeRetainedCurses()
        {
            Pool(ProgressionEventKind.EnvironmentalHazard); Round(8); events.BeginRound(Active);
            var frozen = events.Combined(default(ActiveEffects));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(events.Combined(default(ActiveEffects)).Has(new EffectId("darker-floors")), Is.True);
                events.EndCombatFloor();
                Round(9 + i * 2, true);
                Assert.That(events.Combined(default(ActiveEffects)).Count, Is.Zero);
                Round(10 + i * 2);
            }
            Assert.That(events.Combined(default(ActiveEffects)).Count, Is.Zero);
            Assert.That(frozen.Count, Is.EqualTo(1)); Assert.That(state.CurseCount, Is.Zero);
        }
        [Test] public void MutationRejectsMalformedNoOpAndAbsentHabitEntriesAndRetainsOnlyValidPoolValue()
        {
            Pool(ProgressionEventKind.HiddenMutation);
            Set(profile, "_habits", Array.Empty<HunterHabitData>());
            Set(profile, "_mutationPool", new[] { null, Mutation((HunterTunable)999, 1f, "bad"),
                Mutation(HunterTunable.TurnRate, float.NaN, "bad"), Mutation(HunterTunable.Acceleration, 0f, "bad"),
                Mutation(HunterTunable.ChaseSpeedMultiplier, float.MaxValue, "overflow"),
                Mutation(HunterTunable.TurnRate, 20f, ""), Mutation(HunterTunable.Acceleration, profile.Acceleration, "no-op"),
                Mutation(HunterTunable.ThresholdPauseEnabled, 0f, "missing"), Mutation(HunterTunable.ActionCommitmentSeconds, 0.01f, "bad"),
                Mutation(HunterTunable.Acceleration, 30f, "uneven-steps") });
            Round(8); Assert.That(events.BeginRound(Active), Is.True);
            var frozen = events.RetainedMutations;
            var mutation = frozen["weaver"].Single();
            Assert.That(mutation.Tunable, Is.EqualTo(HunterTunable.Acceleration)); Assert.That(mutation.Value, Is.EqualTo(30f));
            Assert.That(events.History.Single().TellId, Is.EqualTo("uneven-steps"));
            Assert.That(events.History.Single().EffectId, Is.Null);
            Round(30); Assert.That(events.BeginRound(Active), Is.False, "Never repeat a retained tunable.");
            events.ResetRun(); Assert.That(events.RetainedMutations, Is.Empty); Assert.That(events.History, Is.Empty);
            Assert.That(events.TryTakeFact(out _), Is.False); Assert.That(frozen["weaver"].Count, Is.EqualTo(1));
            Assert.That(profile.Acceleration, Is.EqualTo(20f));
        }
        [Test] public void EmptyOrUnavailablePoolsDoNotInventEvents()
        {
            Pool(ProgressionEventKind.HiddenMutation); Set(config, "_mutationProfiles", Array.Empty<HunterProfile>());
            Round(8); Assert.That(events.BeginRound(Active), Is.False); Assert.That(events.History, Is.Empty);
            Pool(ProgressionEventKind.EnvironmentalHazard);
            Set(catalogue, "_entries", new[] { new EffectCatalogueEntry("nothing", EffectKind.Curse,
                FearAxis.Unpredictability, "Nothing???", "Nothing???") });
            Round(9); Assert.That(events.BeginRound(Active), Is.False);
        }
        [Test] public void SeededMutationChoiceReplaysAndNeverLosesProfilesRejectLossOverrides()
        {
            Pool(ProgressionEventKind.HiddenMutation);
            Set(profile, "_mutationPool", new[] { Mutation(HunterTunable.Acceleration, 30f, "steps"),
                Mutation(HunterTunable.TurnRate, 300f, "turn") });
            Round(8); Assert.That(events.BeginRound(Active), Is.True);
            var chosen = events.RetainedMutations["weaver"].Single();
            events = new ProgressionEventController(state, config, catalogue, new System.Random(73)); events.ResetRun();
            Assert.That(events.BeginRound(Active), Is.True);
            Assert.That(events.RetainedMutations["weaver"].Single().Tunable, Is.EqualTo(chosen.Tunable));
            var echo = ScriptableObject.CreateInstance<Worsen.Domain.Hunter.Archetypes.Echo.EchoConfig>();
            try
            {
                Set(profile, "_archetypeRules", echo);
                Set(profile, "_mutationPool", new[] { Mutation(HunterTunable.LossSeconds, 1f, "loss") });
                events.ResetRun(); Assert.That(events.BeginRound(Active), Is.False);
            }
            finally { Object.DestroyImmediate(echo); }
        }
        [Test] public void ShrineAndEventCanShareTheSameFloorAndFearAxis()
        {
            Pool(ProgressionEventKind.HiddenMutation);
            var session = Session(73);
            while (session.Snapshot().Round < 8) Advance(session);
            var shrineConfig = ScriptableObject.CreateInstance<ShrineConfig>();
            try
            {
                Set(shrineConfig, "_availability", new[] {
                    new ShrineAvailability(ShrineKind.Chance, 1, FearAxis.Unpredictability) });
                var shrines = new ShrineController(new ShrineBehaviorState(), shrineConfig, new System.Random(73));
                var sites = new[] { new ShrineSite(Vector3.zero, 1, false), new ShrineSite(Vector3.right, 2, false) };
                Assert.That(session.CurrentEventFearAxis, Is.EqualTo(FearAxis.Unpredictability));
                var placed = shrines.Assemble(sites, 8, false);
                Assert.That(placed.Count, Is.EqualTo(2));
                Assert.That(placed.All(p => p.Kind == ShrineKind.Chance), Is.True);
            }
            finally { Object.DestroyImmediate(shrineConfig); }
        }
        private ProgressionSessionController Session(int seed)
        {
            var result = new ProgressionSessionController(new ProgressionSessionBehaviorState(), config, new System.Random(seed));
            result.StartRun(seed); return result;
        }
        private static void Advance(ProgressionSessionController session)
        {
            var snapshot = session.Snapshot();
            if (snapshot.Phase == ProgressionPhase.ChooseThreat) session.ChooseThreat(snapshot.Choices[0].Id, snapshot.Revision);
            snapshot = session.Snapshot();
            if (snapshot.Phase == ProgressionPhase.ChooseCurse) session.ChooseCurse(snapshot.Choices[0].Id, snapshot.Revision);
            snapshot = session.Snapshot(); Assert.That(session.ConfirmFloorReady(snapshot.GenerationId), Is.True);
            if (session.GenerationRequest().IsShop) session.ContinueShop(session.Snapshot().Revision);
            else session.CompleteFloor(snapshot.GenerationId);
        }
        [Test] public void SeededCadenceReplaysAndNeverChangesLayoutDraws()
        {
            Pool(ProgressionEventKind.ExtraHunter);
            var first = Session(73); var replay = Session(73);
            for (int i = 1; i <= 60; i++)
            {
                Assert.That(first.GenerationRequest().Seed, Is.EqualTo(replay.GenerationRequest().Seed));
                Assert.That(first.EventHistory, Is.EqualTo(replay.EventHistory));
                Assert.That(first.EventHistory.All(e => e.Round >= 8 && e.Round % 3 != 0), Is.True);
                Advance(first); Advance(replay);
            }
            Assert.That(first.EventHistory.Count, Is.GreaterThan(4));
            var changedSeed = Session(74); for (int i = 1; i <= 60; i++) Advance(changedSeed);
            Assert.That(first.EventHistory.Select(e => e.Round), Is.Not.EqualTo(changedSeed.EventHistory.Select(e => e.Round)));
            Set(config, "_eventPool", Array.Empty<ProgressionEventKind>());
            var noEvents = Session(73); var withEvents = Session(73);
            for (int i = 1; i <= 20; i++)
            {
                Pool(ProgressionEventKind.ExtraHunter); Advance(withEvents);
                Set(config, "_eventPool", Array.Empty<ProgressionEventKind>()); Advance(noEvents);
                Assert.That(withEvents.GenerationRequest().Seed, Is.EqualTo(noEvents.GenerationRequest().Seed));
            }
        }
        [TestCase(1)] [TestCase(2)]
        public void MutationMessageAppearsOnOneEligibleScreenAndResetClearsEverything(int selectionInterval)
        {
            Pool(ProgressionEventKind.HiddenMutation); Set(config, "_selectionInterval", selectionInterval);
            var session = Session(73); int messages = 0;
            for (int round = 1; round <= 14; round++)
            {
                if (session.Snapshot().Message == "something is different")
                { messages++; Assert.That(session.Snapshot().Message, Is.EqualTo("something is different")); }
                if (round == 8)
                {
                    Assert.That(session.RetainedMutations["weaver"].Count, Is.EqualTo(1));
                    Assert.That(session.CurrentEventFearAxis, Is.EqualTo(FearAxis.Unpredictability));
                }
                // Shop readiness is a message delivery boundary, unlike generation.
                if (session.GenerationRequest().IsShop)
                {
                    session.ConfirmFloorReady(session.Snapshot().GenerationId);
                    if (session.Snapshot().Message == "something is different") messages++;
                    session.ContinueShop(session.Snapshot().Revision);
                }
                else Advance(session);
            }
            Assert.That(messages, Is.EqualTo(1));
            session.StartRun(73); Assert.That(session.RetainedMutations, Is.Empty);
            Assert.That(session.EventHistory, Is.Empty);
            Assert.That(session.FloorEffects.Count, Is.Zero); Assert.That(session.Snapshot().Message, Is.Not.EqualTo("something is different"));
        }
        [Test] public void CatalogueMigrationRestoresDefaultsAndAddsRequiredModuleIdsOnce()
        {
            var tuned = new EffectCatalogueEntry("more-shrines", EffectKind.Upgrade, FearAxis.Agency, "Tuned", "Adds one shrine.", price: 99);
            Set(catalogue, "_entries", new[] { tuned });
            EffectCatalogueSetup.AppendMissingEntries(catalogue); int count = catalogue.Entries.Count;
            EffectCatalogueSetup.AppendMissingEntries(catalogue); Assert.That(catalogue.Entries.Count, Is.EqualTo(count));
            var defaults = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
            try
            {
                Assert.That(EffectCatalogueUtility.Find(catalogue, "more-shrines").Price,
                    Is.EqualTo(EffectCatalogueUtility.Find(defaults, "more-shrines").Price));
            }
            finally { Object.DestroyImmediate(defaults); }
            foreach (string id in new[] { "blinder-more-traps", "blinder-silent-traps" })
            {
                var entry = EffectCatalogueUtility.Find(catalogue, id);
                Assert.That(entry.Kind, Is.EqualTo(EffectKind.Curse)); Assert.That(entry.RequiredHunterIds, Is.EqualTo(new[] { "blinder" }));
                Assert.That(EffectCatalogueUtility.Eligible(entry, 20, default(ActiveEffects)), Is.False);
                Assert.That(EffectCatalogueUtility.Eligible(entry, 20, new ActiveEffects(new[] {
                    new ActiveEffect(new EffectId("blinder"), EffectKind.Threat, 1) })), Is.True);
            }
            foreach (string id in new[] { "weaver-stickier-webs", "weaver-wider-webs", "weaver-doorway-nests", "weaver-quick-spin",
                "ticking-runs-faster", "ticking-farther-keys", "ticking-loud-keys", "ticking-double-spring" })
                Assert.That(EffectCatalogueUtility.Find(catalogue, id), Is.Not.Null, id);
        }
        [Test] public void MoreShrinesViewRaisesDepthCurveByOne()
        {
            var shrineConfig = ScriptableObject.CreateInstance<ShrineConfig>();
            try
            {
                Set(catalogue, "_entries", new[] { new EffectCatalogueEntry("more-shrines", EffectKind.Upgrade,
                    FearAxis.Agency, "More Shrines", "Adds one shrine.", price: 0) });
                var session = Session(73); Advance(session); Advance(session);
                session.ConfirmFloorReady(session.Snapshot().GenerationId);
                Assert.That(session.Purchase("more-shrines", session.Snapshot().Revision), Is.True);
                Assert.That(session.MoreShrines, Is.True);
                var shrines = new ShrineController(new ShrineBehaviorState(), shrineConfig, new System.Random(73));
                Assert.That(shrines.CountForFloor(12, session.MoreShrines), Is.EqualTo(shrines.CountForFloor(12, false) + 1));
                session.StartRun(73); Assert.That(session.MoreShrines, Is.False);
                var defaults = ScriptableObject.CreateInstance<EffectCatalogueConfig>();
                try { var entry = EffectCatalogueUtility.Find(defaults, "more-shrines"); Assert.That(entry.StackCap, Is.EqualTo(1)); Assert.That(entry.Kind, Is.EqualTo(EffectKind.Upgrade)); }
                finally { Object.DestroyImmediate(defaults); }
            }
            finally { Object.DestroyImmediate(shrineConfig); }
        }
    }
}
