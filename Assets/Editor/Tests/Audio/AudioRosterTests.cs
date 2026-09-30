// ============================================================================
// AudioRosterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies roster admission and routing with transient clips, not scene assets.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
// KEY RESPONSIBILITIES:
//   - Cover exact tells, hunter slots, deduplication, zones and paired publishers.
// DEPENDENCIES:
//   - Core, Audio, Session publishers, Orchestrator, NUnit and Unity test objects.
// USAGE NOTES:
//   Edit Mode only; reflection supplies publishers and inspects owned state.
//   Compilation alone is not evidence that these tests ran or clips are audible.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Audio;
using Worsen.Orchestrator;
using Worsen.Session.Run;
using Worsen.Session.Progression;
using Worsen.Session.Expedition;
using Worsen.Session.HorrorEffects;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Audio
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class AudioRosterTests
    {
        private readonly List<Object> _owned = new List<Object>();
        private AudioSoundscapeDriverConfig _config;
        private AudioSoundscapeDriver _driver;
        private AudioRosterPresenter _presenter;
        private AudioRosterDriverState _roster;
        private AudioSoundscapeDriverState Pool => Get<AudioSoundscapeDriverState>(_driver, "_state");
        [SetUp] public void Setup()
        {
            _config = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>(); _owned.Add(_config);
            var clip = AudioClip.Create("Roster test", 22050, 1, 22050, false); _owned.Add(clip);
            var cues = new[] { CueId.Footstep, CueId.Presence, CueId.Detection, CueId.Chase, CueId.EnemyWindup, CueId.Death };
            Set(_config, "_sounds", cues.Select(c => new AudioSoundDefinition { Cue = c, Clips = new[] { clip }, Gain = 1f,
                PitchMinimum = .8f, PitchMaximum = 1.2f, Priority = 60 }).ToArray());
            Set(_config, "_rosterBindings", _config.RosterBindings.Select(b => { b.Clip = clip; return b; }).ToArray());
            Set(_config, "_timingJitterSeconds", .1f);
            _driver = Component<AudioSoundscapeDriver>(); _driver.gameObject.SetActive(true);
            _driver.Initialize(_config); _driver.SetOwnerEnabled(true); _driver.SetInRun(true);
            _presenter = new AudioRosterPresenter(); _roster = new AudioRosterDriverState();
        }
        [TearDown] public void Cleanup()
        {
            if (_driver != null) _driver.Teardown();
            for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }
        [TestCase("echo")][TestCase("weaver")][TestCase("ticking")]
        public void FeedbackUsesSeparateSlotsAndDuplicateFactsDoNotRetrigger(string archetype)
        {
            foreach (var kind in new[] { HunterFeedbackKind.LightReaction, HunterFeedbackKind.Detected, HunterFeedbackKind.Scream, HunterFeedbackKind.AttackWindup })
            {
                var fact = new HunterFeedbackEvent(new EntityId(4), archetype, kind, Vector3.one, 10);
                Assert.That(_presenter.Feedback(_roster, fact, out var command), Is.True);
                Assert.That(_presenter.Feedback(_roster, fact, out _), Is.False);
                _driver.ObserveHunter(fact);
                Assert.That(Pool.Voices.Count(v => v.Remaining > 0 && v.Catalogue.Slot == (int)command.Slot), Is.EqualTo(1));
            }
            _driver.ObserveHit(new HunterHit()); // No valid attacker uses the existing player catch bank.
            Assert.That(_driver.PlayDeath(), Is.True);
            Assert.That(Pool.Voices.Count(v => v.Remaining > 0), Is.EqualTo(5));
        }
        [TestCase(false)][TestCase(true)]
        public void TurnAndDeliberationDeduplicateInEitherOrderPerHunter(bool habitFirst)
        {
            var hunter = new EntityId(4); var habit = new HunterHabitFact(hunter, HunterHabitKind.TurnToFace, Vector3.zero, 10);
            bool first = habitFirst ? _presenter.Habit(_roster, habit, out _) : _presenter.Deliberation(_roster, hunter, Vector3.zero, 10, out _);
            bool second = habitFirst ? _presenter.Deliberation(_roster, hunter, Vector3.zero, 10, out _) : _presenter.Habit(_roster, habit, out _);
            Assert.That(first, Is.True); Assert.That(second, Is.False);
            Assert.That(_presenter.Deliberation(_roster, new EntityId(5), Vector3.zero, 10, out _), Is.True);
            Assert.That(_presenter.Deliberation(_roster, hunter, Vector3.zero, 11, out _), Is.True);
            var cake = new HunterHabitFact(hunter, HunterHabitKind.CakeReaction, Vector3.zero, 11);
            Assert.That(_presenter.Habit(_roster, cake, out var command), Is.True);
            Assert.That(command.Id, Is.EqualTo("hunter.cake-reaction"));
            Assert.That(_presenter.Habit(_roster, cake, out _), Is.False);
        }
        [TestCase(false)] [TestCase(true)]
        public void DeathSelectsHandCueOrRetainedHunterSting(bool handDeath)
        {
            var hunter = new EntityId(4);
            _driver.ObserveHunter(new HunterFeedbackEvent(hunter, "echo", HunterFeedbackKind.LightReaction, Vector3.one, 1));
            _driver.ObserveHit(new HunterHit(hunter, new EntityId(7), 10, 1, Vector3.one));
            Assert.That(_driver.PlayDeath(handDeath), Is.True);
            if (handDeath)
                Assert.That(Pool.Voices.Any(v => v.Remaining > 0 && v.Cue == (int)CueId.Death && v.Emitter == 0), Is.True);
            else
                Assert.That(Pool.Voices.Any(v => v.Remaining > 0 && v.Emitter == 4 && v.Catalogue.Slot == (int)HunterCueSlot.DeathSting), Is.True);
        }

        [Test]
        public void ReplayBorrowsFootstepsButNotThePlayerSlotAndKeepsSuppliedPitch()
        {
            var fact = new HunterArchetypeFact(new EntityId(4), HunterArchetypeFactKind.ReplayedFootstep, Vector3.one, 20, 10, pitch: .94f);
            Assert.That(_presenter.Archetype(_roster, fact, out var command), Is.True);
            Assert.That(command.Exact, Is.True); Assert.That(command.Pitch, Is.EqualTo(.94f));
            Assert.That(_presenter.Archetype(_roster, fact, out _), Is.False);
            Assert.That(_driver.PlayFootstep(), Is.True); _driver.ObserveArchetype(fact);
            Assert.That(Pool.Voices.Count(v => v.Remaining > 0), Is.EqualTo(2));
            var sources = Get<AudioSource[]>(_driver, "_voices");
            int index = Array.FindIndex(Pool.Voices, v => v.Emitter == 4 && v.Remaining > 0);
            Assert.That(sources[index].pitch, Is.EqualTo(.94f));
        }
        [Test]
        public void WetClickAndTickHaveNoDelayAndStopSilencesOnlyItsOwner()
        {
            var wet = new WeaverFact(new EntityId(4), WeaverFactKind.WetClick, 1, Vector3.one, duration: .4f);
            Assert.That(_presenter.Weaver(_roster, wet, out var command), Is.True);
            Assert.That(command.Exact, Is.True); Assert.That(_presenter.Weaver(_roster, wet, out _), Is.False);
            var poolPresenter = new AudioSoundscapePresenter();
            var entry = new AudioCueCatalogueEntry(CueCategory.Hunter, (int)HunterCueSlot.AttackTiming, NoiseSourceKind.Other, true, true);
            Assert.That(poolPresenter.TryPlay(Pool, _config.Sounds[4], 4, new[] { 1f }, 1f, out var playback, 1f, entry), Is.True);
            Assert.That(playback.Delay, Is.Zero);
            foreach (TickingSound sound in Enum.GetValues(typeof(TickingSound)))
            {
                var fact = new TickingSoundFact(sound, Vector3.zero, .3f, 2, new EntityId(5));
                Assert.That(_presenter.Ticking(_roster, fact, out command), Is.True);
                Assert.That(command.Exact, Is.True); Assert.That(_presenter.Ticking(_roster, fact, out _), Is.False);
                if (sound == TickingSound.Tick) Assert.That(command.Interval, Is.EqualTo(.3f));
            }
            _driver.ObserveTicking(new TickingSoundFact(TickingSound.Tick, Vector3.zero, .3f, 2, new EntityId(5)));
            Assert.That(Pool.Voices.Single(v => v.Remaining > 0 && v.Emitter == 5).Remaining, Is.EqualTo(.3f));
            _driver.ObserveTicking(new TickingSoundFact(TickingSound.Stop, Vector3.zero, 0f, 3, new EntityId(5)));
            Assert.That(Pool.Voices.Any(v => v.Remaining > 0 && v.Emitter == 5), Is.False);
            Assert.That(Pool.Voices.Any(v => v.Remaining > 0 && v.Emitter == 4), Is.True);
        }
        [Test]
        public void RoomPresetsOverrideFloorAndHiddenTellsQueueOnceAcrossFloorReset()
        {
            _presenter.Theme(_roster, "castle-stone");
            _presenter.RoomTheme(_roster, _config, 7, "hospital", "corridor");
            Assert.That(_presenter.ZoneCutoff(_roster, _config, 7), Is.EqualTo(16000f));
            Assert.That(_presenter.ZoneCutoff(_roster, _config, 8), Is.EqualTo(6500f));
            var fact = new ProgressionEventFact(1, 3, ProgressionEventKind.HiddenMutation, ProgressionEventKind.HiddenMutation,
                FearAxis.None, "secret-rule", "echo", "echo.quickened-recording");
            _driver.SetInRun(false); _driver.ObserveProgressionEvent(fact); _driver.ObserveProgressionEvent(fact);
            var state = Get<AudioRosterDriverState>(_driver, "_roster");
            Assert.That(state.PendingTells.Count, Is.EqualTo(1));
            Assert.That(state.PendingTells[0].Id, Is.EqualTo(fact.TellId));
            _driver.ResetRun(true, true); _driver.SetInRun(true);
            Assert.That(state.PendingTells, Is.Empty);
            Assert.That(Pool.Voices.Count(v => v.Remaining > 0), Is.EqualTo(1));
            _driver.ObserveProgressionEvent(fact); Assert.That(state.PendingTells, Is.Empty);
            _driver.SetDeafening(5); _driver.SetMuffledDark(5); _driver.ClearSenses();
            Assert.That(Pool.WorldMix.DeafenedRemaining, Is.Zero); Assert.That(Pool.WorldMix.MuffledRemaining, Is.Zero);
        }
        [Test]
        public void OrchestratorPairsNewPublishersAcrossReconfigureAndTeardown()
        {
            Assert.That(RunSessionManager.Instance, Is.Null);
            var run = Component<RunSessionManager>(); var audio = Component<AudioManager>();
            var audioDriver = audio.GetComponent<AudioDriver>(); Set(audioDriver, "_soundscape", _driver);
            Set(audio, "_driver", audioDriver); Set(audio, "_initialized", true);
            var progression = Component<ProgressionSessionManager>(); var expedition = Component<ExpeditionSessionManager>();
            var effects = Component<HorrorEffectsManager>(); var route = Component<AudioOrchestrator>();
            var hunter = Component<Worsen.Domain.Hunter.HunterManager>();
            var registry = (List<Worsen.Domain.Hunter.HunterManager>)typeof(Worsen.Domain.Hunter.HunterRegistry)
                .GetField("Hunters", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            registry.Add(hunter);
            Set(route, "_run", run); Set(route, "_audio", audio);
            route.ConfigureExpansion(progression, effects, expedition, null);
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    Invoke(route, "OnEnable");
                    foreach (var name in new[] { "HunterArchetypePublished", "HunterHabitPublished", "WeaverFactPublished", "TickingSoundPublished", "BeforeTick", "HitAccepted" })
                        Assert.That(Get<Delegate>(run, name).GetInvocationList().Length, Is.EqualTo(1), name);
                    Assert.That(Get<Delegate>(progression, "ProgressionEventCommitted").GetInvocationList().Length, Is.EqualTo(1));
                    Assert.That(Get<Delegate>(expedition, "RoomThemePublished").GetInvocationList().Length, Is.EqualTo(1));
                    Assert.That(Get<Delegate>(effects, "SensesCleansed").GetInvocationList().Length, Is.EqualTo(1));
                    Assert.That(Get<Delegate>(hunter, "OnDeliberation").GetInvocationList().Length, Is.EqualTo(1));
                }
                Get<Delegate>(expedition, "ThemePublished").DynamicInvoke("castle", "torch", "castle-stone", "fog", "hands");
                Get<Delegate>(expedition, "RoomThemePublished").DynamicInvoke(7, "hospital", "corridor");
                Assert.That(_presenter.ZoneCutoff(Get<AudioRosterDriverState>(_driver, "_roster"), _config, 7), Is.EqualTo(16000f));
            }
            finally { Invoke(route, "OnDisable"); registry.Remove(hunter); Set(audioDriver, "_soundscape", null); }
            Assert.That(Get<Delegate>(hunter, "OnDeliberation"), Is.Null);
            Assert.That(Get<Delegate>(run, "WeaverFactPublished"), Is.Null);
            Assert.That(Get<Delegate>(progression, "ProgressionEventCommitted"), Is.Null);
            Assert.That(Get<Delegate>(expedition, "ThemePublished"), Is.Null);
            Assert.That(Get<Delegate>(effects, "SensesCleansed"), Is.Null);
        }
        private T Component<T>() where T : Component
        { var go = new GameObject(typeof(T).Name); go.SetActive(false); _owned.Add(go); return go.AddComponent<T>(); }
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
