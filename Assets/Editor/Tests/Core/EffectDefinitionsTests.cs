// ============================================================================
// EffectDefinitionsTests.cs
// ============================================================================
// PURPOSE:
//   Protects stable effect identities and frozen, value-equal active-effect views.
//   Invalid or duplicate stacks must fail rather than silently change game rules.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Core Effects.
// KEY RESPONSIBILITIES:
//   - Check lookup, enumeration, copy isolation, validation and equality.
// DEPENDENCIES:
//   - Core definitions, System and NUnit only.
// USAGE NOTES:
//   Pure Edit Mode tests; no scene, clock, engine operations or random source.
// ============================================================================
using System;
using System.Linq;
using NUnit.Framework;
using Worsen.Core;

namespace Worsen.Tests.Core
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class EffectDefinitionsTests
    {
        private static ActiveEffect Entry(string id, int stacks = 1, EffectKind kind = EffectKind.Upgrade) =>
            new ActiveEffect(new EffectId(id), kind, stacks);

        [Test]
        public void LookupAndEnumerationUseStableOrdinalIdsAndDefensiveCopies()
        {
            var entries = new[] { Entry("z", 2), Entry("A") };
            IReadOnlyActiveEffects effects = new ActiveEffects(entries);
            entries[0] = Entry("changed");
            Assert.That(effects.Count, Is.EqualTo(2));
            Assert.That(effects.Has(new EffectId("z")), Is.True);
            Assert.That(effects.Stacks(new EffectId("z")), Is.EqualTo(2));
            Assert.That(effects.Has(new EffectId("a")), Is.False);
            Assert.That(effects.Stacks(default), Is.Zero);
            Assert.That(effects.Select(e => e.Id.Value), Is.EqualTo(new[] { "A", "z" }));
            Assert.That(new EffectId(" A ").Value, Is.EqualTo(" A "));
        }

        [Test]
        public void EqualityAndHashingAreOrderIndependentAndIncludeKindAndStacks()
        {
            var a = new ActiveEffects(new[] { Entry("z", 2), Entry("a") });
            var b = new ActiveEffects(new[] { Entry("a"), Entry("z", 2) });
            Assert.That(a == b, Is.True);
            Assert.That(a.Equals((object)b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a != new ActiveEffects(new[] { Entry("a"), Entry("z", 3) }), Is.True);
            Assert.That(a != new ActiveEffects(new[] { Entry("a"), Entry("z", 2, EffectKind.Curse) }), Is.True);
            Assert.That(Entry("a") == Entry("a"), Is.True);
            Assert.That(new EffectId("a") == new EffectId("a"), Is.True);
            Assert.That(new EffectId("a").GetHashCode(), Is.EqualTo(new EffectId("a").GetHashCode()));
        }

        [Test]
        public void DefaultSnapshotIsEmptyAndEqualsConstructedEmpty()
        {
            var empty = default(ActiveEffects);
            Assert.That(empty, Is.EqualTo(new ActiveEffects(Array.Empty<ActiveEffect>())));
            Assert.That(empty.GetHashCode(), Is.EqualTo(new ActiveEffects(Array.Empty<ActiveEffect>()).GetHashCode()));
            Assert.That(empty.Count, Is.Zero);
            Assert.That(empty.Has(new EffectId("missing")), Is.False);
            Assert.That(empty.ToArray(), Is.Empty);
            Assert.That(default(EffectId).IsValid, Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        public void BlankIdsAreRejected(string id) => Assert.Throws<ArgumentException>(() => new EffectId(id));

        [TestCase(0)]
        [TestCase(-1)]
        public void NonpositiveStacksAreRejected(int stacks) => Assert.Throws<ArgumentOutOfRangeException>(() => Entry("a", stacks));

        [Test]
        public void InvalidEntriesAndDuplicateIdsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new ActiveEffects(null));
            Assert.Throws<ArgumentException>(() => new ActiveEffect(default, EffectKind.Threat, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Entry("a", 1, (EffectKind)99));
            Assert.Throws<ArgumentException>(() => new ActiveEffects(new[] { default(ActiveEffect) }));
            Assert.Throws<ArgumentException>(() => new ActiveEffects(new[] { Entry("a"), Entry("a", 2, EffectKind.Curse) }));
        }
    }
}
