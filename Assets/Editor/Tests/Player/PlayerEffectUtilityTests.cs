// ============================================================================
// PlayerEffectUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Locks every provisional Player mapping independently of catalogue publication.
//   Checks neutral profile parity, exact id matching, deterministic combinations
//   and caps without changing runtime or shared ScriptableObject assets.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Verify each effect touches only its declared channel and honors stack limits.
// DEPENDENCIES:
//   - Core effects, Player config/utility, NUnit and temporary Unity configs.
// USAGE NOTES:
//   Edit Mode fixture; all arithmetic under test is pure. Reflection changes only
//   fixture-owned config instances to prove mappings are data, not hardcoded ids.
// ============================================================================
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;

namespace Worsen.Tests.Player
{
    public sealed class PlayerEffectUtilityTests
    {
        private PlayerProfile _profile;
        private PlayerEffectConfig _config;
        [SetUp] public void SetUp()
        { _profile = ScriptableObject.CreateInstance<PlayerProfile>(); _config = ScriptableObject.CreateInstance<PlayerEffectConfig>(); }
        [TearDown] public void TearDown()
        { UnityEngine.Object.DestroyImmediate(_profile); UnityEngine.Object.DestroyImmediate(_config); }

        private float Baseline(PlayerEffectStat stat)
        {
            switch (stat)
            {
                case PlayerEffectStat.GraceSeconds: return _profile.HitGraceSeconds;
                case PlayerEffectStat.BoostDuration: return _profile.HeavyHitBoostSeconds;
                case PlayerEffectStat.MaximumHealth: return _profile.MaximumHealth;
                case PlayerEffectStat.SprintSpeed: return _profile.SprintSpeed;
                case PlayerEffectStat.GroundAcceleration: return _profile.GroundAcceleration;
                case PlayerEffectStat.AirAcceleration: return _profile.AirAcceleration;
                case PlayerEffectStat.TraversalDuration: return _profile.VaultDuration;
                case PlayerEffectStat.SlideDuration: return _profile.SlideDuration;
                case PlayerEffectStat.SlideRetention: return _profile.SlideWallSpeedRetention;
                case PlayerEffectStat.Regeneration:
                case PlayerEffectStat.FloorStartHealth:
                case PlayerEffectStat.JumpHeight: return 1f;
                default: return 0f;
            }
        }

        internal static ActiveEffects Effects(string id, int stacks = 1)
            => new ActiveEffects(new[] { new ActiveEffect(new EffectId(id), EffectKind.Upgrade, stacks) });
        private float Value(PlayerEffectStat stat, IReadOnlyActiveEffects effects)
            => PlayerEffectUtility.Value(_config, effects, stat, Baseline(stat));

        [Test]
        public void NullEmptyAndUnknownEffectsPreserveEveryProfileValueExactly()
        {
            foreach (PlayerEffectStat stat in Enum.GetValues(typeof(PlayerEffectStat)))
                foreach (IReadOnlyActiveEffects effects in new IReadOnlyActiveEffects[] { null, default(ActiveEffects), Effects("unknown") })
                    Assert.That(Value(stat, effects), Is.EqualTo(Baseline(stat)), stat.ToString());
            Assert.That(PlayerEffectUtility.Value(_config, null, PlayerEffectStat.TraversalDuration, _profile.MantleDuration), Is.EqualTo(_profile.MantleDuration));
            Assert.That(PlayerEffectUtility.Value(_config, null, PlayerEffectStat.BoostDuration, _profile.LightHitBoostSeconds), Is.EqualTo(_profile.LightHitBoostSeconds));
        }

        [TestCase("short-grace", PlayerEffectStat.GraceSeconds, 0.72f)]
        [TestCase("thick-skin", PlayerEffectStat.GraceSeconds, 1.45f)]
        [TestCase("short-burst", PlayerEffectStat.BoostDuration, 0.6f)]
        [TestCase("heavy-legs", PlayerEffectStat.BoostDuration, 0f)]
        [TestCase("long-boost", PlayerEffectStat.BoostDuration, 1.56f)]
        [TestCase("slow-mend", PlayerEffectStat.Regeneration, 0.5f)]
        [TestCase("no-regen", PlayerEffectStat.Regeneration, 0f)]
        [TestCase("field-kit", PlayerEffectStat.Regeneration, 1.5f)]
        [TestCase("rough-start", PlayerEffectStat.FloorStartHealth, 0.5f)]
        [TestCase("thin-skin", PlayerEffectStat.MaximumHealth, 75f)]
        [TestCase("speed-boost", PlayerEffectStat.SprintSpeed, 8.4f)]
        [TestCase("quick-start", PlayerEffectStat.GroundAcceleration, 72f)]
        [TestCase("air-control", PlayerEffectStat.AirAcceleration, 31.25f)]
        [TestCase("fast-hands", PlayerEffectStat.TraversalDuration, 0.2125f)]
        [TestCase("longer-slide", PlayerEffectStat.SlideDuration, 1.56f)]
        [TestCase("higher-jump", PlayerEffectStat.JumpHeight, 1.2f)]
        [TestCase("stored-momentum", PlayerEffectStat.StoredMomentum, 1f)]
        [TestCase("soft-landing", PlayerEffectStat.SoftLanding, 1f)]
        [TestCase("quiet-slide", PlayerEffectStat.QuietSlide, 1f)]
        [TestCase("low-profile", PlayerEffectStat.LowProfile, 1f)]
        [TestCase("no-look-back", PlayerEffectStat.NoLookBack, 1f)]
        public void EachEffectChangesOnlyItsChannels(string id, PlayerEffectStat changed, float expected)
        {
            var effects = Effects(id);
            foreach (PlayerEffectStat stat in Enum.GetValues(typeof(PlayerEffectStat)))
            {
                float target = stat == changed ? expected : Baseline(stat);
                if (id == "longer-slide" && stat == PlayerEffectStat.SlideRetention) target = 0.95f;
                Assert.That(Value(stat, effects), Is.EqualTo(target).Within(0.00001f), stat.ToString());
            }
        }

