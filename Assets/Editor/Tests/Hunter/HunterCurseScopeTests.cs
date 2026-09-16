// ============================================================================
// HunterCurseScopeTests.cs
// ============================================================================
// PURPOSE:
//   Proves per-hunter curse admission and observable consequences across the roster.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10), test suite (section 11) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve general traits while excluding every other species' curse bits.
//   - Check hearing, attack timing and bounded detection screams without engine fixtures.
// DEPENDENCIES:
//   - Hunter controller/state/profile, Core traits, Player/Level read-only views, NUnit.
// USAGE NOTES:
//   Pure controller ticks use injected time/randomness and owned temporary configs.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using Worsen.Domain.Level;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    public sealed class HunterCurseScopeTests
    {
        private static readonly ProgressionTraits[] Groups = {
            ProgressionTraits.RusherLongStride | ProgressionTraits.RusherSecondWind | ProgressionTraits.RusherBloodScent,
            ProgressionTraits.LurkerDarkAdaptation | ProgressionTraits.LurkerCrookedStep | ProgressionTraits.LurkerStolenSilence,
            ProgressionTraits.WatcherLongMemory | ProgressionTraits.WatcherCuttingCorners | ProgressionTraits.WatcherUnquietGaze,
            ProgressionTraits.HexerSplitBolt | ProgressionTraits.HexerHastyScript | ProgressionTraits.HexerLingeringHex,
            ProgressionTraits.ThorncallerThornRing | ProgressionTraits.ThorncallerQuickRoots | ProgressionTraits.ThorncallerReachingRoots
        };
        private sealed class LevelFixture : IReadOnlyLevelState { public bool IsReady => false; public LevelGraph Graph => null; }
        private HunterProfile _profile;
        private HunterBehaviorState _state;
        private HunterController _controller;
        private PlayerBehaviorState _player;
        private void Initialize(string key)
        {
            _profile = ScriptableObject.CreateInstance<HunterProfile>();
            var serialized = new SerializedObject(_profile);
            serialized.FindProperty("_archetypeKey").stringValue = key;
            serialized.FindProperty("_screamOnDetection").boolValue = false;
            serialized.FindProperty("_sensorIntervalTicks").intValue = 1;
            serialized.FindProperty("_screamCooldownSeconds").floatValue = 10f;
            serialized.FindProperty("_noiseMaxAgeSeconds").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _state = new HunterBehaviorState();
            _player = new PlayerBehaviorState { Id = new EntityId(11), Health = 100, SprintSpeed = 8f, Position = Vector3.forward * 5f };
            _controller = new HunterController(_state, _profile, new System.Random(7), _player, new LevelFixture());
            _controller.Reset(new EntityId(12), Vector3.zero, Vector3.forward);
        }
        [TearDown] public void Cleanup() { if (_profile != null) UnityEngine.Object.DestroyImmediate(_profile); }

        [TestCase("rusher", 0)] [TestCase("lurker", 1)] [TestCase("watcher", 2)]
        [TestCase("hexer", 3)] [TestCase("thorncaller", 4)] [TestCase("custom", -1)]
        public void GlobalTraitSnapshotKeepsOnlyOwnCursesAndAllGeneralBits(string key, int group)
        {
            Initialize(key);
            ProgressionTraits allSpecific = Groups.Aggregate(ProgressionTraits.None, (all, item) => all | item);
            ProgressionTraits allDefined = Enum.GetValues(typeof(ProgressionTraits)).Cast<ProgressionTraits>().Aggregate(ProgressionTraits.None, (all, item) => all | item);
            ProgressionTraits general = allDefined & ~allSpecific;
            _controller.SetTraits(allDefined);
            Assert.That((ProgressionTraits)typeof(HunterBehaviorState).GetField("Traits", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_state), Is.EqualTo(general | (group < 0 ? ProgressionTraits.None : Groups[group])));
            Assert.That(_controller.EffectiveAttackDistance, Is.EqualTo(_profile.LungeDistance * (group == 0 ? 1.35f : 1f)).Within(0.0001f));
            Assert.That(_controller.EffectiveSightRange, Is.EqualTo(_profile.SightRange * (group == 2 ? 1.25f : 1f)).Within(0.0001f));
            Assert.That(_controller.EffectiveSightCone, Is.EqualTo(_profile.SightConeDegrees * (group == 1 ? 1.3f : 1f)).Within(0.0001f));
            Assert.That(_controller.SplitBolt, Is.EqualTo(group == 3));
            Assert.That(_controller.ThornRing, Is.EqualTo(group == 4));
            Assert.That(_controller.ProjectileSpeed, Is.EqualTo(_profile.ProjectileSpeed * (group == 3 ? 0.65f : 1f)).Within(0.0001f));
            Assert.That(_controller.SpikeRadius, Is.EqualTo(_profile.SpikeRadius * (group == 4 ? 1.4f : 1f)).Within(0.0001f));
            float expectedWindup = Mathf.Max(0.15f, _profile.LungeWindupSeconds * (group == 1 || group == 3 || group == 4 ? 0.7f : 1f));
            Assert.That(_controller.WindupDuration, Is.EqualTo(expectedWindup).Within(0.0001f));
            _controller.SetTraits(general);
            Assert.That((ProgressionTraits)typeof(HunterBehaviorState).GetField("Traits", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_state), Is.EqualTo(general), "A new snapshot must remove previous curse bits.");
            Assert.That(_controller.EffectiveSightRange, Is.EqualTo(_profile.SightRange));
        }

        [TestCase("rusher", true)] [TestCase("lurker", false)] [TestCase("watcher", false)]
        [TestCase("hexer", false)] [TestCase("thorncaller", false)]
        public void BloodScentExtendsNoiseAgeOnlyForTheRusher(string key, bool accepts)
        {
            Initialize(key); _controller.SetTraits(ProgressionTraits.RusherBloodScent);
            _controller.Tick(default, 0.5f, 3);
            var noise = new NoiseEvent(_player.Id, Vector3.zero, 1f, 0);
            Assert.That(_controller.HearNoise(noise, 1f), Is.EqualTo(accepts));
            Assert.That(_state.BeliefConfidence > 0f, Is.EqualTo(accepts));
        }

        private int DrainScreams()
        { int count = 0; while (_controller.TryDequeueFeedback(out HunterFeedbackEvent item)) if (item.Kind == HunterFeedbackKind.Scream) count++; return count; }

        [TestCase("watcher", true)] [TestCase("rusher", false)] [TestCase("lurker", false)]
        [TestCase("hexer", false)] [TestCase("thorncaller", false)]
        public void UnquietGazeEnablesOnlyWatcherAttackScreamsAndRespectsCooldown(string key, bool screams)
        {
            Initialize(key); _controller.SetTraits(ProgressionTraits.WatcherUnquietGaze);
            var serialized = new SerializedObject(_profile);
            serialized.FindProperty("_attackScreamChance").floatValue = 1f; serialized.ApplyModifiedPropertiesWithoutUndo();
            var visible = new SightProbe(true, true, true);
            _controller.Tick(visible, 0.1f, 0);
            Assert.That(DrainScreams(), Is.Zero, "Detection alone cannot scream.");
            _player.Position = Vector3.forward * 3f;
            _controller.Tick(visible, 0.1f, 1);
            Assert.That(DrainScreams(), Is.EqualTo(screams ? 1 : 0));
            for (int tick = 2; tick <= 50; tick++) _controller.Tick(visible, 0.1f, tick);
            Assert.That(DrainScreams(), Is.Zero, "Repeated attack commitments inside the cooldown cannot spam screams.");
            for (int tick = 51; tick <= 125; tick++) _controller.Tick(visible, 0.1f, tick);
            Assert.That(DrainScreams() > 0, Is.EqualTo(screams));
        }
    }
}
