// ============================================================================
// HunterAttackVocalTests.cs
// ============================================================================
// PURPOSE:
//   Verifies occasional attack-commitment screams and immediate dead-target silence.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10), test suite (section 11) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Keep detection silent, use injected probability and respect scream cooldowns.
//   - Prevent pending or new enemy attack feedback after player death.
// DEPENDENCIES:
//   - Hunter controller/profile/state, Core feedback, Player/Level views, NUnit.
// USAGE NOTES:
//   Pure ticks use scripted random draws and owned config only; no audio playback.
// ============================================================================
using System;
using System.Collections.Generic;
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
    public sealed class HunterAttackVocalTests
    {
        private sealed class LevelFixture : IReadOnlyLevelState { public bool IsReady => false; public LevelGraph Graph => null; }
        private sealed class ScriptedRandom : System.Random
        {
            private readonly double[] _values;
            public int Rolls { get; private set; }
            public ScriptedRandom(params double[] values) { _values = values; }
            public override double NextDouble() => _values[Math.Min(Rolls++, _values.Length - 1)];
        }
        private HunterProfile _profile;
        private HunterBehaviorState _state;
        private PlayerBehaviorState _player;
        private HunterController _controller;
        private ScriptedRandom _random;
        private static readonly SightProbe Visible = new SightProbe(true, true, true);
        private void Initialize(params double[] rolls)
        {
            _profile = ScriptableObject.CreateInstance<HunterProfile>();
            var serialized = new SerializedObject(_profile);
            serialized.FindProperty("_archetypeKey").stringValue = "rusher";
            serialized.FindProperty("_screamOnDetection").boolValue = true;
            serialized.FindProperty("_attackScreamChance").floatValue = 0.25f;
            serialized.FindProperty("_sensorIntervalTicks").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _player = new PlayerBehaviorState { Id = new EntityId(51), Health = 100f, SprintSpeed = 8f, Position = Vector3.forward * 3f };
            _state = new HunterBehaviorState(); _random = new ScriptedRandom(rolls);
            _controller = new HunterController(_state, _profile, _random, _player, new LevelFixture());
            _controller.Reset(new EntityId(-51), Vector3.zero, Vector3.forward);
        }
        [TearDown] public void Cleanup() { if (_profile != null) UnityEngine.Object.DestroyImmediate(_profile); }
        private List<HunterFeedbackKind> Drain()
        { var kinds = new List<HunterFeedbackKind>(); while (_controller.TryDequeueFeedback(out HunterFeedbackEvent item)) kinds.Add(item.Kind); return kinds; }

        [Test] public void ChanceIsRolledAtCommitmentAndScreamsNeverOccurOnDetectionOrRecovery()
        {
            Initialize(0.9, 0.1, 0.1);
            _player.Position = Vector3.forward * 8f;
            _controller.Tick(Visible, 0.1f, 0);
            Assert.That(Drain().Contains(HunterFeedbackKind.Scream), Is.False);
            Assert.That(_random.Rolls, Is.Zero);
            _player.Position = Vector3.forward * 3f;
            HunterTickResult first = _controller.Tick(Visible, 0.1f, 1);
            Assert.That(first.BeginLunge, Is.True);
            Assert.That(Drain().Contains(HunterFeedbackKind.Scream), Is.False, "The .9 draw must fail the .25 chance.");
            int screams = 0, commitments = 1;
            for (int tick = 2; tick < 110; tick++)
            {
                HunterTickResult result = _controller.Tick(Visible, 0.1f, tick);
                if (result.BeginLunge) commitments++;
                List<HunterFeedbackKind> cues = Drain();
                if (result.BeginLunge)
                    Assert.That(cues.FindAll(cue => cue == HunterFeedbackKind.Scream || cue == HunterFeedbackKind.AttackWindup).Count, Is.EqualTo(1), "An attack has one onset voice, never a growl layered with a scream.");
                foreach (HunterFeedbackKind cue in cues)
                    if (cue == HunterFeedbackKind.Scream)
                    { Assert.That(result.BeginLunge, Is.True, "Only a real attack commitment may scream."); screams++; }
            }
            Assert.That(commitments, Is.GreaterThan(3));
            Assert.That(screams, Is.EqualTo(1), "The successful second draw starts a cooldown covering later attacks.");
            Assert.That(_random.Rolls, Is.EqualTo(2), "Cooldown attacks do not reroll or emit additional vocals.");
        }

        [Test] public void DeathDropsAlreadyQueuedVocalsAndPreventsFurtherAttackDecisions()
        {
            Initialize(0.1);
            Assert.That(_controller.Tick(Visible, 0.1f, 0).BeginLunge, Is.True);
            int serial = _state.AttackSerial;
            _player.Health = 0f;
            Assert.That(_controller.TryDequeueFeedback(out _), Is.False, "A cue queued while alive must not be delivered after death.");
            HunterTickResult result = _controller.Tick(Visible, 0.1f, 1);
            Assert.That(result.BeginLunge, Is.False);
            Assert.That(_state.IsActive, Is.False);
            Assert.That(_state.LungePhase, Is.EqualTo(HunterLungePhase.None));
            Assert.That(_state.AttackSerial, Is.EqualTo(serial));
            Assert.That(Drain(), Is.Empty);
            Assert.That(_random.Rolls, Is.EqualTo(1));
        }
    }
}