        [TestCase("thick-skin", PlayerEffectStat.GraceSeconds, 1.7f, 1.95f)]
        [TestCase("long-boost", PlayerEffectStat.BoostDuration, 1.92f, 2.28f)]
        [TestCase("field-kit", PlayerEffectStat.Regeneration, 2f, 2.5f)]
        [TestCase("quick-start", PlayerEffectStat.GroundAcceleration, 84f, 96f)]
        [TestCase("air-control", PlayerEffectStat.AirAcceleration, 37.5f, 43.75f)]
        [TestCase("fast-hands", PlayerEffectStat.TraversalDuration, 0.180625f, 0.15353125f)]
        public void StacksAddOrCompoundToThreeThenCap(string id, PlayerEffectStat stat, float two, float three)
        {
            Assert.That(Value(stat, Effects(id, 2)), Is.EqualTo(two).Within(0.00001f));
            Assert.That(Value(stat, Effects(id, 3)), Is.EqualTo(three).Within(0.00001f));
            Assert.That(Value(stat, Effects(id, int.MaxValue)), Is.EqualTo(three).Within(0.00001f));
        }

        [Test]
        public void NonStackingRowsIgnoreExtraCopiesAndCapsHaveStrictBoundaries()
        {
            foreach (PlayerEffectConfig.Mapping row in _config.Mappings)
                if (row.StackCap == 1) Assert.That(Value(row.Stat, Effects(row.EffectId, 99)), Is.EqualTo(Value(row.Stat, Effects(row.EffectId))));
            Assert.That(Value(PlayerEffectStat.SprintSpeed, Effects("speed-boost", int.MaxValue)), Is.EqualTo(9.49f).Within(0.00001f));
            Assert.That(Value(PlayerEffectStat.SprintSpeed, Effects("speed-boost", 4)), Is.LessThan(_config.HunterChaseSpeedCeiling));
            Assert.That(PlayerEffectUtility.Value(_config, Effects("fast-hands", 3), PlayerEffectStat.TraversalDuration, 0.11f), Is.EqualTo(0.1f));
            Assert.That(PlayerEffectUtility.Value(_config, null, PlayerEffectStat.TraversalDuration, 0.05f), Is.EqualTo(0.05f), "No effect does not retune a profile.");
            Assert.That(PlayerEffectUtility.Value(_config, Effects("longer-slide"), PlayerEffectStat.SlideRetention, 0.99f), Is.EqualTo(1f));
        }

        [Test]
        public void MultipliersThenBonusesAreOrderIndependentAndDisablesWin()
        {
            var entries = new[] {
                new ActiveEffect(new EffectId("short-grace"), EffectKind.Curse, 1),
                new ActiveEffect(new EffectId("thick-skin"), EffectKind.Upgrade, 3),
                new ActiveEffect(new EffectId("slow-mend"), EffectKind.Curse, 1),
                new ActiveEffect(new EffectId("field-kit"), EffectKind.Upgrade, 3),
                new ActiveEffect(new EffectId("short-burst"), EffectKind.Curse, 1),
                new ActiveEffect(new EffectId("long-boost"), EffectKind.Upgrade, 3) };
            var effects = new ActiveEffects(entries);
            Assert.That(Value(PlayerEffectStat.GraceSeconds, effects), Is.EqualTo(1.47f).Within(0.00001f));
            Assert.That(Value(PlayerEffectStat.Regeneration, effects), Is.EqualTo(1.25f));
            Assert.That(Value(PlayerEffectStat.BoostDuration, effects), Is.EqualTo(1.14f).Within(0.00001f));
            Array.Reverse(entries);
            foreach (PlayerEffectStat stat in Enum.GetValues(typeof(PlayerEffectStat)))
                Assert.That(Value(stat, new ActiveEffects(entries)), Is.EqualTo(Value(stat, effects)));
            var disabled = new System.Collections.Generic.List<ActiveEffect>(entries) {
                new ActiveEffect(new EffectId("no-regen"), EffectKind.Curse, 1),
                new ActiveEffect(new EffectId("heavy-legs"), EffectKind.Curse, 1) };
            Assert.That(Value(PlayerEffectStat.Regeneration, new ActiveEffects(disabled)), Is.Zero);
            Assert.That(Value(PlayerEffectStat.BoostDuration, new ActiveEffects(disabled)), Is.Zero);
        }

        [Test]
        public void MappingIdsAreExactConfigDataNotControllerConstants()
        {
            typeof(PlayerEffectConfig).GetField("_mappings", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_config,
                new[] { new PlayerEffectConfig.Mapping("Custom Exact Key", PlayerEffectStat.GraceSeconds, PlayerEffectOperation.Add, 2f, 2) });
            Assert.That(Value(PlayerEffectStat.GraceSeconds, Effects("Custom Exact Key", 3)), Is.EqualTo(5.2f));
            Assert.That(Value(PlayerEffectStat.GraceSeconds, Effects("custom exact key")), Is.EqualTo(_profile.HitGraceSeconds));
            Assert.That(Value(PlayerEffectStat.GraceSeconds, Effects("short-grace")), Is.EqualTo(_profile.HitGraceSeconds));
        }
    }
}
