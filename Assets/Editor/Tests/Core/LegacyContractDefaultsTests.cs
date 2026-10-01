// ============================================================================
// LegacyContractDefaultsTests.cs
// ============================================================================
// PURPOSE:
//   Keeps existing constructor calls and enum identities compatible at H1.
//   New metadata must not reinterpret old noise, damage, chase or run outcomes.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Core compatibility.
// KEY RESPONSIBILITIES:
//   - Assert legacy field values, new defaults and explicit metadata overrides.
// DEPENDENCIES:
//   - Core definitions, NUnit and pure UnityEngine value types only.
// USAGE NOTES:
//   Pure Edit Mode tests; no consumer migration or scene wiring is exercised.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Core
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class LegacyContractDefaultsTests
    {
        [Test]
        public void NoiseLegacyConstructionPreservesEveryFieldAndDefaultsToOther()
        {
            var noise = new NoiseEvent(new EntityId(7), Vector3.one, 2f, 12);
            Assert.That(noise.Source, Is.EqualTo(new EntityId(7)));
            Assert.That(noise.Position, Is.EqualTo(Vector3.one));
            Assert.That(noise.Loudness, Is.EqualTo(2f));
            Assert.That(noise.Tick, Is.EqualTo(12));
            Assert.That(noise.SourceKind, Is.EqualTo(NoiseSourceKind.Other));
            Assert.That(new NoiseEvent(noise.Source, noise.Position, noise.Loudness, noise.Tick, NoiseSourceKind.Firecracker).SourceKind,
                Is.EqualTo(NoiseSourceKind.Firecracker));
        }

        [Test]
        public void HunterLegacyConstructionPreservesDamageAndReasonWithHeavyLungeDefaults()
        {
            var hit = new HunterHit(new EntityId(2), new EntityId(1), 50, 12, Vector3.one);
            Assert.That(hit.Hunter, Is.EqualTo(new EntityId(2)));
            Assert.That(hit.Target, Is.EqualTo(new EntityId(1)));
            Assert.That(hit.Damage, Is.EqualTo(50));
            Assert.That(hit.Tick, Is.EqualTo(12));
            Assert.That(hit.HunterPosition, Is.EqualTo(Vector3.one));
            Assert.That(hit.Reason, Is.EqualTo(ChaseEndReason.Lunge));
            Assert.That(hit.Severity, Is.EqualTo(HitSeverity.Heavy));
            Assert.That(hit.Source, Is.EqualTo(HitSource.Lunge));
            var ranged = new HunterHit(hit.Hunter, hit.Target, 25, 13, Vector3.zero, ChaseEndReason.Projectile);
            Assert.That(ranged.Reason, Is.EqualTo(ChaseEndReason.Projectile));
            Assert.That(ranged.Source, Is.EqualTo(HitSource.Lunge), "Legacy ranged sources are not inferred or migrated.");
            var explicitHit = new HunterHit(hit.Hunter, hit.Target, 10, 14, Vector3.zero, severity: HitSeverity.Light, source: HitSource.Scream);
            Assert.That(explicitHit.Severity, Is.EqualTo(HitSeverity.Light));
            Assert.That(explicitHit.Source, Is.EqualTo(HitSource.Scream));
        }

        [Test]
        public void HandLegacyConstructionKeepsHitValuesWithLightHandDefaults()
        {
            var hand = new CollapseHandFact(new EntityId(1), 3, CollapseHandEventKind.Hit, Vector3.one, 0.5f, 25f, 12);
            Assert.That(hand.PlayerId, Is.EqualTo(new EntityId(1)));
            Assert.That(hand.RoomId, Is.EqualTo(3));
            Assert.That(hand.Kind, Is.EqualTo(CollapseHandEventKind.Hit));
            Assert.That(hand.Position, Is.EqualTo(Vector3.one));
            Assert.That(hand.SlowMultiplier, Is.EqualTo(0.5f));
            Assert.That(hand.Damage, Is.EqualTo(25f));
            Assert.That(hand.Tick, Is.EqualTo(12));
            Assert.That(hand.Severity, Is.EqualTo(HitSeverity.Light));
            Assert.That(hand.Source, Is.EqualTo(HitSource.Hand));
        }

        [TestCase(RunEndReason.Escaped)]
        [TestCase(RunEndReason.Died)]
        public void RunSummaryLegacyConstructionPreservesOutcomeAndAddsUnreportedDetails(RunEndReason reason)
        {
            var summary = new RunSummary(10d, 2, 3, 4, 1, 5d, reason);
            Assert.That(summary.ElapsedSeconds, Is.EqualTo(10d));
            Assert.That(summary.CakesCollected, Is.EqualTo(2));
            Assert.That(summary.GoldenCakesCollected, Is.EqualTo(3));
            Assert.That(summary.ChaseCount, Is.EqualTo(4));
            Assert.That(summary.ChasesEscaped, Is.EqualTo(1));
            Assert.That(summary.TotalChaseSeconds, Is.EqualTo(5d));
            Assert.That(summary.EndReason, Is.EqualTo(reason));
            Assert.That(summary.Seed, Is.Zero);
            Assert.That(summary.Scene, Is.EqualTo(SceneKey.None));
            Assert.That(summary.DeathCause, Is.EqualTo(DeathCause.None));
            Assert.That(summary.KillerArchetypeId, Is.Empty);
            Assert.That(summary.GrabsEscaped, Is.Zero);
            Assert.That(summary.SecondsFromExitOpenToEscape, Is.EqualTo(-1d));
            Assert.That(summary.DepthReached, Is.Zero);
        }

        [Test]
        public void RunSummaryAcceptsExplicitDetailsWithoutChangingLegacyFields()
        {
            var summary = new RunSummary(10d, 2, 3, 4, 1, 5d, RunEndReason.Died, 123, SceneKey.None,
                DeathCause.Hunter, "echo", 2, 3d, 4);
            Assert.That(summary.Seed, Is.EqualTo(123));
            Assert.That(summary.DeathCause, Is.EqualTo(DeathCause.Hunter));
            Assert.That(summary.KillerArchetypeId, Is.EqualTo("echo"));
            Assert.That(summary.GrabsEscaped, Is.EqualTo(2));
            Assert.That(summary.SecondsFromExitOpenToEscape, Is.EqualTo(3d));
            Assert.That(summary.DepthReached, Is.EqualTo(4));
        }

        [Test]
        public void TelemetryKeepsOriginalOrdinalsAndUsesAppendedCsvNames()
        {
            string[] original = { "HorizontalSpeed", "ChaseStarted", "ChaseEnded", "InputLockStarted", "InputLockEnded",
                "LookBackStarted", "LookBackEnded", "VaultAttempt", "VaultFailed", "Proximity", "Heat", "FloorTime", "AcceptedHit" };
            for (int i = 0; i < original.Length; i++) Assert.That(((TelemetrySampleKind)i).ToString(), Is.EqualTo(original[i]));
            string[] added = { "RoundStarted", "RoundEnded", "WalletChanged", "ProgressionChoice", "FloorSeed", "HunterStall" };
            for (int i = 0; i < added.Length; i++) Assert.That(((TelemetrySampleKind)(original.Length + i)).ToString(), Is.EqualTo(added[i]));
        }

        [Test]
        public void GraceAndGuidancePreserveExplicitIdentityAndInterval()
        {
            var grace = new GraceWindowFact(new EntityId(1), 10, 20, HitSeverity.Heavy);
            Assert.That(grace.PlayerId, Is.EqualTo(new EntityId(1)));
            Assert.That(grace.StartTick, Is.EqualTo(10));
            Assert.That(grace.EndTick, Is.EqualTo(20));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GraceWindowFact(new EntityId(1), 20, 10, HitSeverity.Light));
            var target = new GuidanceTarget(GuidanceKind.WhiteArrow, Vector3.forward, Vector3.one, anchorId: 3, isFallback: true);
            Assert.That(target.AnchorId, Is.EqualTo(3));
            Assert.That(target.EntityId, Is.EqualTo(EntityId.None));
            Assert.That(target.IsFallback, Is.True);
        }
    }
}
